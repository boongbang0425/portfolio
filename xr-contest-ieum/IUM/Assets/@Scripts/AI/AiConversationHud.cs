using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// PTT 상태 표시 (F-013 3.3) plus the selection list shown after repeated recognition failures.
/// This is a screen-overlay UIDocument so the whole flow is verifiable without an HMD; the
/// world-space VR HUD of 5단계 can replace the view without touching the conversation layer.
///
/// 이음이 답변 자막은 씬에 <see cref="SubtitleView"/>가 있으면 그쪽으로 위임해 노장 대사 자막과
/// 같은 뷰·스타일(하단, 이음이 색)로 표시한다. 이때 답변은 문장 단위로 쪼개 TTS 재생 진행률에
/// 맞춰 페이지를 넘긴다 — 대사 자막이 줄 단위로 표시되는 것과 체감을 맞추기 위해서다. 뷰가 없는
/// 씬(AiVoiceTest 등)에서는 종전대로 이 HUD의 ai-subtitle 요소가 폴백으로 표시한다.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class AiConversationHud : MonoBehaviour
{
    /// <summary>
    /// 문장 페이징까지 끝난 이음이 답변 자막 한 페이지. null 또는 공백이면 숨김이다.
    /// <see cref="WorldSubtitleDirector"/>가 월드 말풍선으로 그리려고 구독한다.
    ///
    /// 원본인 <c>AiConversationManager.SubtitleChanged</c>가 아니라 여기서 알리는 이유는 문장
    /// 분할과 페이지 넘김 규칙이 이 HUD에만 있기 때문이다. 구독자가 원본을 받으면 같은 규칙을
    /// 다시 구현해야 하고, 두 벌이 되는 순간 화면 자막과 말풍선이 다른 문장을 보여 준다.
    /// </summary>
    public static event System.Action<string> PageDisplayed;

    /// <summary>도메인 리로드를 끈 채 플레이에 들어가면 이전 세션의 구독이 남는다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => PageDisplayed = null;

    [Tooltip("Typed-question row. Keep it on for desktop testing, off for builds.")]
    [SerializeField] bool showDebugPanel = true;

    UIDocument _document;
    Label _stateIcon;
    Label _stateLabel;
    Label _transcript;
    Label _subtitle;
    Label _serviceLabel;
    VisualElement _levelTrack;
    VisualElement _levelFill;
    VisualElement _suggestions;
    VisualElement _debugPanel;
    TextField _questionField;
    Button _askButton;

    AiConversationManager _conversation;

    // 대사 자막 뷰 공유 (같은 씬의 SubtitleView). 못 찾으면 ai-subtitle 폴백을 쓴다.
    SubtitleView _subtitleView;
    bool _subtitleViewSearched;

    // 문장 페이지. 항목이 2개 이상일 때만 Update가 TTS 진행률로 페이지를 넘긴다.
    string[] _pages;
    float[] _pageEnds;
    int _pageIndex;

    void Awake() => _document = GetComponent<UIDocument>();

    void OnEnable()
    {
        if (_document?.visualTreeAsset == null) return;

        var root = _document.rootVisualElement;
        _stateIcon = root.Q<Label>("ai-state-icon");
        _stateLabel = root.Q<Label>("ai-state-label");
        _transcript = root.Q<Label>("ai-transcript");
        _subtitle = root.Q<Label>("ai-subtitle");
        _serviceLabel = root.Q<Label>("ai-service-label");
        _levelTrack = root.Q<VisualElement>("ai-level-track");
        _levelFill = root.Q<VisualElement>("ai-level-fill");
        _suggestions = root.Q<VisualElement>("ai-suggestions");
        _debugPanel = root.Q<VisualElement>("ai-debug");
        _questionField = root.Q<TextField>("ai-question-field");
        _askButton = root.Q<Button>("ai-ask-button");

        if (_stateLabel == null || _subtitle == null || _suggestions == null)
        {
            Debug.LogError("[AI HUD] Required elements are missing from IeumiHud.uxml.");
            return;
        }

        if (_debugPanel != null)
            _debugPanel.style.display = showDebugPanel ? DisplayStyle.Flex : DisplayStyle.None;

        if (_askButton != null) _askButton.clicked += AskTypedQuestion;
        _questionField?.RegisterCallback<KeyDownEvent>(OnQuestionFieldKeyDown);
        _questionField?.RegisterCallback<FocusInEvent>(OnQuestionFieldFocusIn);
        _questionField?.RegisterCallback<FocusOutEvent>(OnQuestionFieldFocusOut);

        // 자막 표시 경로가 바뀌면 ai-subtitle 폴백을 지금 뜬 답변에 바로 반영해야 한다.
        // 그러지 않으면 다음 페이지가 올 때까지 이전 경로의 표시가 그대로 남는다.
        WorldSubtitleDirector.DisplayModeChanged += OnSubtitleDisplayModeChanged;

        _conversation = AiConversationManager.Instance;
        if (_conversation == null) return;

        _conversation.StateChanged += OnStateChanged;
        _conversation.TranscriptChanged += OnTranscriptChanged;
        _conversation.SubtitleChanged += OnSubtitleChanged;
        _conversation.SuggestionsChanged += OnSuggestionsChanged;
        _conversation.Ready += OnReady;

        OnStateChanged(_conversation.State);
        OnTranscriptChanged(_conversation.Transcript);
        OnSubtitleChanged(_conversation.Subtitle);
        OnSuggestionsChanged();
        OnReady();
    }

    void OnDisable()
    {
        WorldSubtitleDirector.DisplayModeChanged -= OnSubtitleDisplayModeChanged;

        if (_askButton != null) _askButton.clicked -= AskTypedQuestion;
        _questionField?.UnregisterCallback<KeyDownEvent>(OnQuestionFieldKeyDown);
        _questionField?.UnregisterCallback<FocusInEvent>(OnQuestionFieldFocusIn);
        _questionField?.UnregisterCallback<FocusOutEvent>(OnQuestionFieldFocusOut);

        // 구독이 끊기면 이후의 숨김 이벤트를 못 받으므로, 위임한 자막을 지금 내린다.
        _pages = null;
        DisplaySubtitle(null);

        if (_conversation != null) _conversation.SuppressPushToTalk = false;
        if (_conversation == null) return;
        _conversation.StateChanged -= OnStateChanged;
        _conversation.TranscriptChanged -= OnTranscriptChanged;
        _conversation.SubtitleChanged -= OnSubtitleChanged;
        _conversation.SuggestionsChanged -= OnSuggestionsChanged;
        _conversation.Ready -= OnReady;
        _conversation = null;
    }

    void Update()
    {
        if (_conversation == null) return;

        // Width is a percentage so the meter follows the track without a layout query.
        if (_levelFill != null && _conversation.State == AiConversationState.Listening)
            _levelFill.style.width = new StyleLength(Length.Percent(Mathf.Clamp01(_conversation.MicrophoneLevel) * 100f));

        TickSubtitlePaging();
    }

    void OnReady()
    {
        if (_serviceLabel == null || _conversation == null) return;
        var offline = _conversation.IsOffline ? " · 오프라인" : string.Empty;
        _serviceLabel.text = _conversation.ServiceSummary + offline;
    }

    void OnStateChanged(AiConversationState state)
    {
        if (_stateLabel == null) return;

        _stateLabel.text = state switch
        {
            AiConversationState.Idle => "질문 가능",
            AiConversationState.Listening => "듣는 중",
            AiConversationState.Transcribing => "변환 중",
            AiConversationState.Thinking => "생각 중",
            AiConversationState.Speaking => "답변 중",
            _ => "사용 불가"
        };

        SetIconModifier(state);
        SetVisible(_levelTrack, "level-track--visible", state == AiConversationState.Listening);
        if (state == AiConversationState.Listening && _levelFill != null)
            _levelFill.style.width = new StyleLength(Length.Percent(0f));

        OnReady();
    }

    void SetIconModifier(AiConversationState state)
    {
        if (_stateIcon == null) return;

        _stateIcon.RemoveFromClassList("state-icon--idle");
        _stateIcon.RemoveFromClassList("state-icon--listening");
        _stateIcon.RemoveFromClassList("state-icon--busy");
        _stateIcon.RemoveFromClassList("state-icon--speaking");

        var modifier = state switch
        {
            AiConversationState.Idle => "state-icon--idle",
            AiConversationState.Listening => "state-icon--listening",
            AiConversationState.Transcribing => "state-icon--busy",
            AiConversationState.Thinking => "state-icon--busy",
            AiConversationState.Speaking => "state-icon--speaking",
            _ => null
        };

        if (modifier != null) _stateIcon.AddToClassList(modifier);
    }

    void OnTranscriptChanged(string value)
    {
        if (_transcript == null) return;
        _transcript.text = string.IsNullOrWhiteSpace(value) ? string.Empty : $"\"{value}\"";
        SetVisible(_transcript, "transcript--visible", !string.IsNullOrWhiteSpace(value));
    }

    void OnSubtitleChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _pages = null;
            DisplaySubtitle(null);
            return;
        }

        _pages = SplitSentences(value, out _pageEnds);
        _pageIndex = 0;
        DisplaySubtitle(_pages[0]);
    }

    /// <summary>
    /// TTS 재생 진행률(글자 수 비례)에 맞춰 문장 페이지를 넘긴다. 재생 전 합성 대기 구간에는
    /// 첫 문장이 떠 있고, 재생이 끝나면 마지막 문장이 자막 꼬리 시간 동안 유지된다.
    /// </summary>
    void TickSubtitlePaging()
    {
        if (_pages == null || _pages.Length < 2) return;

        var voice = AiVoicePlayer.Current;
        if (voice == null || !voice.IsSpeaking || voice.Duration <= 0f) return;

        var progress = voice.Elapsed / voice.Duration;
        var index = _pageIndex;
        while (index < _pages.Length - 1 && progress >= _pageEnds[index]) index++;
        if (index == _pageIndex) return;

        _pageIndex = index;
        DisplaySubtitle(_pages[index]);
    }

    /// <summary>
    /// 답변 자막 한 페이지를 표시하거나(null이면 숨김), 대사 자막 뷰가 있으면 그쪽으로 위임한다.
    /// 위임 시 ai-subtitle 요소는 계속 숨겨 두어 자막이 두 곳에 뜨지 않게 한다.
    /// </summary>
    /// <summary>표시 경로가 바뀌면 지금 떠 있는 페이지를 다시 적용한다.</summary>
    void OnSubtitleDisplayModeChanged(SubtitleDisplayMode mode) =>
        DisplaySubtitle(_pages != null && _pageIndex >= 0 && _pageIndex < _pages.Length
            ? _pages[_pageIndex]
            : null);

    void DisplaySubtitle(string text)
    {
        // 월드 말풍선이 먼저다. VR에서는 아래 두 경로가 모두 화면 공간이라 렌더되지 않는다.
        // 페이징이 끝난 뒤 여기서 알리는 이유는 문장 분할 규칙을 두 벌 두지 않기 위해서다.
        PageDisplayed?.Invoke(text);

        var view = SharedSubtitleView;
        var routed = view != null && view.IsReady;
        if (routed) view.SetAiSubtitle(text);

        if (_subtitle == null) return;

        // 말풍선만 쓰는 동안에는 자기 라벨도 내린다. 그러지 않으면 SubtitleView가 없는 씬에서
        // 데스크톱 자막이 이중으로 뜬다.
        var ownText = routed || !WorldSubtitleDirector.ScreenSubtitlesEnabled ? null : text;
        _subtitle.text = ownText ?? string.Empty;
        SetVisible(_subtitle, "subtitle--visible", !string.IsNullOrWhiteSpace(ownText));
    }

    /// <summary>
    /// 같은 씬의 대사 자막 뷰. 한 번만 찾는다 — HUD와 SubtitleView는 같은 씬에서 생성·파괴되므로
    /// 씬 도중에 새로 생기는 경우는 없다.
    /// </summary>
    SubtitleView SharedSubtitleView
    {
        get
        {
            if (_subtitleView != null || _subtitleViewSearched) return _subtitleView;
            _subtitleViewSearched = true;
            _subtitleView = FindAnyObjectByType<SubtitleView>(FindObjectsInactive.Include);
            return _subtitleView;
        }
    }

    /// <summary>
    /// 문장 부호 기준으로 답변을 페이지로 쪼개고, 페이지별 누적 글자 비율(재생 진행률 기준 전환
    /// 시점)을 계산한다. 소수점(3.5)은 문장 끝으로 보지 않고, 연속 부호(?!, ...)는 묶는다.
    /// </summary>
    static string[] SplitSentences(string value, out float[] pageEnds)
    {
        var pages = new List<string>();
        var start = 0;

        for (var i = 0; i < value.Length; i++)
        {
            if (!IsSentenceEnd(value[i])) continue;
            if (value[i] == '.' && i + 1 < value.Length && char.IsDigit(value[i + 1])) continue;

            while (i + 1 < value.Length && IsSentenceEnd(value[i + 1])) i++;

            var page = value.Substring(start, i - start + 1).Trim();
            if (page.Length > 0) pages.Add(page);
            start = i + 1;
        }

        var tail = value.Substring(start).Trim();
        if (tail.Length > 0) pages.Add(tail);
        if (pages.Count == 0) pages.Add(value.Trim());

        var total = 0f;
        for (var i = 0; i < pages.Count; i++) total += pages[i].Length;

        pageEnds = new float[pages.Count];
        var accumulated = 0f;
        for (var i = 0; i < pages.Count; i++)
        {
            accumulated += pages[i].Length;
            pageEnds[i] = accumulated / Mathf.Max(1f, total);
        }

        return pages.ToArray();
    }

    static bool IsSentenceEnd(char c) => c is '.' or '!' or '?' or '…' or '。';

    void OnSuggestionsChanged()
    {
        if (_suggestions == null || _conversation == null) return;

        _suggestions.Clear();
        var questions = _conversation.Suggestions;

        for (var i = 0; i < questions.Count; i++)
        {
            var suggestion = questions[i];
            if (string.IsNullOrWhiteSpace(suggestion?.Question)) continue;

            var button = new Button(() => _conversation.AskSuggestion(suggestion))
            {
                text = suggestion.Question
            };
            button.AddToClassList("suggestion-button");
            _suggestions.Add(button);
        }

        SetVisible(_suggestions, "suggestions--visible", _suggestions.childCount > 0);
    }

    // The PTT key is read straight from the keyboard, so typing must disable it.
    void OnQuestionFieldFocusIn(FocusInEvent evt)
    {
        if (_conversation != null) _conversation.SuppressPushToTalk = true;
    }

    void OnQuestionFieldFocusOut(FocusOutEvent evt)
    {
        if (_conversation != null) _conversation.SuppressPushToTalk = false;
    }

    void OnQuestionFieldKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
        AskTypedQuestion();
        evt.StopPropagation();
    }

    void AskTypedQuestion()
    {
        if (_conversation == null || _questionField == null) return;

        var question = _questionField.value;
        if (string.IsNullOrWhiteSpace(question)) return;

        _questionField.SetValueWithoutNotify(string.Empty);
        _conversation.Ask(question);
    }

    static void SetVisible(VisualElement element, string modifier, bool visible)
    {
        if (element == null) return;
        if (visible) element.AddToClassList(modifier);
        else element.RemoveFromClassList(modifier);
    }
}
