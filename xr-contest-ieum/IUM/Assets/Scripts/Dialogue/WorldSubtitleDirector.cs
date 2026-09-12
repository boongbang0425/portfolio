using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>자막을 어디에 그릴지. 기본은 말풍선만이다.</summary>
public enum SubtitleDisplayMode
{
    /// <summary>월드 말풍선만. 화면 오버레이 자막은 숨긴다.</summary>
    BubbleOnly,

    /// <summary>화면 오버레이 자막만. VR에서는 아무것도 보이지 않으므로 데스크톱 비교용이다.</summary>
    ScreenOnly,

    /// <summary>둘 다. 말풍선 위치를 화면 자막과 대조할 때만 쓴다.</summary>
    Both
}

/// <summary>
/// 자막을 화자 머리 위 월드 말풍선으로 옮기는 매니저 (F-018 1.4).
///
/// VR에서 화면 공간(ScreenSpaceOverlay) UI가 전혀 렌더되지 않아 <c>SubtitleView</c>의 자막이
/// HMD에서 보이지 않는 것이 출발점이다. 대사 계층은 손대지 않는다 — <c>DialoguePlayer</c>가 내던
/// <c>SubtitleChanged</c>를 그대로 받아 표시 방법만 바꾼다. 화면 자막은 지우지 않고 남겨 두되
/// <see cref="DisplayMode"/>로 끈다(기본 <see cref="SubtitleDisplayMode.BubbleOnly"/>).
///
/// 씬과 프리팹을 건드리지 않는다. <see cref="WorldSubtitleBootstrap"/>이 진입점에서 이 매니저를
/// 세우고, 말풍선 계층은 <see cref="WorldSubtitleBubble"/>이 코드로 만든다.
///
/// 채널 우선순위는 <c>SubtitleView</c>와 같다: 대사·컷씬 자막이 이음이 AI 답변 자막보다 항상
/// 앞선다. 두 뷰가 서로 다른 순서로 자막을 고르면 데스크톱과 VR에서 보이는 것이 달라진다.
/// </summary>
public sealed class WorldSubtitleDirector : Singleton<WorldSubtitleDirector>
{
    [Tooltip("자막 표시 경로. 실행 중에 바꿔도 즉시 반영된다.")]
    [SerializeField] SubtitleDisplayMode displayMode = SubtitleDisplayMode.BubbleOnly;

    static SubtitleDisplayMode _mode = SubtitleDisplayMode.BubbleOnly;

    WorldSubtitleBubble _bubble;

    NojangBehaviour _nojang;
    EeumBehaviour _ieumi;

    InGameDialogue _dialogue;
    CutsceneDirector _cutscene;
    AiConversationManager _conversation;

    /// <summary>HUD가 페이지를 마지막으로 보낸 프레임. 같은 프레임의 원본 자막은 버린다.</summary>
    int _lastPagedFrame = -1;

    /// <summary>페이지가 오지 않으면 다음 LateUpdate에 쓸 원본 자막.</summary>
    string _pendingAiRaw;
    bool _hasPendingAiRaw;

    /// <summary>보류 중인 AI 자막. 대사 자막이 내려가면 아직 유효할 때만 표시된다.</summary>
    string _aiText;

    /// <summary>대사·컷씬 자막이 떠 있는 동안 참. AI 채널은 그동안 표시를 미룬다.</summary>
    bool _dialogueActive;

    /// <summary>현재 대사 자막이 컷씬에서 왔는지. 컷씬은 화자 모델을 신뢰할 수 없어 폴백으로 간다.</summary>
    bool _fromCutscene;

    DialogueSpeaker _speaker;

    /// <summary>표시 중인 대사 문구. 표시 경로가 바뀌었을 때 다시 그리기 위해 들고 있는다.</summary>
    string _dialogueText;

    /// <summary>매니저가 실제로 설치됐는지. 설치 전에는 화면 자막을 끄면 안 된다.</summary>
    public static bool Installed { get; private set; }

    /// <summary>화면 오버레이 자막을 그려도 되는지. <c>SubtitleView</c>와 AI HUD가 이 값을 본다.</summary>
    public static bool ScreenSubtitlesEnabled => !Installed || _mode != SubtitleDisplayMode.BubbleOnly;

