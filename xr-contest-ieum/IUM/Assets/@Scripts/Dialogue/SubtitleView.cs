using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 자막 (F-018 1.4). Screen-overlay UIDocument so the whole dialogue flow is verifiable without an
/// HMD; the world-space VR subtitle can replace this view without touching the dialogue layer.
///
/// Serves both 인게임 대사 and 컷씬 대사. The two never overlap — a cutscene locks the inputs that
/// trigger in-game lines and holds the process gate — so one view can take both without arbitrating.
///
/// 이음이 AI 답변도 같은 뷰를 지난다 (<see cref="SetAiSubtitle"/>): AI 자막이 별도 스타일로
/// 따로 뜨지 않도록 대사 자막과 뷰를 공유한다. 대사·컷씬이 이 채널보다 항상 우선한다 — 대사가
/// 시작되면 PTT 잠금이 AI 파이프라인을 끊어 AI 자막이 먼저 내려가지만, 잠금 안내처럼 잠긴
/// 상태에서 발화되는 고정 대사가 겹칠 수 있어 뷰에서도 우선순위를 지킨다.
///
/// Deliberately independent of 대사 볼륨: a subtitle must show even when dialogue audio is muted.
///
/// VR에서는 ScreenSpaceOverlay 패널이 아예 렌더되지 않아 이 뷰의 자막이 HMD에서 보이지 않는다.
/// 그래서 실제 표시는 <see cref="WorldSubtitleDirector"/>의 월드 말풍선이 맡고, 이 뷰는
/// <see cref="WorldSubtitleDirector.ScreenSubtitlesEnabled"/>가 참일 때만 그린다 — 데스크톱에서
/// 같은 자막이 두 곳에 뜨지 않게 하기 위한 것이며, 기본값은 말풍선만이다. 뷰 자체는 남겨 둔다:
/// 말풍선 배치를 대조하거나 월드 말풍선이 막혔을 때 되돌아올 곳이 필요하다.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class SubtitleView : MonoBehaviour
{
    /// <summary>
    /// 화자별 글자색. 배경 판을 없앤 뒤로는 검은 외곽선 위에서 읽히는 값만 쓴다 — 순색은 밝은
    /// 하늘 배경에서 뭉개지므로 노장은 따뜻한 황금톤, 이음이는 밝은 하늘톤으로 낮춰 잡았다.
    /// </summary>
    static readonly Color NojangColor = new(1f, 0.804f, 0.376f);   // #FFCD60
    static readonly Color IeumiColor = new(0.31f, 0.796f, 1f);     // #4FCBFF
    static readonly Color DefaultColor = Color.white;              // #FFFFFF

    UIDocument _document;
    VisualElement _box;
    Label _speakerLabel;
    Label _textLabel;
    InGameDialogue _dialogue;
    CutsceneDirector _cutscene;

    /// <summary>보류 중인 AI 자막. 대사 자막이 내려가면 아직 유효할 때만 표시된다.</summary>
    string _aiText;

    /// <summary>대사·컷씬 자막이 화면에 떠 있는 동안 참. AI 채널은 이 동안 표시를 미룬다.</summary>
    bool _dialogueActive;

    /// <summary>표시 경로가 바뀌었을 때 다시 그리기 위해 들고 있는 마지막 대사 자막.</summary>
    DialogueSpeaker _speaker;
    string _text;

    /// <summary>UXML 요소가 준비됐는지. 아니면 호출자는 자체 폴백 표시로 내려간다.</summary>
    public bool IsReady => _box != null;

    void Awake() => _document = GetComponent<UIDocument>();

    void OnEnable()
    {
        if (_document?.visualTreeAsset == null) return;

        var root = _document.rootVisualElement;
        _box = root.Q<VisualElement>("subtitle-box");
        _speakerLabel = root.Q<Label>("subtitle-speaker");
        _textLabel = root.Q<Label>("subtitle-text");

        if (_box == null || _speakerLabel == null || _textLabel == null)
        {
            Debug.LogError("[Subtitle] Required elements are missing from Subtitle.uxml.");
            return;
        }

        _aiText = null;
        _text = null;
        _dialogueActive = false;
        Hide();

        _dialogue = Find<InGameDialogue>();
        if (_dialogue != null) _dialogue.SubtitleChanged += OnSubtitleChanged;

        _cutscene = Find<CutsceneDirector>();
        if (_cutscene != null) _cutscene.SubtitleChanged += OnSubtitleChanged;

        WorldSubtitleDirector.DisplayModeChanged += OnDisplayModeChanged;
    }

    void OnDisable()
    {
        WorldSubtitleDirector.DisplayModeChanged -= OnDisplayModeChanged;

        if (_dialogue != null)
        {
            _dialogue.SubtitleChanged -= OnSubtitleChanged;
            _dialogue = null;
        }

        if (_cutscene == null) return;
        _cutscene.SubtitleChanged -= OnSubtitleChanged;
        _cutscene = null;
    }

    /// <summary>표시 경로가 바뀌면 지금 떠 있는 자막에 바로 반영한다. 다음 줄까지 기다리지 않는다.</summary>
    void OnDisplayModeChanged(SubtitleDisplayMode mode) => Reapply();

    /// <summary>
    /// Binds to a source only if it already exists. Singleton.Instance would create one, and a
    /// subtitle view has no business spinning up a dialogue or cutscene service that this scene
    /// never asked for. The scene lookup covers the case where the source's Awake has not run yet.
    /// </summary>
    static T Find<T>() where T : MonoBehaviour =>
        Singleton<T>.HasInstance
            ? Singleton<T>.Instance
            : FindAnyObjectByType<T>(FindObjectsInactive.Include);

    void OnSubtitleChanged(DialogueSpeaker speaker, string text)
    {
        if (_box == null) return;

        _dialogueActive = !string.IsNullOrWhiteSpace(text);
        _speaker = speaker;
        _text = text;

        Reapply();
    }

    /// <summary>
    /// 채널 우선순위(대사·컷씬 &gt; AI)와 표시 경로를 한 곳에서 판정한다. 자막이 새로 오거나
    /// 표시 경로가 바뀌면 여기를 다시 지난다.
    /// </summary>
    void Reapply()
    {
        if (_box == null) return;

        if (!WorldSubtitleDirector.ScreenSubtitlesEnabled)
        {
            Hide();
            return;
        }

        if (_dialogueActive) Show(_speaker, _text);
        else if (_aiText != null) Show(DialogueSpeaker.Ieumi, _aiText);
        else Hide();
    }

    /// <summary>
    /// 이음이 AI 답변 자막 채널. 대사 자막과 같은 뷰·스타일로 표시하되, 대사·컷씬 자막이 떠 있는
    /// 동안은 보류하고 그 자막이 내려간 뒤 아직 유효하면 표시한다. null 또는 공백이면 숨긴다.
    /// 화자 색은 항상 이음이 규칙(#4FCBFF)을 따른다.
    /// </summary>
    public void SetAiSubtitle(string text)
    {
        if (_box == null) return;

        _aiText = string.IsNullOrWhiteSpace(text) ? null : text;
        if (_dialogueActive) return;

        Reapply();
    }

    void Show(DialogueSpeaker speaker, string text)
    {
        // 이름표는 쓰지 않는다 — 화자 구분은 본문 글자색이 맡는다. 인라인 display를 세우면
        // USS의 숨김 규칙을 덮어쓰므로 여기서는 건드리지 않는다.
        _textLabel.text = text;
        ApplySpeakerColor(speaker);
        _box.AddToClassList("subtitle-box--visible");
    }

    void Hide()
    {
        _box.RemoveFromClassList("subtitle-box--visible");
        _textLabel.text = string.Empty;
        _speakerLabel.text = string.Empty;
    }

    void ApplySpeakerColor(DialogueSpeaker speaker)
    {
        var color = speaker switch
        {
            DialogueSpeaker.Nojang => NojangColor,
            DialogueSpeaker.Ieumi => IeumiColor,
            DialogueSpeaker.Narration => DefaultColor,
            _ => DefaultColor
        };

        _textLabel.style.color = color;
    }
}
