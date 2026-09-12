using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

/// <summary>How the model classified its own answer. Drives the 무관한 질문 steering rule.</summary>
public enum AiAnswerTopic
{
    Process,
    Related,
    OffTopic
}

/// <summary>Parsed model output. Falls back to plain text when the JSON envelope is missing.</summary>
public readonly struct AiModelResponse
{
    public AiModelResponse(string answer, AiAnswerTopic topic, bool grounded, bool showGuide = false)
    {
        Answer = answer;
        Topic = topic;
        Grounded = grounded;
        ShowGuide = showGuide;
    }

    public string Answer { get; }
    public AiAnswerTopic Topic { get; }

    /// <summary>False when the model says it answered without supporting material.</summary>
    public bool Grounded { get; }

    /// <summary>
    /// 시각 공정 가이드를 띄워 달라는 요청. 이음이는 조작을 말로 읊지 않으므로, 위치나 방법을
    /// 보여 줘야 하는 상황은 전부 이 플래그와 <see cref="ProcessGuideService"/>로 수렴한다.
    /// 봉투에 필드가 없으면 false다.
    /// </summary>
    public bool ShowGuide { get; }

    public bool HasAnswer => !string.IsNullOrWhiteSpace(Answer);
}

/// <summary>
/// Turns the current process, the retrieved material and the conversation history into the
/// Solar request. Every behavioural rule from F-012 lives in the system prompt; the safety
/// filter afterwards is what enforces the ones a prompt cannot guarantee.
/// </summary>
public sealed class AiPromptBuilder
{
    const int MaxKnowledgeEntries = 4;

    readonly AiConfig _config;
    readonly AiKnowledgeBase _knowledge;

    public AiPromptBuilder(AiConfig config, AiKnowledgeBase knowledge)
    {
        _config = config ?? new AiConfig();
        _knowledge = knowledge ?? AiKnowledgeBase.CreateEmpty();
    }

    public AiChatRequest Build(
        string question,
        AiProcessContext context,
        IReadOnlyList<AiChatMessage> history,
        int offTopicCount,
        string extraInstruction = null)
    {
        var conversation = _config.Conversation;
        var request = new AiChatRequest
        {
            Temperature = _config.Llm.Temperature,
            MaxTokens = _config.Llm.MaxTokens
        };

        request.Messages.Add(new AiChatMessage(AiChatRole.System, BuildSystemPrompt(context, offTopicCount)));

        var grounding = BuildGrounding(question, context);
        if (!string.IsNullOrEmpty(grounding))
            request.Messages.Add(new AiChatMessage(AiChatRole.System, grounding));

        if (history != null)
        {
            var start = Math.Max(0, history.Count - _config.Llm.HistoryTurns * 2);
            for (var i = start; i < history.Count; i++)
                request.Messages.Add(history[i]);
        }

        var userText = Truncate(question, conversation.MaxUserChars);
        if (!string.IsNullOrEmpty(extraInstruction))
            userText = $"{userText}\n\n[재시도 지시] {extraInstruction}";

        request.Messages.Add(new AiChatMessage(AiChatRole.User, userText));
        return request;
    }

