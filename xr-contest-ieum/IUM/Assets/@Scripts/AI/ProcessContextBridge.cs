using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Play 씬의 실행 중 러너를 이음이의 현재 공정 데이터로 잇는다 (F-012 2.3).
///
/// <see cref="AiProcessContextRegistry"/>의 프로바이더 자리(IAiProcessContextProvider)가 이 인수인계를
/// 위해 비워져 있었다. 이 컴포넌트가 그 자리를 차지하면 디버그 스위처가 쓰던 정적 폴백 경로는
/// 자동으로 물러나고, 개발 씬(AiVoiceTest)처럼 프로바이더가 없는 곳에서는 종전대로 폴백이 돈다.
///
/// 실행기는 <see cref="QuestManager"/> 하나로 통일됐다. 튜토리얼과 제작 공정 다섯이 모두
/// <c>quest.json</c>의 그래프를 타므로 퀘스트 경로가 기본이고, 남아 있는
/// <see cref="ProcessRunner"/>는 <c>process.json</c>으로만 도는 옛 씬을 위한 레거시 폴백이다.
/// 둘 다 쉬고 있으면 null을 돌려주어 저장 기반 폴백에 맡긴다.
///
/// 컨텍스트가 담는 것 (프롬프트 도달 지점은 <see cref="AiPromptBuilder"/>):
/// <list type="bullet">
/// <item>StepName → "- 단계:" 줄. 목표 문구(goal) 그대로. 월드 HUD도 같은 값을 읽는다.</item>
/// <item>StepDescription → "- 지금 해야 할 일:" 줄. 공정 요약 · 목표 위치 · 수량 · 조작을 한 줄로 묶는다.</item>
/// <item>FocusKeywords → <see cref="AiKnowledgeBase.Search"/>의 가점. 자료 항목의 제목·키워드와
/// 겹쳐야 점수가 붙으므로 <c>ai_knowledge.json</c>에 실재하는 용어만 넣는다.</item>
/// </list>
///
/// 조작 힌트를 컨텍스트에 넣는 근거 (F-012 2.4): 05 문서가 막는 것은 "정답을 바로 말하기"이고
/// 프롬프트도 각도·치수·좌표 지시를 금한다. 조작키와 도구 쓰는 법은 그 금지 대상이 아니라
/// 프롬프트가 명시한 '할 수 있는 것'(도구 사용법 설명)에 속한다. 게이팅 판단은 프롬프트 계층이
/// <see cref="AiProcessContext.AllowDirectAnswer"/>로 하므로 여기서는 사실만 전달한다.
///
/// 실패 계상 (F-012 2.4): 공정 시스템에는 명시적 실패 판정이 없다. 대신 10초 무진전 재안내
/// (<see cref="QuestManager.RetryHinted"/>)를 실패 1회로 계상한다 — "막혀 있음"의 판정이 이미
/// 그 시점이기 때문이다. 세 번 쌓이면 <see cref="AiProcessContext.AllowDirectAnswer"/>가 켜져
/// 이음이가 원칙 설명 대신 직접 조작을 안내한다. 단계가 바뀌면 계상을 되돌린다.
/// </summary>
public sealed class ProcessContextBridge : MonoBehaviour, IAiProcessContextProvider
{
    /// <summary>공정이 무엇을 하는 자리인지 한 줄로. 기획 원문(대사·03 문서)의 취지 요약이다.</summary>
    static readonly Dictionary<ProcessId, string> ProcessSummaries = new()
    {
        [ProcessId.Tutorial] = "조작 튜토리얼: 본 작업에 앞서 보기·이동·방향 전환·가리키기·잡기·놓기·이음이 부르기를 예행연습한다.",
        [ProcessId.ShortPart] =
            "짧은 부재 가공: 한 부재를 먹매김 → 톱질 → 대패질 → 끌질 순으로 끝까지 다듬는다. " +
            "먹통·톱·평대패·끌과 망치를 처음 쥐는 단계다.",
        [ProcessId.MediumPart] =
            "중간 부재 가공: 같은 순서에 자귀질과 배대패질이 더해진다. 자귀로 굵게 쳐내고, 평대패로 면을 잡은 뒤 " +
            "배대패로 곡면을 다듬고 끌로 결구 홈을 판다.",
        [ProcessId.LongPart] =
            "긴 부재 가공: 배운 도구를 모두 써서 가장 큰 부재를 혼자 완성한다. 새로 배우는 도구는 없고 " +
            "자귀·대패·끌의 작업량이 크게 늘어난다.",
        [ProcessId.GongpoPuzzle] =
            "조립: 가공한 도리를 기둥 위 제자리에 걸어 결구를 맞물린 뒤, 공포 서른일곱 개 부재를 아래에서 위로 " +
            "정해진 순서와 방향으로 짜 올려 지붕의 하중을 나눈다.",
        [ProcessId.Prologue] = "프롤로그: 숭례문 화재와 기술 계승이라는 배경을 보는 구간이다.",
        [ProcessId.Ending] = "엔딩: 완성된 결과를 돌아보는 구간이다."
    };

