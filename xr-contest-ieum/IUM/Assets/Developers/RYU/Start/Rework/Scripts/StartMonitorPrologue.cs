using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR;

/// <summary>
/// 시작 화면의 "처음부터" 연출. 플레이어를 중앙 모니터 앞 시청 지점으로 옮기고 시점을 모니터
/// 중앙으로 돌린 뒤, 프롤로그 영상을 모니터 캔버스에서 재생하고 끝나면(또는 건너뛰면) 프롤로그를
/// 완료 처리해 다음 공정으로 진입한다.
///
/// 전체화면 프롤로그 컷씬(cutscene.json "prologue")과의 중복 재생은 경로 차단으로 막는다:
/// <see cref="GameFlow.PrepareNewGameAsync"/>는 진행 초기화·저장만 하고 GoTo(Prologue)를 부르지
/// 않으므로 <see cref="CutsceneDirector"/>가 개입할 일이 없고, 종료 시
/// <see cref="GameFlow.CompletePrologueExternallyAsync"/>가 컷씬 완료와 동일한 기록·저장·다음 공정
/// 진입을 수행한다.
///
/// 영상 재생은 <see cref="CutsceneVideoSurface"/>의 선례를 따른 경량 구현이다(APIOnly VideoPlayer,
/// 매 프레임 VIDEO 버스 볼륨 적용, 오류 시 종료 처리로 진행 불가 방지). 건너뛰기는 컷씬과 같은
/// 홀드 방식이며 <see cref="PlayerCommands.Skip"/>을 읽으므로 데스크톱(Space)과 XR이 같은 경로를
/// 쓴다.
///
/// 여러 파일로 분할된 영상(<see cref="CutsceneDefinition.Videos"/>)은 목록 순서대로 이어 재생한다.
/// 플레이어를 두 개 두어 한 편이 나가는 동안 다음 편을 미리 열어 두고(더블 버퍼), 편이 끝나는
/// 순간 화면 참조만 바꿔 전환 갭을 최소화한다. 건너뛰기 홀드는 어느 편이 나가는 중이든 전체
/// 프롤로그를 끝낸다 — 이 역시 <see cref="CutsceneVideoSurface"/>와 같은 방식이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StartMonitorPrologue : MonoBehaviour
{
    [Header("영상 표면 (중앙 모니터 캔버스)")]
    [SerializeField] GameObject videoGroup;
    [SerializeField] RawImage videoImage;
    [SerializeField] AspectRatioFitter videoFitter;
    [SerializeField] GameObject skipGaugeRoot;
    [Tooltip("건너뛰기 게이지의 표시 알파. 비어 있으면 종전대로 영상 내내 게이지를 띄운다.")]
    [SerializeField] CanvasGroup skipGaugeGroup;
    [SerializeField] Image skipGaugeFill;

    [Header("시청 위치·시선")]
    [Tooltip("영상 시청 위치. 회전의 yaw만 플레이어에 적용한다.")]
    [SerializeField] Transform viewAnchor;
    [Tooltip("시선을 고정할 지점. 중앙 모니터 캔버스 중심을 넣는다.")]
    [SerializeField] Transform lookTarget;
    [Tooltip("시선 회전에 걸리는 시간(초). 데스크톱 전용이며 XR에서는 회전을 생략한다.")]
    [SerializeField, Min(0f)] float turnSeconds = 1f;

    // 기본 22°의 근거(시청 거리 1.7m에서 화면 높이 0.506m가 시야 세로의 70~80%를 채우는 값):
    // 눈높이 = 발 + 1.6m(Player 프리팹 Head)이고 화면 중심 y≈1.909m라 눈이 중심보다 약 0.31m 낮다.
    // 시선을 화면 중심으로 올려 보면(피치 θ=atan(0.31/1.7)≈10.3°) 화면 상단(y=2.162m, 눈 기준
    // +0.562m)은 시선 위 atan(0.562/1.7)−θ≈8.0°, 하단(y=1.656m, +0.056m)은 시선 아래
    // θ−atan(0.056/1.7)≈8.4°에 있다. 채움 비율 fill = [tan8.0° + tan8.4°] / (2·tan(FOV/2))이므로
    // FOV 22°에서 fill≈74%로 목표 범위 중앙에 든다(20°→82%, 25°→65%). 가로는 16:9 수평 반FOV
    // tan⁻¹(tan11°·16/9)≈19.1° > 화면 반각 atan(0.4545/1.7)≈15.0°라 좌우가 잘리지 않는다.
    [Header("영상 시청 FOV")]
    [Tooltip("영상 시청 동안 적용할 카메라 수직 FOV(도). XR에서는 적용하지 않는다.")]
    [SerializeField, Range(1f, 179f)] float videoFieldOfView = 22f;
    [Tooltip("FOV 전환 보간 시간(초). 일시정지와 무관하게 unscaled 시간으로 진행한다.")]
    [SerializeField, Min(0f)] float fovTweenSeconds = 0.3f;

    [Header("영상 소스")]
    [Tooltip("cutscene.json의 prologue video 값을 읽지 못했을 때 쓰는 StreamingAssets 상대 경로.")]
    [SerializeField] string fallbackVideoPath = "OnboardingVideoVRT.webm";
    [Tooltip("cutscene.json settings.skipHoldSeconds를 읽지 못했을 때의 건너뛰기 홀드 시간.")]
    [SerializeField, Min(0.1f)] float fallbackSkipHoldSeconds = 2f;

    /// <summary>flow.json에서 prologue 공정이 가리키는 컷씬 id. 영상 경로를 여기서 빌려 읽는다.</summary>
    const string PrologueCutsceneId = "prologue";

    /// <summary>
    /// 영상 준비 제한 시간. 로컬 파일 한 편을 여는 데 필요한 시간보다 넉넉하며, 준비가 오류도 완료도
    /// 없이 멎었을 때 프롤로그가 입력 잠금을 쥔 채 영원히 멈춰 있는 것을 막는다.
    /// </summary>
    const float PrepareTimeoutSeconds = 15f;

    /// <summary>건너뛰기 게이지가 나타나는 시간(초). 키를 누른 반응이 늦게 느껴지지 않을 만큼 짧다.</summary>
    const float SkipGaugeFadeInSeconds = 0.15f;

    /// <summary>게이지가 사라지는 시간(초). 나타날 때보다 느려야 손을 뗀 뒤가 부드럽다.</summary>
    const float SkipGaugeFadeOutSeconds = 0.25f;

    /// <summary>활성 플레이어. 화면과 소리는 항상 이 쌍에서 나간다.</summary>
    VideoPlayer _player;
    AudioSource _audio;

    /// <summary>대기 플레이어. 다음 편을 미리 열어 두는 더블 버퍼로, 목록이 2편 이상일 때만 만든다.</summary>
    VideoPlayer _standby;
    AudioSource _standbyAudio;

    readonly List<string> _playlist = new();
    int _activeIndex;

    /// <summary>대기 플레이어가 맡은 편의 목록 인덱스. -1이면 준비할 다음 편이 없다.</summary>
    int _standbyIndex = -1;
    bool _standbyPrepared;

    /// <summary>현재 편이 끝났는데 다음 편 준비가 아직일 때. 마지막 프레임을 띄운 채 완료를 기다린다.</summary>
    bool _waitingForStandby;

    /// <summary>FOV를 줄여 둔 플레이어 카메라와 원래 값. XR이거나 카메라를 못 찾으면 비어 있다.</summary>
    Camera _videoCamera;
    float _originalFieldOfView;
    bool _fovApplied;

    /// <summary>FOV 보간 세대. 새 보간이 시작되면 증가해 이전 보간 루프를 중단시킨다.</summary>
    int _fovTweenVersion;

    TaskCompletionSource<bool> _preparation;
    bool _finished;
    IDisposable _inputLock;
    StartMenuController _startMenu;
    Func<Task> _handler;

    /// <summary>연출 진행 중 여부. <see cref="StartPlayMenu"/>가 메뉴를 숨길 때 읽는다.</summary>
    public bool IsRunning { get; private set; }

    void OnEnable() => PauseService.Changed += OnPauseChanged;

    void OnDisable() => PauseService.Changed -= OnPauseChanged;

    /// <summary>
    /// StartMenuController는 다른 오브젝트의 활성 시점에 좌우되므로 어댑터 선례대로 폴링해 연결한다.
    /// 새 게임 진입을 이 컴포넌트가 대신하도록 핸들러를 등록한다 — 덮어쓰기 확인(#confirm-panel)은
    /// 종전대로 StartMenuController가 먼저 처리한 뒤에 이 핸들러가 불린다.
    /// </summary>
    void Update()
    {
        if (_startMenu != null) return;

        _startMenu = FindFirstObjectByType<StartMenuController>();
        if (_startMenu == null) return;

        _handler = RunAsync;
        _startMenu.NewGameHandler = _handler;
    }

    /// <summary>처음부터: 데이터 준비 → 이동·시선 → 모니터 영상 → 프롤로그 완료 → 다음 공정.</summary>
    public async Task RunAsync()
    {
        if (IsRunning) return;
        IsRunning = true;

        // 컷씬과 같은 잠금 조합. Skip은 InputLockFlags 밖이라 홀드 건너뛰기는 계속 동작한다.
        _inputLock = InputLockService.Acquire(InputLockFlags.Cutscene, "시작 화면 프롤로그");

        try
        {
            if (!await GameFlow.Instance.PrepareNewGameAsync())
            {
                Debug.LogError("[StartPrologue] 새 게임 데이터를 준비하지 못해 프롤로그를 중단합니다.");
                return;
            }

            if (this == null) return;

            var player = FindFirstObjectByType<Player>();
            MovePlayerToAnchor(player);
            await TurnToTargetAsync(player);
            if (this == null) return;

            if (!await PlayVideoAsync(player))
                Debug.LogWarning("[StartPrologue] 영상 재생에 실패해 프롤로그를 영상 없이 완료합니다.");

            if (this == null) return;
            HideSurface();

            // 건너뛰기도 완료로 기록한다 (F-003 3.5와 동일한 규칙).
            await GameFlow.Instance.CompletePrologueExternallyAsync();
        }
        catch (Exception exception)
        {
            // 이 Task는 StartMenuController.StartNewGameAsync가 버리므로(`_ = ...`) 잡지 않으면
            // 예외가 어디에도 나타나지 않는다 — 처음부터를 눌러도 아무 일이 없는 것처럼 보인다.
            Debug.LogException(exception);
        }
        finally
        {
            _inputLock?.Dispose();
            _inputLock = null;
            HideSurface();
            IsRunning = false;
        }
    }

    /// <summary>
    /// <see cref="PlayerPoseTracker"/>의 Apply 패턴. CharacterController가 자기 위치 사본을 트랜스폼에
    /// 되쓰므로, 꺼두지 않으면 이동이 다음 프레임에 되돌아간다.
    /// </summary>
    void MovePlayerToAnchor(Player player)
    {
        if (player == null || viewAnchor == null) return;

        var controller = player.Controller;
        var wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;

        player.transform.SetPositionAndRotation(
            viewAnchor.position, Quaternion.Euler(0f, viewAnchor.eulerAngles.y, 0f));

        if (wasEnabled) controller.enabled = true;
    }

    /// <summary>
    /// 시선을 모니터 중앙으로 부드럽게 돌린다. HMD 착용 상태의 강제 시점 회전은 멀미 요인이라
    /// XR에서는 생략하고 로그만 남긴다 (ISSUE-028 정책, <c>TutorialFlowDirector</c> 선례).
    /// <see cref="PauseService.Now"/>로 진행해 일시정지 중에는 회전도 멈춘다.
    /// </summary>
    async Task TurnToTargetAsync(Player player)
    {
        if (player == null || player.View == null || lookTarget == null) return;

        if (XRSettings.isDeviceActive)
        {
            Debug.Log("[StartPrologue] XR 장치가 활성이라 프롤로그 시점 회전을 생략합니다.");
            return;
        }

        var head = player.Head;
        var from = head.forward;
        var elapsed = 0f;
        var last = PauseService.Now;

        while (this != null && player != null && lookTarget != null)
        {
            var direction = lookTarget.position - head.position;
            var to = direction.sqrMagnitude > 0.0001f ? direction.normalized : head.forward;
            var t = turnSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / turnSeconds);

            // 시작과 끝을 부드럽게 — 등속 회전은 시작하는 순간이 튄다.
            player.View.SetLookDirection(Vector3.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t)));

            if (t >= 1f) break;

            await Task.Yield();

            var now = PauseService.Now;
            elapsed += Mathf.Max(0f, now - last);
            last = now;
        }
    }

    /// <summary>영상을 준비·재생하고 종료 또는 건너뛰기 홀드 완료까지 기다린다.</summary>
    async Task<bool> PlayVideoAsync(Player player)
    {
        if (videoImage == null)
        {
            Debug.LogWarning("[StartPrologue] 영상 표면(RawImage)이 연결되지 않았습니다.");
            return false;
        }

        ResolveCutsceneData(out var paths, out var skipHold);

        _playlist.Clear();
        for (var i = 0; i < paths.Count; i++)
            if (!string.IsNullOrWhiteSpace(paths[i]))
                _playlist.Add(paths[i]);

        if (_playlist.Count == 0)
        {
            Debug.LogWarning("[StartPrologue] 재생할 영상 경로가 없습니다.");
            return false;
        }

        // 어느 목록이 실제로 잡혔는지 남긴다. cutscene.json을 못 읽어 폴백으로 떨어진 경우와 분할
        // 목록이 그대로 온 경우를 콘솔 한 줄로 구분할 수 있어야 한다.
        Debug.Log($"[StartPrologue] 영상 {_playlist.Count}편을 재생합니다: {string.Join(", ", _playlist)}");

        _activeIndex = 0;
        _standbyIndex = -1;
        _standbyPrepared = false;
        _waitingForStandby = false;

        EnsurePlayer();
        var prepared = await PrepareAsync(ResolveUrl(_playlist[0]));
        if (!prepared || this == null) return false;

        if (videoFitter != null && _player.width > 0 && _player.height > 0)
            videoFitter.aspectRatio = (float)_player.width / (float)_player.height;

        videoImage.texture = _player.texture;
        if (videoGroup != null) videoGroup.SetActive(true);
        // 게이지 오브젝트는 켜두고 알파로 감춘다 — 홀드가 시작되는 프레임에 레이아웃이 새로 잡히면
        // 첫 프레임이 튄다. 그룹이 없는 구성에서는 종전대로 상시 표시가 된다.
        if (skipGaugeFill != null) skipGaugeFill.fillAmount = 0f;
        if (skipGaugeGroup != null) skipGaugeGroup.alpha = 0f;
        if (skipGaugeRoot != null) skipGaugeRoot.SetActive(true);

        // 화면 확대는 거리 대신 FOV로 — 영상이 표시되는 동안만 줄이고 종료 경로에서 원복한다.
        ApplyVideoFov(player);

        _finished = false;
        ApplyVolume();
        _player.Play();
        if (PauseService.IsPaused) _player.Pause();

        // 다음 편이 있으면 재생과 겹쳐 미리 연다 — 편 사이 전환 갭을 줄이는 더블 버퍼.
        PrepareStandby(_activeIndex + 1);

        var held = 0f;
        var gaugeAlpha = 0f;
        while (this != null && !_finished)
        {
            // 변경된 소리를 즉시 미리 듣기 (F-002 2.3, CutsceneVideoSurface 선례).
            ApplyVolume();

            // APIOnly 모드의 출력 텍스처는 재생 중 교체될 수 있다. 매 프레임 참조만 맞춰 둔다.
            if (videoImage != null && _player != null && videoImage.texture != _player.texture)
                videoImage.texture = _player.texture;

            // 일시정지 중에는 게이지도 멈춘다 (CutsceneDirector.TickSkipInput 선례).
            if (!PauseService.IsPaused)
            {
                var holding = ReadSkip(player);
                held = holding ? held + Time.unscaledDeltaTime : 0f;
                if (skipGaugeFill != null) skipGaugeFill.fillAmount = Mathf.Clamp01(held / skipHold);

                // 게이지는 스킵 키를 누르는 동안에만 보인다. 영상 위에 상시로 띄우면 연출을 가린다.
                // 홀드 자체가 unscaled로 누적되므로 페이드도 같은 시간축을 쓴다.
                var fade = holding ? SkipGaugeFadeInSeconds : SkipGaugeFadeOutSeconds;
                gaugeAlpha = Mathf.MoveTowards(gaugeAlpha, holding ? 1f : 0f,
                    fade <= 0f ? 1f : Time.unscaledDeltaTime / fade);
                if (skipGaugeGroup != null) skipGaugeGroup.alpha = gaugeAlpha;

                if (held >= skipHold) break;
            }

            await Task.Yield();
        }

        return true;
    }

    /// <summary>
    /// 데스크톱(Space 2초 유지)과 XR이 같은 명령을 쓴다. Skip은 잠금 대상이 아니라 입력 잠금 중에도
    /// 살아 있다 (<see cref="PlayerCommands.Skip"/>).
    /// </summary>
    static bool ReadSkip(Player player) =>
        player != null && player.Input != null && player.Input.Commands.Skip;

    /// <summary>
    /// 영상 시청 동안만 카메라 FOV를 줄여 화면을 확대한다. HMD의 FOV는 광학계가 정하는 고정값이라
    /// XR에서는 생략하고 로그만 남긴다 (ISSUE-028의 시점 회전 생략과 같은 정책,
    /// <see cref="TurnToTargetAsync"/> 선례). 카메라를 못 찾으면 확대 없이 진행한다.
    /// </summary>
    void ApplyVideoFov(Player player)
    {
        if (XRSettings.isDeviceActive)
        {
            Debug.Log("[StartPrologue] XR 장치가 활성이라 영상 FOV 확대를 생략합니다.");
            return;
        }

        var head = player != null ? player.Head : null;
        var camera = head != null ? head.GetComponentInChildren<Camera>() : null;
        if (camera == null)
        {
            Debug.LogWarning("[StartPrologue] 플레이어 카메라를 찾지 못해 영상 FOV 확대를 생략합니다.");
            return;
        }

        _videoCamera = camera;
        _originalFieldOfView = camera.fieldOfView;
        _fovApplied = true;
        _ = TweenFovAsync(camera, videoFieldOfView);
    }

    /// <summary>
    /// 영상 종료·건너뛰기·오류(<see cref="HideSurface"/>)와 파괴(<see cref="OnDestroy"/>)의 공통
    /// 원복. instant면 보간 없이 즉시 되돌린다 — 파괴 중에는 비동기 루프를 새로 돌릴 수 없다.
    /// </summary>
    void RestoreVideoFov(bool instant)
    {
        if (!_fovApplied) return;
        _fovApplied = false;

        if (_videoCamera == null) return;

        if (instant)
        {
            _fovTweenVersion++; // 진행 중인 보간이 있으면 중단만 시킨다.
            _videoCamera.fieldOfView = _originalFieldOfView;
        }
        else
        {
            _ = TweenFovAsync(_videoCamera, _originalFieldOfView);
        }

        _videoCamera = null;
    }

    /// <summary>
    /// FOV 보간. 시청 연출의 일부일 뿐 게임 시간과 무관하므로 unscaled 시간으로 진행한다
    /// (일시정지 중에도 멈추지 않는다). 새 보간이 시작되면 세대가 갱신되어 이전 루프는 끝난다.
    /// </summary>
    async Task TweenFovAsync(Camera camera, float target)
    {
        var version = ++_fovTweenVersion;
        var from = camera.fieldOfView;
        var elapsed = 0f;

        while (camera != null && version == _fovTweenVersion)
        {
            // 원복 보간 도중 이 컴포넌트가 파괴되면 끝값으로 스냅해 카메라를 어중간한 FOV로
            // 남기지 않는다. 확대 보간 중의 파괴는 OnDestroy의 즉시 원복이 세대를 올려 먼저
            // 끊으므로 여기 오지 않는다.
            if (this == null)
            {
                camera.fieldOfView = target;
                break;
            }

            var t = fovTweenSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fovTweenSeconds);
            camera.fieldOfView = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) break;

            await Task.Yield();
            elapsed += Time.unscaledDeltaTime;
        }
    }

    /// <summary>
    /// cutscene.json의 prologue 항목에서 영상 목록과 건너뛰기 홀드 시간을 읽는다. 분할된 영상은
    /// videos 목록으로, 단일 영상은 1개짜리 목록으로 돌아온다(<see cref="CutsceneDefinition.VideoList"/>).
    /// 영상 파일 구성이 바뀌어도 여기는 손댈 것이 없다. 못 읽으면 직렬화된 폴백을 쓴다.
    /// </summary>
    void ResolveCutsceneData(out IReadOnlyList<string> paths, out float skipHold)
    {
        paths = new[] { fallbackVideoPath };
        skipHold = fallbackSkipHoldSeconds;

        if (!DataManager.HasInstance || DataManager.Instance.Static == null) return;
        if (!DataManager.Instance.Static.TryGet<CutsceneTable>(CutsceneDirector.DataKey, out var table) ||
            table == null)
            return;

        table.Prepare();
        if (table.Settings != null) skipHold = table.Settings.SkipHoldSeconds;
        if (table.TryGet(PrologueCutsceneId, out var definition) && definition.HasVideo)
            paths = definition.VideoList;
    }

    void EnsurePlayer()
    {
        if (_player != null) return;
        (_player, _audio) = CreatePlayer();
    }

    /// <summary>대기 플레이어는 다편 재생이 실제로 필요해질 때만 만든다. 단일 영상이 대부분이다.</summary>
    void EnsureStandbyPlayer()
    {
        if (_standby != null) return;
        (_standby, _standbyAudio) = CreatePlayer();
    }

    (VideoPlayer player, AudioSource audio) CreatePlayer()
    {
        var audio = gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;

        // ignoreListenerPause는 기본값 그대로. 일시정지하면 소리도 함께 멎는다.
        audio.spatialBlend = 0f;

        var player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.waitForFirstFrame = true;
        player.source = VideoSource.Url;

        // APIOnly는 VideoPlayer가 자기 출력 텍스처를 들고 있어 RenderTexture 관리가 필요 없다.
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.skipOnDrop = true;

        player.loopPointReached += OnLoopPointReached;
        player.errorReceived += OnErrorReceived;
        return (player, audio);
    }

    /// <summary>
    /// 첫 편을 첫 프레임까지 연다.
    ///
    /// <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>: 없으면 <c>TrySetResult</c>가
    /// 대기 중인 이어짓기를 **그 자리에서** 실행해, 재생 시작·표면 활성화·대기 플레이어 생성이 전부
    /// VideoPlayer의 <c>prepareCompleted</c> 네이티브 콜백 안에서 벌어진다. 그 콜백 안에서
    /// <c>AddComponent&lt;VideoPlayer&gt;</c>와 <c>Prepare</c>를 다시 부르는 것은 재진입이므로,
    /// 이어짓기를 다음 메인 루프 틱으로 미뤄 콜백 밖에서 돌게 한다.
    ///
    /// 준비가 오류도 완료도 없이 멎으면 <see cref="RunAsync"/>가 입력 잠금을 쥔 채 영원히 대기해
    /// 게임을 진행할 수 없다. 제한 시간을 두어 그 경우에도 로그를 남기고 빠져나온다.
    /// </summary>
    async Task<bool> PrepareAsync(string url)
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _preparation = pending;

        OpenSource(_player, _audio, url);
        _player.prepareCompleted += OnPrepareCompleted;
        _player.Prepare();

        var finished = await Task.WhenAny(
            pending.Task, Task.Delay(TimeSpan.FromSeconds(PrepareTimeoutSeconds)));

        if (finished == pending.Task) return pending.Task.Result;

        Debug.LogError($"[StartPrologue] 영상 준비가 {PrepareTimeoutSeconds}초 안에 끝나지 않았습니다: {url}");
        if (_player != null) _player.prepareCompleted -= OnPrepareCompleted;
        CompletePreparation(false);
        return false;
    }

    /// <summary>
    /// 소스와 오디오를 플레이어에 건다. 트랙 활성화는 Prepare 이후에는 반영되지 않고, 트랙 수는
    /// 준비가 끝나야 알 수 있다. 첫 트랙만 미리 잡고 나머지는 준비 완료 시점에 맞춘다.
    /// </summary>
    static void OpenSource(VideoPlayer player, AudioSource audio, string url)
    {
        player.url = url;
        player.controlledAudioTrackCount = 1;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, audio);
    }

    /// <summary>목록의 index 편을 대기 플레이어에 미리 연다. 남은 편이 없으면 대기를 비운다.</summary>
    void PrepareStandby(int index)
    {
        _standbyPrepared = false;
        _standbyIndex = -1;
        if (index >= _playlist.Count) return;

        EnsureStandbyPlayer();
        _standbyIndex = index;
        OpenSource(_standby, _standbyAudio, ResolveUrl(_playlist[index]));
        _standby.prepareCompleted += OnStandbyPrepareCompleted;
        _standby.Prepare();
    }

    void OnStandbyPrepareCompleted(VideoPlayer player)
    {
        player.prepareCompleted -= OnStandbyPrepareCompleted;

        // 종료 이후 뒤늦게 도착한 완료. 역할 교대 뒤라면 _standby가 아니므로 함께 걸러진다.
        if (player != _standby || _standbyIndex < 0) return;

        for (ushort track = 0; track < _standby.controlledAudioTrackCount; track++)
            _standby.SetTargetAudioSource(track, _standbyAudio);

        _standbyPrepared = true;

        // 현재 편이 먼저 끝나 기다리던 중이었으면 즉시 잇는다.
        if (_waitingForStandby) SwitchToStandby();
    }

    /// <summary>
    /// 편 전환. 두 플레이어의 역할을 맞바꾸고 화면 참조만 새 플레이어로 옮긴다 — 다음 편은 이미
    /// 첫 프레임까지 준비되어 있으므로 전환 갭은 Play 호출 지연 정도로 끝난다.
    /// </summary>
    void SwitchToStandby()
    {
        _waitingForStandby = false;

        (_player, _standby) = (_standby, _player);
        (_audio, _standbyAudio) = (_standbyAudio, _audio);
        _activeIndex = _standbyIndex;

        // 끝난 편은 즉시 내린다. 이 플레이어가 이어서 다음다음 편의 준비를 맡는다.
        _standby.Stop();

        // 해상도가 편마다 다를 수 있다. 레터박스 비율과 텍스처 참조를 새 플레이어로 맞춘다.
        if (videoFitter != null && _player.width > 0 && _player.height > 0)
            videoFitter.aspectRatio = (float)_player.width / (float)_player.height;
        if (videoImage != null) videoImage.texture = _player.texture;

        ApplyVolume();
        _player.Play();
        if (PauseService.IsPaused) _player.Pause();

        PrepareStandby(_activeIndex + 1);
    }

    void OnPrepareCompleted(VideoPlayer player)
    {
        _player.prepareCompleted -= OnPrepareCompleted;

        for (ushort track = 0; track < _player.controlledAudioTrackCount; track++)
            _player.SetTargetAudioSource(track, _audio);

        CompletePreparation(true);
    }

    void OnErrorReceived(VideoPlayer player, string message)
    {
        Debug.LogError($"[StartPrologue] 영상 재생 오류: {message} ({player.url})");

        if (player == _player)
        {
            // 준비 중이면 실패로 닫고, 재생 중이면 끝난 것으로 본다 — 재생하지 못한 영상 때문에
            // 프롤로그가 입력 잠금을 쥔 채 멈춰 있으면 게임을 진행할 수 없다.
            if (!CompletePreparation(false)) _finished = true;
            return;
        }

        // 다음 편 준비 실패. 오류로 끝났으면 prepareCompleted는 오지 않으므로 직접 내리고, 그 편은
        // 건너뛰어 그다음 편을 시도한다 — 남은 편이 없는데 현재 편이 이미 끝나 기다리는 중이었다면
        // 여기서 프롤로그를 닫는다.
        player.prepareCompleted -= OnStandbyPrepareCompleted;
        var waiting = _waitingForStandby;
        PrepareStandby(_standbyIndex + 1);
        if (_standbyIndex < 0 && waiting) _finished = true;
    }

    bool CompletePreparation(bool result)
    {
        var pending = _preparation;
        if (pending == null) return false;

        _preparation = null;
        pending.TrySetResult(result);
        return true;
    }

    void OnLoopPointReached(VideoPlayer player)
    {
        // 역할 교대 직후 옛 활성 플레이어에서 늦게 도착하는 통지는 무시한다.
        if (player != _player) return;

        // 다음 편이 없으면 여기가 끝. 준비돼 있으면 곧장 잇고, 아직이면 마지막 프레임을 띄운 채
        // 준비 완료를 기다린다 (OnStandbyPrepareCompleted가 이어 준다).
        if (_standbyIndex < 0) _finished = true;
        else if (_standbyPrepared) SwitchToStandby();
        else _waitingForStandby = true;
    }

    /// <summary>영상 볼륨 (F-002 2.2, ISSUE-020). VIDEO 버스를 지나 마스터 볼륨·뮤트가 함께 걸린다.</summary>
    void ApplyVolume()
    {
        var volume = AudioBusVolume.Resolve(Core.Audio.AudioBus.Video);
        if (_audio != null) _audio.volume = volume;
        if (_standbyAudio != null) _standbyAudio.volume = volume;
    }

    /// <summary>
    /// AudioListener.pause는 소리만 멈추고 영상 프레임은 계속 넘어간다. 재생 자체를 세우지 않으면
    /// 메뉴를 닫았을 때 그림과 소리가 어긋난다 (CutsceneVideoSurface 선례).
    /// </summary>
    void OnPauseChanged(bool paused)
    {
        if (!IsRunning || _player == null) return;

        // 다음 편을 기다리는 동안에는 Play를 다시 걸지 않는다 — 끝 지점의 플레이어를 재개하면
        // loopPointReached가 중복으로 온다. 전환은 준비 완료 통지가 잇는다.
        if (paused) _player.Pause();
        else if (!_finished && !_waitingForStandby && _player.isPrepared) _player.Play();
    }

    void HideSurface()
    {
        RestoreVideoFov(instant: false);

        ResetPlayer(_player);
        ResetPlayer(_standby);

        _playlist.Clear();
        _activeIndex = 0;
        _standbyIndex = -1;
        _standbyPrepared = false;
        _waitingForStandby = false;

        if (videoImage != null) videoImage.texture = null;
        if (skipGaugeFill != null) skipGaugeFill.fillAmount = 0f;
        if (skipGaugeGroup != null) skipGaugeGroup.alpha = 0f;
        if (skipGaugeRoot != null) skipGaugeRoot.SetActive(false);
        if (videoGroup != null) videoGroup.SetActive(false);
    }

    void ResetPlayer(VideoPlayer player)
    {
        if (player == null) return;

        player.prepareCompleted -= OnPrepareCompleted;
        player.prepareCompleted -= OnStandbyPrepareCompleted;
        player.Stop();
    }

    void OnDestroy()
    {
        RestoreVideoFov(instant: true);

        if (_startMenu != null && _startMenu.NewGameHandler == _handler)
            _startMenu.NewGameHandler = null;

        CleanupPlayer(_player);
        CleanupPlayer(_standby);

        CompletePreparation(false);

        _inputLock?.Dispose();
        _inputLock = null;
    }

    void CleanupPlayer(VideoPlayer player)
    {
        if (player == null) return;

        player.prepareCompleted -= OnPrepareCompleted;
        player.prepareCompleted -= OnStandbyPrepareCompleted;
        player.loopPointReached -= OnLoopPointReached;
        player.errorReceived -= OnErrorReceived;
    }

    /// <summary>
    /// StreamingAssets 기준 상대 경로를 절대 경로로 바꾼다. 스킴이 붙어 있으면 그대로 쓴다
    /// (<see cref="CutsceneVideoSurface"/>와 같은 규칙 — Android의 jar 경로 때문에 Path.Combine을
    /// 쓰지 않는다).
    /// </summary>
    static string ResolveUrl(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        if (source.Contains("://")) return source;

        var relative = source.Replace('\\', '/').TrimStart('/');
        return $"{Application.streamingAssetsPath}/{relative}";
    }
}