    string BuildSystemPrompt(AiProcessContext context, int offTopicCount)
    {
        var conversation = _config.Conversation;
        var allowDirect = context != null && context.AllowDirectAnswer;

        // 실패가 쌓여도 길이를 늘리지 않는다. DirectAnswerMaxChars의 여유분은 '구체적인 조작을
        // 말로 설명하는' 답을 담기 위한 것이었는데, 그 역할을 시각 가이드가 가져갔다. 안전
        // 필터는 종전대로 넉넉한 상한으로 자르므로(AiSafetyFilter.InspectOutput) 충돌하지 않는다.
        var maxChars = conversation.MaxResponseChars;

        var builder = new StringBuilder();
        builder.AppendLine("너는 '이음이'다. 숭례문 복원 현장을 체험하는 VR 콘텐츠에서 플레이어를 돕는 학습 도우미다.");
        builder.AppendLine("플레이어는 전통 목조건축을 처음 배우는 신입 목수이고, 현장에는 노장(스승)이 함께 있다.");
        builder.AppendLine();

        builder.AppendLine("[말투]");
        builder.AppendLine("- 한국어 반말로, 친근하고 차분하게 말한다.");
        builder.AppendLine("- 이모지, 마크다운, 목록 기호, 괄호 설명을 쓰지 않는다. 음성으로 읽을 문장만 쓴다.");
        builder.AppendLine($"- {conversation.TargetResponseChars}자 안팎으로 답하고, 어떤 경우에도 {maxChars}자를 넘기지 않는다.");
        builder.AppendLine("- 길게 설명해야 하면 핵심만 먼저 말하고, 더 궁금하면 다시 물어보라고 한다.");
        builder.AppendLine();

        builder.AppendLine("[할 수 있는 것]");
        builder.AppendLine("- 현재 공정의 원리와 목적 설명");
        builder.AppendLine("- 도구 사용법 설명");
        builder.AppendLine("- 실패 원인에 대한 보조 설명");
        builder.AppendLine("- 전통 목조건축 용어 설명");
        builder.AppendLine("- 숭례문 복원과 기술 계승의 의미 설명");
        builder.AppendLine("- 생각을 유도하는 힌트 제공");
        builder.AppendLine();

        builder.AppendLine("[하지 않는 것]");
        builder.AppendLine("- 플레이어 대신 작업을 수행하거나 대신 해 주겠다고 말하지 않는다.");
        builder.AppendLine("- 합격, 불합격, 점수, 등급을 판정하거나 예고하지 않는다.");
        builder.AppendLine("- 노장의 지시를 바꾸거나 반대하지 않는다.");
        builder.AppendLine("- 게임 상태나 저장 데이터를 바꿔 주겠다고 말하지 않는다.");
        builder.AppendLine("- 자료에 없는 역사적 사실을 지어내지 않는다.");
        builder.AppendLine("- 어디를 어떻게 다루라고 조작을 하나하나 말로 지시하지 않는다. 그건 시각 가이드가 맡는다.");
        builder.AppendLine();

        builder.AppendLine("[근거]");
        builder.AppendLine("- 아래 제공되는 공정 설명과 참고 자료를 우선 근거로 삼는다.");
        builder.AppendLine("- 자료에 없는 내용은 확실한 사실처럼 단정하지 않는다.");
        builder.AppendLine("- 모르면 \"그 부분은 내가 가진 자료만으로는 정확히 말하기 어려워.\" 라고 말한다.");
        builder.AppendLine();

        builder.AppendLine("[힌트 방식]");
        builder.AppendLine("- 정답을 바로 말하지 말고 스스로 알아차리도록 관찰할 지점을 짚어 준다.");
        builder.AppendLine("- 나쁜 예: \"부재를 오른쪽으로 30도 돌려.\"");
        builder.AppendLine("- 좋은 예: \"홈의 방향과 돌출된 부분이 서로 마주 보고 있는지 살펴볼래?\"");
        builder.AppendLine("- 각도, 치수, 좌표를 지정해 조작을 지시하지 않는다.");
        if (allowDirect)
        {
            builder.AppendLine("- 플레이어가 이미 여러 번 막혀 있다. 시각 가이드가 대신 표시되니 말로 세세히 지시하지 말 것.");
            builder.AppendLine("- 이런 때는 showGuide를 true로 하고, 말은 무엇을 왜 하는지 짚는 한두 문장으로 끝낸다.");
        }

        builder.AppendLine();

        builder.AppendLine("[시각 가이드]");
        builder.AppendLine("- 어디서 무엇을 다뤄야 하는지는 말이 아니라 화면의 시각 가이드가 알려 준다.");
        builder.AppendLine("- 플레이어가 위치나 방법을 보여 달라고 하거나, 말로 조작을 설명해야 할 상황이면 showGuide를 true로 한다.");
        builder.AppendLine("- showGuide를 true로 한 답은 원칙이나 격려를 담은 한두 문장으로 짧게 끝낸다.");
        builder.AppendLine("- 가이드를 표시해 주겠다고 예고하거나 표시했다고 말하지 않는다. 표시는 시스템이 한다.");
        builder.AppendLine();
        builder.AppendLine("[작업과 무관한 질문]");
        if (offTopicCount >= _config.Conversation.OffTopicToleranceCount)
        {
            builder.AppendLine("- 무관한 질문이 반복되고 있다. 한 문장으로만 받아 주고 바로 현재 공정으로 돌아오게 유도한다.");
        }
        else
        {
            builder.AppendLine("- 짧게 받아 준 뒤 자연스럽게 현재 공정으로 이어 간다. 불이익을 주거나 나무라지 않는다.");
        }

        if (_config.Llm.UseStructuredResponse)
        {
            builder.AppendLine();
            builder.AppendLine("[출력 형식]");
            builder.AppendLine("반드시 아래 JSON 한 개만 출력한다. 코드 블록이나 설명을 덧붙이지 않는다.");
            builder.AppendLine("{\"answer\":\"음성으로 읽을 한국어 문장\",\"topic\":\"process|related|offtopic\"," +
                               "\"grounded\":true,\"showGuide\":false}");
            builder.AppendLine("- topic: 현재 공정 관련이면 process, 목조건축 일반이면 related, 그 외에는 offtopic");
            builder.AppendLine("- grounded: 제공된 자료에 근거하면 true, 아니면 false");
            builder.AppendLine("- showGuide: 화면에 시각 가이드를 띄워야 하면 true, 아니면 false");
        }

        return builder.ToString();
    }