    /// <summary>
    /// 수량 목표에서 셀 대상의 이름. 진행을 "자리 2/3"처럼 읽히게 한다.
    ///
    /// 공정이 부재 단위가 되면서 한 공정 안에 먹선·절단·대패면·홈이 섞이므로, 단위는 공정이 아니라
    /// 그 목표의 신호 키로 정한다. 키를 모르면 "자리"로 부른다 — 어떤 작업이든 존 하나가 한 자리다.
    /// </summary>
    static readonly Dictionary<string, string> SignalCountUnits = new(StringComparer.Ordinal)
    {
        [PartProcessSignals.ShortInk] = "먹선",
        [PartProcessSignals.MediumInk] = "먹선",
        [PartProcessSignals.LongInk] = "먹선",
        [PartProcessSignals.ShortSaw] = "절단 자리",
        [PartProcessSignals.MediumSaw] = "절단 자리",
        [PartProcessSignals.LongSaw] = "절단 자리",
        [PartProcessSignals.MediumAdze] = "자귀 자리",
        [PartProcessSignals.LongAdze] = "자귀 자리",
        [PartProcessSignals.ShortPlane] = "대패 면",
        [PartProcessSignals.MediumPlane] = "대패 면",
        [PartProcessSignals.LongPlane] = "대패 면",
        [PartProcessSignals.MediumCurved] = "곡면",
        [PartProcessSignals.ShortChisel] = "홈",
        [PartProcessSignals.MediumChisel] = "홈",
        [PartProcessSignals.LongChisel] = "홈",
        [PartProcessSignals.PurlinInstall] = "부재",
        [PartProcessSignals.GongpoAssembled] = "부재"
    };

    /// <summary>
    /// 자료 검색 가점용 키워드. <c>ai_knowledge.json</c>의 항목 제목·키워드와 겹칠 때만 점수가 되므로
    /// 그 파일에 실재하는 용어만 골랐다. 없는 말을 넣어도 오류는 없지만 아무 일도 하지 않는다.
    /// </summary>
    static readonly Dictionary<ProcessId, string[]> ProcessFocusKeywords = new()
    {
        [ProcessId.Tutorial] = new[] { "숭례문", "복원", "전승" },
        [ProcessId.ShortPart] = new[] { "먹매김", "먹줄", "먹선", "기준선", "먹통", "톱질", "톱", "끌질", "끌", "망치", "홈" },
        [ProcessId.MediumPart] = new[] { "먹매김", "먹선", "톱질", "자귀", "대패", "곡면", "끌질", "홈", "결구" },
        [ProcessId.LongPart] = new[] { "먹매김", "먹선", "톱질", "자귀", "대패", "끌질", "홈", "결구", "나뭇결" },
        [ProcessId.GongpoPuzzle] = new[] { "도리", "맞춤", "결구", "장부", "수평", "공포", "짜임", "순서", "하중", "이음" }
    };

    [Tooltip("비우면 씬에서 찾는다. 씬 빌더가 프리팹에 함께 넣는다.")]
    [SerializeField] ProcessRunner runner;

