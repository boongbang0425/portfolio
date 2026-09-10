using System;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// 월드 공간 일시정지 HUD (F-019 2.2, F-002). Owns the Pause and Option panels built from the FBX
/// meshes and wires their plates to <see cref="PauseController"/>.
///
/// Replaces what <see cref="PauseMenuView"/> draws; it does not replace it. The pause layer itself is
/// untouched — this class only listens to <see cref="PauseController.VisibilityChanged"/> and calls
/// the same public methods the UXML view calls.
///
/// 이동·잡기·상호작용 차단은 여기서 하지 않는다. <see cref="PauseController.Open"/>이 이미
/// <see cref="InputLockFlags.Menu"/>를 잡고 <see cref="PauseService"/>가 timeScale을 0으로 만든다.
/// 이 클래스가 하는 일은 그 잠금 아래에서도 메뉴만은 조작되게 하는 것이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PauseWorldMenu : MonoBehaviour
{
    const float BindRetrySeconds = 0.5f;

    [Header("패널")]
    [SerializeField] Transform pausePanel;
    [SerializeField] Transform optionPanel;

    [Header("일시정지 버튼")]
    [SerializeField] WorldMenuButton resumeButton;
    [SerializeField] WorldMenuButton optionsButton;
    [SerializeField] WorldMenuButton restartProcessButton;
    [SerializeField] WorldMenuButton mainMenuButton;

    [Header("옵션")]
    [SerializeField] WorldMenuButton optionBackButton;
    [SerializeField] WorldMenuSlider musicSlider;
    [SerializeField] WorldMenuSlider dialogueSlider;
    [SerializeField] WorldMenuSlider environmentSlider;
    [SerializeField] WorldMenuSlider videoSlider;

    [Header("배치")]
    [Tooltip("패널 앞면이 향하는 로컬 축. FBX의 +Z가 임포트 후 루트의 +Y가 된다.")]
    [SerializeField] Vector3 panelFacingLocal = Vector3.up;

    [Tooltip("패널 위쪽에 해당하는 로컬 축.")]
    [SerializeField] Vector3 panelUpLocal = Vector3.back;

    [Tooltip("열릴 때 플레이어 앞 몇 미터에 고정할지.")]
    [SerializeField, Min(0.3f)] float distance = 1.3f;

    [Tooltip("눈높이 대비 세로 오프셋.")]
    [SerializeField] float verticalOffset = -0.1f;

    [Tooltip("패널 세로 길이를 이 미터 값에 맞춰 균일 스케일한다.")]
    [SerializeField, Min(0.1f)] float panelHeight = 0.95f;

    [SerializeField] bool autoFitScale = true;

    [Header("포인터")]
    [SerializeField] WorldMenuPointer[] pointers;

    readonly VolumeSettingsAdapter _volumes = new();

    PauseController _controller;
    float _nextBindAttempt;
    bool _wired;
    bool _measured;
    Vector3 _localCentre;

    void Awake()
    {
        Wire();
        SetVisible(false);
    }

    void Start()
    {
        // Deferred to Start so every WorldMenuButton has woken up and cannot overwrite the press
        // offset with its own Awake defaults.
        ConfigurePressDirection();
    }

    void OnEnable()
    {
        _nextBindAttempt = 0f;
        TryBindController();
    }

    void OnDisable()
    {
        if (_controller != null)
        {
            _controller.VisibilityChanged -= SetVisible;
            _controller = null;
        }

        _volumes.Flush();
    }

    void Update()
    {
        if (_controller == null && Time.unscaledTime >= _nextBindAttempt)
        {
            _nextBindAttempt = Time.unscaledTime + BindRetrySeconds;
            TryBindController();
        }

        _volumes.Tick();
    }

    void Wire()
    {
        if (_wired) return;
        _wired = true;

        Bind(resumeButton, OnResume);
        Bind(optionsButton, ShowOption);
        Bind(restartProcessButton, OnRestartProcess);
        Bind(mainMenuButton, OnMainMenu);
        Bind(optionBackButton, ShowPause);

        Bind(musicSlider, VolumeSettingsAdapter.Channel.Music);
        Bind(dialogueSlider, VolumeSettingsAdapter.Channel.Dialogue);
        Bind(environmentSlider, VolumeSettingsAdapter.Channel.Environment);
        Bind(videoSlider, VolumeSettingsAdapter.Channel.Video);

        foreach (var pointer in Pointers()) pointer.SetMenuRoot(transform);
    }

    void Bind(WorldMenuButton button, Action action)
    {
        if (button == null)
        {
            Debug.LogError("[PauseHud] 버튼 매핑이 비어 있습니다. PauseWorldMenu의 인스펙터를 확인하십시오.", this);
            return;
        }

        button.Clicked += action;
    }

    void Bind(WorldMenuSlider slider, VolumeSettingsAdapter.Channel channel)
    {
        if (slider == null)
        {
            Debug.LogError($"[PauseHud] {channel} 슬라이더 매핑이 비어 있습니다.", this);
            return;
        }

        slider.ValueChanged += value => _volumes.Write(channel, value);
    }

    /// <summary>
    /// Hands every plate the world direction that points into the panel, which is the way it travels
    /// when pressed. The plates cannot work it out alone: their meshes only say which axis is thin,
    /// not which side of it the player is standing on.
    /// </summary>
    void ConfigurePressDirection()
    {
        var into = transform.TransformDirection(-panelFacingLocal.normalized);

        foreach (var button in GetComponentsInChildren<WorldMenuButton>(true))
            button.Configure(into);
    }

    bool TryBindController()
    {
        if (_controller != null) return true;

        _controller = PauseController.HasInstance
            ? PauseController.Instance
            : FindAnyObjectByType<PauseController>(FindObjectsInactive.Include);

        if (_controller == null) return false;

        _controller.VisibilityChanged += SetVisible;
        if (_controller.IsOpen) SetVisible(true);
        return true;
    }

    void SetVisible(bool visible)
    {
        if (visible)
        {
            Place();
            ShowPause();
        }
        else
        {
            SetPanel(pausePanel, false);
            SetPanel(optionPanel, false);
            foreach (var pointer in Pointers()) pointer.gameObject.SetActive(false);

            // 옵션을 열어둔 채 메뉴를 닫아도 저장은 남지 않는다 (F-002 2.5).
            _volumes.Flush();
        }
    }

    /// <summary>
    /// World-locked in front of the player rather than head-locked. A panel that follows the head
    /// cannot be looked away from and reads as attached to the eyes, which is the usual source of
    /// discomfort with VR menus.
    /// </summary>
    void Place()
    {
        var view = ResolveView();
        if (view == null)
        {
            Debug.LogError("[PauseHud] 카메라를 찾지 못해 메뉴를 배치할 수 없습니다.", this);
            return;
        }

        Measure();

        var forward = view.forward;
        forward.y = 0f;

        // Looking straight up or down leaves no usable heading; the player's own facing is the
        // sensible fallback.
        if (forward.sqrMagnitude < 1e-4f) forward = view.up;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();

        // The panel's own facing axis has to end up pointing back at the player, and its up axis has
        // to end up vertical. Composing with the inverse of the local basis does both at once.
        var localBasis = Quaternion.LookRotation(panelFacingLocal.normalized, panelUpLocal.normalized);
        transform.rotation = Quaternion.LookRotation(-forward, Vector3.up) * Quaternion.Inverse(localBasis);

        var anchor = view.position + forward * distance + Vector3.up * verticalOffset;

        // The FBX has the panel occupying x -50..0 and y 0..70 rather than straddling its own origin,
        // so putting the root at the anchor would hang the panel off to one side and above the
        // player's eyeline. Backing out the measured centre puts the panel itself where it belongs.
        transform.position = anchor - transform.rotation * Vector3.Scale(_localCentre, transform.localScale);
    }

    /// <summary>
    /// Reads the panel's own extent once and scales it to <see cref="panelHeight"/>. Local bounds are
    /// independent of the root's scale, so this can run before or after the scale is applied.
    /// </summary>
    void Measure()
    {
        if (_measured) return;
        _measured = true;

        if (!WorldMenuGeometry.TryLocalBounds(transform, out var bounds))
        {
            Debug.LogError("[PauseHud] 패널 메시를 찾지 못해 크기를 맞출 수 없습니다.", this);
            return;
        }

        _localCentre = bounds.center;
        if (!autoFitScale) return;

        var up = panelUpLocal.normalized;
        var height = Vector3.Dot(bounds.size, new Vector3(
            Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)));

        if (height < 1e-4f) return;
        transform.localScale = Vector3.one * (panelHeight / height);
    }

    Transform ResolveView()
    {
        var camera = Camera.main;
        if (camera != null) return camera.transform;

        var any = FindAnyObjectByType<Camera>();
        return any != null ? any.transform : null;
    }

    void ShowPause()
    {
        SetPanel(pausePanel, true);
        SetPanel(optionPanel, false);
        EnablePointers();
    }

    void ShowOption()
    {
        SetPanel(pausePanel, false);
        SetPanel(optionPanel, true);
        EnablePointers();
        RefreshVolumes();
    }

    void RefreshVolumes()
    {
        Pull(musicSlider, VolumeSettingsAdapter.Channel.Music);
        Pull(dialogueSlider, VolumeSettingsAdapter.Channel.Dialogue);
        Pull(environmentSlider, VolumeSettingsAdapter.Channel.Environment);
        Pull(videoSlider, VolumeSettingsAdapter.Channel.Video);
    }

    void Pull(WorldMenuSlider slider, VolumeSettingsAdapter.Channel channel)
    {
        if (slider == null) return;
        if (_volumes.TryRead(channel, out var value)) slider.SetValueWithoutNotify(value);
    }

    /// <summary>
    /// 데스크톱에서는 오른손 포인터 하나만 켠다. 두 포인터 모두 마우스 커서에서 레이를 만들기 때문에,
    /// 둘 다 켜 두면 같은 버튼에 클릭이 두 번 들어간다.
    /// </summary>
    void EnablePointers()
    {
        var desktop = !XRSettings.isDeviceActive;

        foreach (var pointer in Pointers())
            pointer.gameObject.SetActive(!desktop || pointer.Hand == XRHandSide.Right);
    }

    static void SetPanel(Transform panel, bool active)
    {
        if (panel != null) panel.gameObject.SetActive(active);
    }

    WorldMenuPointer[] Pointers() => pointers ?? Array.Empty<WorldMenuPointer>();

    void OnResume() => _controller?.Close();

    void OnRestartProcess() => _controller?.RequestProcessRestart();

    /// <summary>
    /// 메인 화면으로 (F-019 2.4). 프리팹에 확인창 지오메트리가 없어 확인 단계 없이 즉시 이동한다.
    /// </summary>
    void OnMainMenu() => _controller?.ReturnToMainMenu();
}