    string BuildGrounding(string question, AiProcessContext context)
    {
        var builder = new StringBuilder();

        if (context != null)
        {
            builder.AppendLine("[현재 상황]");
            builder.AppendLine($"- 공정: {context.ProcessLabel}");
            if (!string.IsNullOrWhiteSpace(context.StepName))
                builder.AppendLine($"- 단계: {context.StepName}");
            if (!string.IsNullOrWhiteSpace(context.StepDescription))
                builder.AppendLine($"- 지금 해야 할 일: {context.StepDescription}");
            if (context.FailureCount > 0)
                builder.AppendLine($"- 연속 실패 횟수: {context.FailureCount}");
            if (!string.IsNullOrWhiteSpace(context.LastFailureReason))
                builder.AppendLine($"- 직전 실패 원인: {context.LastFailureReason}");

            var process = _knowledge.GetProcess(context.Process);
            if (process != null)
            {
                if (!string.IsNullOrWhiteSpace(process.Goal))
                    builder.AppendLine($"- 공정 목적: {process.Goal}");
                AppendList(builder, "공정 요점", process.KeyPoints);
                AppendList(builder, "흔한 실수", process.CommonMistakes);
            }

            builder.AppendLine();
        }

        var entries = _knowledge.Search(question, context, MaxKnowledgeEntries);
        if (entries.Count > 0)
        {
            builder.AppendLine("[참고 자료]");
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                builder.Append("- ").Append(entry.Title).Append(": ").Append(entry.Summary);
                if (!string.IsNullOrWhiteSpace(entry.Source))
                    builder.Append(" (출처: ").Append(entry.Source).Append(')');
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    static void AppendList(StringBuilder builder, string label, List<string> values)
    {
        if (values == null || values.Count == 0) return;
        builder.Append("- ").Append(label).Append(": ");
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0) builder.Append(" / ");
            builder.Append(values[i]);
        }

        builder.AppendLine();
    }

    static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        value = value.Trim();
        return value.Length <= maxChars ? value : value.Substring(0, maxChars);
    }

    /// <summary>
    /// Reads the JSON envelope. A model that answers in plain text still works: the whole
    /// reply becomes the answer and is treated as ungrounded process talk.
    /// </summary>
    public static AiModelResponse ParseResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new AiModelResponse(null, AiAnswerTopic.Process, false);

        var text = StripCodeFence(content.Trim());
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            try
            {
                var json = JObject.Parse(text.Substring(start, end - start + 1));
                var answer = json.Value<string>("answer");
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    var topic = ParseTopic(json.Value<string>("topic"));
                    var grounded = json["grounded"]?.Type == JTokenType.Boolean
                        ? json.Value<bool>("grounded")
                        : true;

                    // 봉투에 없으면 false. 가이드는 켜는 쪽이 명시적이어야 한다.
                    var showGuide = json["showGuide"]?.Type == JTokenType.Boolean &&
                                    json.Value<bool>("showGuide");

                    return new AiModelResponse(answer.Trim(), topic, grounded, showGuide);
                }
            }
            catch (Exception)
            {
                // Malformed JSON is not worth a log line every turn; the plain-text path covers it.
            }
        }

        return new AiModelResponse(text, AiAnswerTopic.Process, false);
    }

    static AiAnswerTopic ParseTopic(string value) => value?.Trim().ToLowerInvariant() switch
    {
        "offtopic" => AiAnswerTopic.OffTopic,
        "related" => AiAnswerTopic.Related,
        _ => AiAnswerTopic.Process
    };

    static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;

        var firstBreak = text.IndexOf('\n');
        if (firstBreak < 0) return text;

        var body = text.Substring(firstBreak + 1);
        var fenceEnd = body.LastIndexOf("```", StringComparison.Ordinal);
        return fenceEnd >= 0 ? body.Substring(0, fenceEnd).Trim() : body.Trim();
    }
}