    [Tooltip("비우면 씬에서 찾는다. 튜토리얼 이식(TutorialImport) 전에는 없어도 된다.")]
    [SerializeField] QuestManager quest;

    // 매 질문마다 새로 만들지 않고 하나를 고쳐 쓴다. 소비 측(AiConversationManager)이 필요하면
    // Clone으로 스냅숏을 뜬다.
    readonly AiProcessContext _context = new();

    // 재사용 버퍼. StepDescription은 질문마다 다시 조립되지만 진행 수치 말고는 거의 그대로다.
    readonly StringBuilder _description = new();

    int _failureCount;
    string _failureReason;
    bool _focusApplied;
    ProcessId _focusProcess;

    void Awake()
    {
        if (runner == null)
            runner = FindAnyObjectByType<ProcessRunner>(FindObjectsInactive.Include);
        if (quest == null)
            quest = FindAnyObjectByType<QuestManager>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        if (runner != null)
        {
            runner.ProcessChanged += OnProcessChanged;
            runner.StepChanged += OnStepChanged;
            runner.RetryHinted += OnRetryHinted;
        }

        if (quest != null)
        {
            quest.QuestChanged += OnQuestChanged;
            quest.ObjectiveChanged += OnObjectiveChanged;
            quest.RetryHinted += OnRetryHinted;
        }

        AiProcessContextRegistry.SetProvider(this);
    }

    void OnDisable()
    {
        if (runner != null)
        {
            runner.ProcessChanged -= OnProcessChanged;
            runner.StepChanged -= OnStepChanged;
            runner.RetryHinted -= OnRetryHinted;
        }

        if (quest != null)
        {
            quest.QuestChanged -= OnQuestChanged;
            quest.ObjectiveChanged -= OnObjectiveChanged;
            quest.RetryHinted -= OnRetryHinted;
        }

        // 씬 전환에서 다음 씬의 프로바이더가 먼저 등록됐을 수 있으므로 자기 등록만 지운다.
        AiProcessContextRegistry.ClearProvider(this);
    }

    public AiProcessContext GetContext()
    {
        // 퀘스트가 우선. 실행기가 QuestManager 하나로 통일된 뒤로 튜토리얼과 제작 공정 모두
        // 이쪽을 탄다. 순서를 정해 두면 옛 러너가 남은 씬에서 잘못 공존해도 답이 흔들리지 않는다.
        if (quest != null && quest.IsRunning)
            return FillFromQuest();

        if (runner != null && runner.IsRunning)
            return FillFromRunner();

        return null;
    }

    AiProcessContext FillFromQuest()
    {
        var step = quest.CurrentObjective;
        _context.Process = quest.Process;
        _context.StepName = step?.Goal;
        _context.StepDescription = BuildQuestDescription(step);
        return Finish(quest.Process);
    }

    /// <summary>
    /// <c>process.json</c>만으로 도는 옛 씬용 폴백. 단계 데이터에 조작 힌트가 없고
    /// (ControlHint는 퀘스트 노드에만 있다) 진행률도 공개돼 있지 않아 공정 요약과 단계 위치까지만 싣는다.
    /// </summary>
    AiProcessContext FillFromRunner()
    {
        var step = runner.CurrentStep;
        _context.Process = runner.Process;
        _context.StepName = step?.Goal;

        _description.Clear();
        AppendSummary(_description, runner.Process);
        if (runner.StepCount > 0)
        {
            Separate(_description);
            _description.Append($"단계 {runner.StepIndex + 1}/{runner.StepCount}");
            if (!string.IsNullOrWhiteSpace(step?.Goal)) _description.Append($": {step.Goal}");
        }

        _context.StepDescription = _description.Length > 0 ? _description.ToString() : null;
        return Finish(runner.Process);
    }

