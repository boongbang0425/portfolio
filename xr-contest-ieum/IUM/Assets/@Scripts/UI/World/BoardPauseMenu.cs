using System;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// BoardRoot 기반 월드 공간 일시정지 HUD (F-019 2.2, F-002). <see cref="PauseWorldMenu"/>를 참조
/// 모델로 팀원 제작 BoardRoot 프리팹(BoardSwitcher + Board3DButton/Board3DSlider)을
/// <see cref="PauseController"/>에 배선한다.
///
/// 일시정지 레이어 자체는 건드리지 않는다 — <see cref="PauseController.VisibilityChanged"/>를 구독해
/// 보드를 보이고 숨길 뿐이고, 버튼은 UXML 뷰가 부르는 것과 같은 공개 메서드를 부른다.
/// 판 전환(일시정지↔옵션)은 프리팹에 배선된 <see cref="BoardSwitcher"/>가 그대로 담당한다.
///
/// 이동·잡기·상호작용 차단도 여기서 하지 않는다. <see cref="PauseController.Open"/>이 이미
/// <see cref="InputLockFlags.Menu"/>를 잡고 <see cref="PauseService"/>가 timeScale을 0으로 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoardPauseMenu : MonoBehaviour
{
    const float BindRetrySeconds = 0.5f;

    [Header("보드")]
    [Tooltip("BoardRoot 인스턴스의 판 전환기. 옵션·뒤로가기 버튼의 퍼시스턴트 onClick이 이미 배선돼 있다.")]
    [SerializeField] BoardSwitcher switcher;

    [Tooltip("표시/숨김 대상인 BoardRoot 인스턴스 루트.")]
    [SerializeField] Transform boardRoot;

    [Header("일시정지 버튼")]
    [SerializeField] Board3DButton continueButton;
    [SerializeField] Board3DButton optionsButton;
    [SerializeField] Board3DButton restartButton;
    [SerializeField] Board3DButton mainMenuButton;

    [Header("옵션")]
    [SerializeField] Board3DButton backButton;
    [SerializeField] Board3DSlider musicSlider;
    [Tooltip("Slider_Voice. 저장 채널은 Dialogue다.")]
    [SerializeField] Board3DSlider voiceSlider;
    [Tooltip("Slider_Ambient. 저장 채널은 Environment다.")]
    [SerializeField] Board3DSlider ambientSlider;
    [SerializeField] Board3DSlider videoSlider;

    [Header("배치")]
    [Tooltip("보드 앞면이 향하는 로컬 축. 글리프 돌출과 프리팹의 포인트 라이트(+Z에서 -Z로 비춤) " +
             "기준으로 +Z로 추정했다. 에디터 확인 전 추정값이다 (ISSUE-026과 같은 성격).")]
    [SerializeField] Vector3 panelFacingLocal = Vector3.forward;

    [Tooltip("보드 위쪽에 해당하는 로컬 축.")]
    [SerializeField] Vector3 panelUpLocal = Vector3.up;

    [Tooltip("열릴 때 플레이어 앞 몇 미터에 고정할지. 기존 PauseHud와 같은 체감을 위한 값이다.")]
    [SerializeField, Min(0.3f)] float distance = 1.3f;

    [Tooltip("눈높이 대비 세로 오프셋.")]
    [SerializeField] float verticalOffset = -0.1f;

    [Tooltip("보드 세로 길이를 이 미터 값에 맞춰 균일 스케일한다. 루트 스케일 4.3의 원본 판은 약 " +
             "0.2m라 이 보정 없이는 기존 HUD 대비 시야각이 절반 이하다.")]
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
        if (boardRoot == null && switcher != null) boardRoot = switcher.transform;

        Wire();
        SetVisible(false);
    }

    void Start()
    {
        // Start로 미룬 이유는 PauseWorldMenu와 같다 — 모든 버튼의 Awake가 끝난 뒤 눌림 방향을
        // 주입해야 버튼 쪽 기본값이 이를 덮어쓰지 못한다. 보드가 비활성이라 버튼 Awake가 아직
        // 안 돌았어도 Configure 선행은 Board3DButton이 지원한다.
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

        Bind(continueButton, OnResume);
        Bind(restartButton, OnRestartProcess);
        Bind(mainMenuButton, OnMainMenu);

        // 옵션·뒤로가기의 판 전환은 프리팹의 퍼시스턴트 onClick(BoardSwitcher.ShowOptions/ShowPause)이
        // 이미 맡고 있다. 여기서는 옵션이 열린 직후 저장값을 끌어오는 훅만 얹는다 — 런타임 리스너는
        // 퍼시스턴트 콜 뒤에 불리므로 슬라이더가 활성화된 상태에서 값이 들어간다.
        Bind(optionsButton, RefreshVolumes);
        if (backButton == null)
            Debug.LogError("[BoardHud] 뒤로가기 버튼 매핑이 비어 있습니다.", this);

        Bind(musicSlider, VolumeSettingsAdapter.Channel.Music);
        Bind(voiceSlider, VolumeSettingsAdapter.Channel.Dialogue);
        Bind(ambientSlider, VolumeSettingsAdapter.Channel.Environment);
        Bind(videoSlider, VolumeSettingsAdapter.Channel.Video);

        foreach (var pointer in Pointers()) pointer.SetMenuRoot(transform);
    }

    void Bind(Board3DButton button, Action action)
    {
        if (button == null)
        {
            Debug.LogError("[BoardHud] 버튼 매핑이 비어 있습니다. BoardPauseMenu의 인스펙터를 확인하십시오.", this);
            return;
        }

        button.onClick.AddListener(() => action());
    }

    void Bind(Board3DSlider slider, VolumeSettingsAdapter.Channel channel)
    {
        if (slider == null)
        {
            Debug.LogError($"[BoardHud] {channel} 슬라이더 매핑이 비어 있습니다.", this);
            return;
        }

        slider.onValueChanged += value => _volumes.Write(channel, value);
    }

    /// <summary>
    /// 모든 판에 "보드 안쪽" 월드 방향을 넘긴다. 판 혼자서는 자기 메시의 얇은 축만 알 뿐 어느
    /// 부호가 안쪽인지 모른다. PauseWorldMenu.ConfigurePressDirection과 같은 방식이다.
    /// </summary>
    void ConfigurePressDirection()
    {
        var into = transform.TransformDirection(-panelFacingLocal.normalized);

        foreach (var button in GetComponentsInChildren<Board3DButton>(true))
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
        if (boardRoot == null)
        {
            Debug.LogError("[BoardHud] BoardRoot 참조가 비어 있어 메뉴를 표시할 수 없습니다.", this);
            return;
        }

        if (visible)
        {
            Place();

            // 활성화 첫 회에는 BoardSwitcher.Awake가 ShowPause를 이미 수행한다. 그래도 명시적으로
            // 다시 부르는 이유는 재활성화 때문이다 — 이전에 옵션 판을 열어둔 채 닫혔을 수 있다.
            boardRoot.gameObject.SetActive(true);
            switcher?.ShowPause();
            EnablePointers();
        }
        else
        {
            boardRoot.gameObject.SetActive(false);
            foreach (var pointer in Pointers()) pointer.gameObject.SetActive(false);

            // 옵션을 열어둔 채 메뉴를 닫아도 저장은 남지 않는다 (F-002 2.5).
            _volumes.Flush();
        }
    }

    /// <summary>
    /// 머리 고정이 아니라 월드 고정으로 플레이어 앞에 둔다. 머리를 따라오는 패널은 시선을 뗄 수
    /// 없어 VR 메뉴 불편의 흔한 원인이다. PauseWorldMenu.Place와 같은 계산이다.
    /// </summary>
    void Place()
    {
        var view = ResolveView();
        if (view == null)
        {
            Debug.LogError("[BoardHud] 카메라를 찾지 못해 메뉴를 배치할 수 없습니다.", this);
            return;
        }

        Measure();

        var forward = view.forward;
        forward.y = 0f;

        // 수직으로 위·아래를 보는 중이면 진행 방향이 없다. 플레이어 몸의 위 방향이 합리적 폴백이다.
        if (forward.sqrMagnitude < 1e-4f) forward = view.up;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();

        // 보드의 앞면 축이 플레이어를 향하고 위 축이 수직이 되도록, 로컬 기저의 역과 합성한다.
        var localBasis = Quaternion.LookRotation(panelFacingLocal.normalized, panelUpLocal.normalized);
        transform.rotation = Quaternion.LookRotation(-forward, Vector3.up) * Quaternion.Inverse(localBasis);

        var anchor = view.position + forward * distance + Vector3.up * verticalOffset;

        // BoardRoot 프리팹은 자체 배치 좌표(y 1.3, z 0.6)와 스케일 4.3을 품고 있어 루트를 앵커에
        // 그대로 두면 보드가 옆·위로 밀린다. 실측한 중심을 되빼서 보드 자체를 앵커에 맞춘다.
        transform.position = anchor - transform.rotation * Vector3.Scale(_localCentre, transform.localScale);
    }

    /// <summary>
    /// 보드의 로컬 크기를 한 번 재고 <see cref="panelHeight"/>에 맞춰 스케일한다. 로컬 경계는
    /// 루트 스케일과 무관하므로 스케일 적용 전후 어느 시점에 돌아도 된다.
    /// </summary>
    void Measure()
    {
        if (_measured) return;
        _measured = true;

        if (!WorldMenuGeometry.TryLocalBounds(transform, out var bounds))
        {
            Debug.LogError("[BoardHud] 보드 메시를 찾지 못해 크기를 맞출 수 없습니다.", this);
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

    /// <summary>옵션 판이 열릴 때 저장값을 슬라이더로 끌어온다 (F-002 2.2).</summary>
    void RefreshVolumes()
    {
        Pull(musicSlider, VolumeSettingsAdapter.Channel.Music);
        Pull(voiceSlider, VolumeSettingsAdapter.Channel.Dialogue);
        Pull(ambientSlider, VolumeSettingsAdapter.Channel.Environment);
        Pull(videoSlider, VolumeSettingsAdapter.Channel.Video);
    }

    void Pull(Board3DSlider slider, VolumeSettingsAdapter.Channel channel)
    {
        if (slider == null) return;

        // SetValue는 onValueChanged를 발화하지 않으므로 되쓰기 루프가 생기지 않는다.
        if (_volumes.TryRead(channel, out var value)) slider.SetValue(value);
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

    WorldMenuPointer[] Pointers() => pointers ?? Array.Empty<WorldMenuPointer>();

    void OnResume() => _controller?.Close();

    void OnRestartProcess() => _controller?.RequestProcessRestart();

    /// <summary>
    /// 메인 화면으로 (F-019 2.4). BoardRoot에도 확인창 지오메트리가 없어 확인 단계 없이 즉시
    /// 이동한다 (ISSUE-024 상태 유지).
    /// </summary>
    void OnMainMenu() => _controller?.ReturnToMainMenu();
}