    /// <summary>월드 말풍선을 그려야 하는지.</summary>
    public static bool BubblesEnabled => _mode != SubtitleDisplayMode.ScreenOnly;

    public static event Action<SubtitleDisplayMode> DisplayModeChanged;

    public static SubtitleDisplayMode DisplayMode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;

            if (HasInstance)
            {
                Instance.displayMode = value;
                Instance.ReapplyBubble();
            }

            DisplayModeChanged?.Invoke(value);
        }
    }

    /// <summary>도메인 리로드를 끈 채 플레이에 들어가면 이전 세션의 값과 구독이 남는다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        _mode = SubtitleDisplayMode.BubbleOnly;
        Installed = false;
        DisplayModeChanged = null;
    }

    protected override void Awake()
    {
        base.Awake();
        if (!ReferenceEquals(Instance, this)) return;

        _mode = displayMode;
        Installed = true;

        _bubble = WorldSubtitleBubble.Create(transform, "SubtitleBubble");

        SceneManager.sceneLoaded += OnSceneLoaded;
        AiConversationHud.PageDisplayed += OnAiPage;

        Bind();

        // 화면 자막 쪽이 이미 켜져 있을 수 있다. 설치 사실을 알려 표시를 정리하게 한다.
        DisplayModeChanged?.Invoke(_mode);
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        AiConversationHud.PageDisplayed -= OnAiPage;
        Unbind();

        if (ReferenceEquals(Instance, this)) Installed = false;
        base.OnDestroy();
    }

    void Update()
    {
        // 중복 인스턴스는 Singleton.Awake가 Destroy를 걸어 두었지만 실제 파괴는 프레임 끝이라
        // Update가 한 번 돈다. 그 인스턴스의 인스펙터 값이 정품의 설정을 덮어쓰면 안 된다.
        if (!ReferenceEquals(Instance, this)) return;

        // 인스펙터에서 값을 바꾸면 반영한다. enum 비교 하나라 비용은 없다시피 하다.
        if (displayMode != _mode) DisplayMode = displayMode;
    }

    // ---- 배선 ----

    /// <summary>
    /// 씬이 바뀌면 화자와 자막 소스를 다시 잡는다. <c>InGameDialogue</c>·<c>CutsceneDirector</c>는
    /// DontDestroyOnLoad 싱글턴이라 보통 그대로지만, 그것들이 없는 씬에서 시작해 나중에 생기는
    /// 경우가 있어 매번 확인한다. NPC는 씬마다 다른 개체이므로 반드시 다시 찾아야 한다.
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        _nojang = null;
        _ieumi = null;

        // 새 씬이 프로젝트 서체를 물고 왔을 수 있다. 이미 잡았으면 이 호출은 그냥 지나간다.
        DialogueBubbleFont.Invalidate();

        Bind();
    }

    void Bind()
    {
        var dialogue = Find<InGameDialogue>();
        if (dialogue != _dialogue)
        {
            if (_dialogue != null) _dialogue.SubtitleChanged -= OnDialogueSubtitle;
            _dialogue = dialogue;
            if (_dialogue != null) _dialogue.SubtitleChanged += OnDialogueSubtitle;
        }

        var cutscene = Find<CutsceneDirector>();
        if (cutscene != _cutscene)
        {
            if (_cutscene != null) _cutscene.SubtitleChanged -= OnCutsceneSubtitle;
            _cutscene = cutscene;
            if (_cutscene != null) _cutscene.SubtitleChanged += OnCutsceneSubtitle;
        }

        var conversation = Find<AiConversationManager>();
        if (conversation == _conversation) return;

        if (_conversation != null) _conversation.SubtitleChanged -= OnAiManagerSubtitle;
        _conversation = conversation;
        if (_conversation != null) _conversation.SubtitleChanged += OnAiManagerSubtitle;
    }

    void Unbind()
    {
        if (_dialogue != null) _dialogue.SubtitleChanged -= OnDialogueSubtitle;
        if (_cutscene != null) _cutscene.SubtitleChanged -= OnCutsceneSubtitle;
        if (_conversation != null) _conversation.SubtitleChanged -= OnAiManagerSubtitle;
        _dialogue = null;
        _cutscene = null;
        _conversation = null;
    }

    /// <summary>
    /// 이미 있을 때만 잡는다. <c>Singleton.Instance</c>는 없으면 만들어 버리는데, 자막 매니저가
    /// 이 씬이 요청한 적 없는 대사·컷씬 서비스를 띄울 이유가 없다. <c>SubtitleView.Find</c>와
    /// 같은 규칙이며, 싱글턴의 Awake가 아직 안 돈 구간을 씬 조회가 메운다.
    /// </summary>
    static T Find<T>() where T : MonoBehaviour =>
        Singleton<T>.HasInstance
            ? Singleton<T>.Instance
            : FindAnyObjectByType<T>(FindObjectsInactive.Include);

    // ---- 자막 채널 ----

    void OnDialogueSubtitle(DialogueSpeaker speaker, string text) => Apply(speaker, text, false);

    void OnCutsceneSubtitle(DialogueSpeaker speaker, string text) => Apply(speaker, text, true);

    void Apply(DialogueSpeaker speaker, string text, bool fromCutscene)
    {
        // 도메인 리로드를 끈 채 플레이를 반복하면 static 이벤트에 죽은 구독이 남을 수 있다.
        if (_bubble == null) return;

        _dialogueActive = !string.IsNullOrWhiteSpace(text);

        if (_dialogueActive)
        {
            _speaker = speaker;
            _fromCutscene = fromCutscene;
            _dialogueText = text;
            Show(speaker, text, fromCutscene);
            return;
        }

        _dialogueText = null;

        if (_aiText != null)
        {
            _speaker = DialogueSpeaker.Ieumi;
            _fromCutscene = false;
            Show(DialogueSpeaker.Ieumi, _aiText, false);
            return;
        }

        _bubble.Hide();
    }

    /// <summary>
    /// AI 자막 원본. <c>AiConversationHud</c>가 이것을 문장 단위로 쪼개 다시 보내 주므로 정상
    /// 경로에서는 쓰이지 않고, HUD가 없거나 UXML이 없어 동작하지 못하는 씬에서만 쓰인다.
    ///
    /// 씬에 HUD가 있는지 미리 찾아 두는 방식을 쓰지 않는다. 비활성 HUD나 UXML이 빠져 초기화에
    /// 실패한 HUD도 "있음"으로 잡혀, 페이지가 영영 오지 않는데 원본까지 막아 버린다. 대신 <b>실제로
    /// 페이지가 왔는지</b>를 본다 — 원본을 한 프레임 미뤄 두었다가 그동안 페이지가 오면 버린다.
    /// 매니저와 HUD 중 누가 먼저 구독했든 결과가 같다.
    /// </summary>
    void OnAiManagerSubtitle(string text)
    {
        if (_lastPagedFrame == Time.frameCount) return;

        _pendingAiRaw = text;
        _hasPendingAiRaw = true;
    }

    void LateUpdate()
    {
        if (!_hasPendingAiRaw) return;

        _hasPendingAiRaw = false;
        OnAiSubtitle(_pendingAiRaw);
        _pendingAiRaw = null;
    }

    /// <summary>
    /// <c>AiConversationHud</c>가 문장 페이징을 끝낸 한 페이지. 문장 분할 규칙을 두 벌 두지 않으려고
    /// 원본 대신 이쪽을 정상 경로로 삼는다.
    /// </summary>
    void OnAiPage(string text)
    {
        _lastPagedFrame = Time.frameCount;
        OnAiSubtitle(text);
    }

    /// <summary>이음이 AI 답변 자막을 실제로 표시한다. 화자가 항상 이음이라 색도 그 규칙(#4FCBFF)이다.</summary>
    void OnAiSubtitle(string text)
    {
        if (_bubble == null) return;

        // 대기 중인 원본이 있으면 버린다. 페이지가 왔으니 그쪽이 더 정확하다.
        _hasPendingAiRaw = false;
        _pendingAiRaw = null;

        _aiText = string.IsNullOrWhiteSpace(text) ? null : text;

        // 대사·컷씬이 앞선다. 그 자막이 내려갈 때 Apply가 보류분을 꺼내 준다.
        if (_dialogueActive) return;

        if (_aiText != null)
        {
            _speaker = DialogueSpeaker.Ieumi;
            _fromCutscene = false;
            Show(DialogueSpeaker.Ieumi, _aiText, false);
            return;
        }

        _bubble.Hide();
    }

    void Show(DialogueSpeaker speaker, string text, bool fromCutscene)
    {
        if (_bubble == null) return;

        if (!BubblesEnabled)
        {
            _bubble.Hide();
            return;
        }

        ResolveAnchor(speaker, fromCutscene, out var boundsRoot, out var head);
        _bubble.SetAnchor(boundsRoot, head);
        _bubble.Show(speaker, text);
    }

    /// <summary>표시 경로가 바뀌었을 때 현재 자막을 다시 적용한다.</summary>
    void ReapplyBubble()
    {
        if (_bubble == null) return;

        if (_dialogueActive && _dialogueText != null) Show(_speaker, _dialogueText, _fromCutscene);
        else if (_aiText != null) Show(DialogueSpeaker.Ieumi, _aiText, false);
        else _bubble.Hide();
    }

    // ---- 화자 앵커 ----

    /// <summary>
    /// 화자를 씬에서 찾아 말풍선이 붙을 곳을 정한다.
    ///
    /// <paramref name="boundsRoot"/>는 렌더러 바운즈를 재는 뿌리이고 <paramref name="head"/>는
    /// 렌더러가 하나도 없을 때 쓰는 대체 지점이다. 둘 다 null이면 카메라 앞 폴백 패널이 된다.
    ///
    /// 컷씬은 화자를 찾지 않고 바로 폴백으로 간다. 컷씬 씬은 자체 카메라로 다른 곳을 비추는데
    /// 주 씬의 노장·이음이는 그대로 살아 있어서, 그쪽에 말풍선을 달면 화면 밖 어딘가에 뜬다.
    /// </summary>
    void ResolveAnchor(DialogueSpeaker speaker, bool fromCutscene, out Transform boundsRoot, out Transform head)
    {
        boundsRoot = null;
        head = null;

        if (fromCutscene || speaker == DialogueSpeaker.Narration) return;

        switch (speaker)
        {
            case DialogueSpeaker.Nojang:
            {
                if (_nojang == null) _nojang = FindAnyObjectByType<NojangBehaviour>();
                if (!IsUsable(_nojang)) return;

                boundsRoot = _nojang.transform;
                head = ResolveFace(_nojang.transform);
                return;
            }

            case DialogueSpeaker.Ieumi:
            {
                if (_ieumi == null) _ieumi = FindAnyObjectByType<EeumBehaviour>();
                if (!IsUsable(_ieumi)) return;

                boundsRoot = _ieumi.transform;

                // 이음이 FBX는 Generic 리그라 휴머노이드 머리 본 조회가 통하지 않는다.
                // GazeTracker가 쓰는 머리 트랜스폼을 그대로 재사용한다(루트 폴백 포함).
                head = _ieumi.FaceTransform;
                return;
            }
        }
    }

    /// <summary>꺼져 있는 화자에는 말풍선을 달지 않는다. 폴백 패널이 대신 받는다.</summary>
    static bool IsUsable(MonoBehaviour behaviour) =>
        behaviour != null && behaviour.gameObject.activeInHierarchy;

    /// <summary>휴머노이드면 머리 본. 아니면 루트 그대로. <c>TutorialFlowDirector</c>와 같은 규칙이다.</summary>
    static Transform ResolveFace(Transform speaker)
    {
        var animator = speaker.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return speaker;

        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        return head != null ? head : speaker;
    }

    /// <summary>
    /// 디버그용. 말풍선이 현재 쓰는 폰트를 밖에서 갈아 끼운다 — 한글이 깨질 때 확인 경로가 된다.
    /// </summary>
    public static void SetFont(TMP_FontAsset font) => DialogueBubbleFont.Override = font;
}