    /// <summary>
    /// 공정 한 줄 설명 + 목표 위치와 문구 + 수량 맥락 + 조작 방법을 한 줄로 묶는다.
    /// 프롬프트가 이 값을 "- 지금 해야 할 일:" 한 줄에 그대로 붙이므로 줄바꿈 대신 ' / '로 나눈다.
    /// </summary>
    string BuildQuestDescription(ProcessStepData step)
    {
        _description.Clear();
        AppendSummary(_description, quest.Process);

        // 목표 위치는 goal과 함께 붙인다. StepName과 한 문장 겹치지만, 몇 번째 목표인지가
        // 붙어야 "지금 어디쯤"이라는 질문에 답할 수 있다.
        if (quest.ObjectiveCount > 0 && quest.ObjectiveNumber > 0)
        {
            Separate(_description);
            _description.Append($"목표 {quest.ObjectiveNumber}/{quest.ObjectiveCount}");
            if (!string.IsNullOrWhiteSpace(step?.Goal)) _description.Append($": {step.Goal}");
        }
        else if (!string.IsNullOrWhiteSpace(step?.Goal))
        {
            Separate(_description);
            _description.Append($"지금 목표: {step.Goal}");
        }

        AppendSignalProgress(_description, step);

        // 이음이가 안내하는 조작도 지금 쓰는 장치의 것이어야 한다. HUD와 같은 해석을 쓴다.
        var hint = quest.CurrentNode?.CurrentControlHint;
        if (!string.IsNullOrWhiteSpace(hint))
        {
            Separate(_description);
            _description.Append($"조작: {hint.Trim()}");
        }

        return _description.Length > 0 ? _description.ToString() : null;
    }

    /// <summary>
    /// 신호 목표의 진행을 적는다. 신호 키가 목표마다 갈린 뒤로 <c>amount</c>는 대체로 그 부재의 존
    /// 개수 그대로다. 예외는 공포 조립(1→18→37) 하나로, 세 목표가 한 키를 나눠 써서 그때만
    /// <see cref="ProcessSignalBus"/> 누계가 분모가 된다. 진행률은 Ratio(값, amount)라서 되곱하면
    /// 어느 쪽이든 현재 값이 나온다.
    /// </summary>
    void AppendSignalProgress(StringBuilder builder, ProcessStepData step)
    {
        if (step == null || step.Condition != StepCondition.Signal) return;

        var total = Mathf.RoundToInt(step.Amount);
        if (total < 2) return;

        var done = Mathf.Clamp(Mathf.RoundToInt(quest.ObjectiveProgress * total), 0, total);
        var unit = step.Target != null && SignalCountUnits.TryGetValue(step.Target, out var label)
            ? label
            : "자리";
        Separate(builder);
        builder.Append($"{unit} {done}/{total}");
    }

    static void AppendSummary(StringBuilder builder, ProcessId process)
    {
        if (ProcessSummaries.TryGetValue(process, out var summary)) builder.Append(summary);
    }

    static void Separate(StringBuilder builder)
    {
        if (builder.Length > 0) builder.Append(" / ");
    }

    AiProcessContext Finish(ProcessId process)
    {
        ApplyFocusKeywords(process);
        _context.LastFailureReason = _failureReason;
        _context.FailureCount = _failureCount;
        return _context;
    }

    void ApplyFocusKeywords(ProcessId process)
    {
        if (_focusApplied && _focusProcess == process) return;

        _context.FocusKeywords.Clear();
        if (ProcessFocusKeywords.TryGetValue(process, out var keywords))
            _context.FocusKeywords.AddRange(keywords);

        _focusApplied = true;
        _focusProcess = process;
    }

    void OnRetryHinted(ProcessStepData step)
    {
        _failureCount++;
        _failureReason = string.IsNullOrWhiteSpace(step?.Goal)
            ? "현재 단계에서 진전이 없어 재안내를 받음"
            : $"'{step.Goal}' 단계에서 진전이 없어 재안내를 받음";
        AiProcessContextRegistry.NotifyChanged();
    }

    void OnProcessChanged(ProcessId _) => ResetFailures();

    void OnStepChanged(ProcessStepData _, int __) => ResetFailures();

    void OnQuestChanged(QuestDefinition _) => ResetFailures();

    void OnObjectiveChanged(QuestNodeData _, int __) => ResetFailures();

    void ResetFailures()
    {
        _failureCount = 0;
        _failureReason = null;
        AiProcessContextRegistry.NotifyChanged();
    }
}
