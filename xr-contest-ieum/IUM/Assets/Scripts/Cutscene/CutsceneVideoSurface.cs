using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// mp4 컷씬의 재생 표면. <see cref="CutsceneDirector"/>가 필요할 때 한 번 만들어 계속 재사용한다.
///
/// 인게임 컷씬은 씬을 겹쳐 올려 <see cref="CutsceneStage"/>가 연출하지만, 영상 컷씬에는 연출할 씬
/// 오브젝트가 없다. 영상 한 편마다 빈 씬과 Build Settings 항목을 만드는 대신, cutscene.json의
/// video 한 줄로 끝나도록 재생 수단을 하나 더 연 것이 이 클래스다.
///
/// 여러 파일로 분할된 영상(<see cref="CutsceneDefinition.Videos"/>)은 목록 순서대로 이어 재생한다.
/// 플레이어를 두 개 두어 한 편이 나가는 동안 다음 편을 미리 열어 두고(더블 버퍼), 편이 끝나는
/// 순간 화면 참조만 바꿔 전환 갭을 최소화한다.
///
/// Screen Space Overlay를 쓰지 않는 이유는 <see cref="ScreenFader"/>와 같다 — XR 렌더 타깃으로
/// 신뢰할 수 없다. 정렬 순서는 페이더보다 낮게 두어, 진입·종료 암전과 건너뛰기 페이드가 영상을
/// 정상적으로 덮는다. UIToolkit으로 그리는 캡션과 건너뛰기 게이지는 두 캔버스보다 위에 남는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CutsceneVideoSurface : MonoBehaviour
{
    /// <summary>ScreenFader(5000)보다 아래. 암전이 영상을 덮어야 한다.</summary>
    const int SortingOrder = 4000;

    const float CameraPlaneOffset = 0.06f;

    Canvas _canvas;
    RawImage _image;
    AspectRatioFitter _fitter;
    Material _overlayMaterial;

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

    TaskCompletionSource<bool> _preparation;

    /// <summary>True from <see cref="Play"/> until <see cref="Stop"/>. 종료 판정 주체를 가른다.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// 재생할 영상이 열려 있는지. 표면은 컷씬 사이에 살아남으므로, 영상 없는 컷씬이 뒤따를 때
    /// 호출자가 이걸 보고 <see cref="Play"/>를 건너뛴다.
    /// </summary>
    public bool IsPrepared => _player != null && _player.isPrepared;

    /// <summary>
    /// 재생이 끝났는지. 오류로 끝난 경우에도 true다 — 재생하지 못한 영상 때문에 컷씬이 입력 잠금을
    /// 쥔 채 멈춰 있으면 게임을 진행할 수 없다.
    /// </summary>
    public bool IsFinished { get; private set; }

    void Awake()
    {
        BuildCanvas();
        BuildPlayer();
        SetVisible(false);
    }

    void OnEnable() => PauseService.Changed += OnPauseChanged;

    void OnDisable() => PauseService.Changed -= OnPauseChanged;

    /// <summary>
    /// 카메라 재바인딩. 컷씬이 카메라를 교체하거나 씬이 바뀌면 붙어 있던 카메라가 꺼지고, 그대로
    /// 두면 캔버스가 아무것도 그리지 않는다 (ISSUE-012와 같은 사유).
    /// </summary>
    void LateUpdate()
    {
        if (!IsActive) return;

        // APIOnly 모드의 출력 텍스처는 재생 중에 교체될 수 있다. 매 프레임 참조만 맞춰 둔다.
        if (_image != null && _player != null && _image.texture != _player.texture)
            _image.texture = _player.texture;

        // 변경된 소리를 즉시 미리 듣기 (F-002 2.3). 컷씬 중에도 일시정지와 옵션 진입은 허용되므로
        // 재생 시작 시점에 한 번 읽는 것으로는 재생 중의 슬라이더 조작이 반영되지 않는다. 볼륨이
        // 아직 버스를 타지 않아 통지받을 곳이 없으므로 여기서 다시 읽는다 (ISSUE-015).
        ApplyVolume();

        if (_canvas == null) return;
        if (_canvas.worldCamera != null && _canvas.worldCamera.isActiveAndEnabled) return;
        BindCamera();
    }

    void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.sortingOrder = SortingOrder;

        // 영상 비율과 화면 비율이 다를 때 남는 여백. 없으면 그 자리에 게임 화면이 그대로 비친다.
        var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        background.transform.SetParent(transform, false);
        var backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = Color.black;
        backgroundImage.raycastTarget = false;
        Stretch(backgroundImage.rectTransform);

        var frame = new GameObject("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        frame.transform.SetParent(transform, false);
        _image = frame.GetComponent<RawImage>();
        _image.raycastTarget = false;
        Stretch(_image.rectTransform);

        // 레터박스. 영상 해상도를 알게 되는 준비 완료 시점에 비율을 넣는다.
        _fitter = frame.AddComponent<AspectRatioFitter>();
        _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        _fitter.aspectRatio = 16f / 9f;

        ApplyOverlayMaterial(backgroundImage);

        BindCamera();
    }

    /// <summary>
    /// 씬 깊이를 무시하는 머티리얼. Screen Space Camera 캔버스의 uGUI 기본 머티리얼은 씬 깊이와
    /// LEqual z-테스트를 하므로, planeDistance(near+0.06)보다 카메라에 가까운 오브젝트(손, 쥔
    /// 도구)가 영상을 뚫고 나온다. ZTest를 Always로 바꾸면 씬 깊이와 무관하게 화면 전체를 덮는다.
    /// 페이더(정렬 5000)가 영상(4000)을 덮는 관계는 캔버스 정렬 순서라 영향받지 않는다.
    /// </summary>
    void ApplyOverlayMaterial(Image backgroundImage)
    {
        var shader = Shader.Find("UI/Default");
        if (shader == null)
        {
            // 없으면 종전 동작(z-테스트 유지)으로 그린다. 영상이 아예 안 나오는 것보다 낫다.
            Debug.LogWarning("[Cutscene] UI/Default 셰이더를 찾지 못해 영상이 씬 깊이에 가려질 수 있습니다.");
            return;
        }

        _overlayMaterial = new Material(shader);
        _overlayMaterial.SetInt("unity_GUIZTestMode",
            (int)UnityEngine.Rendering.CompareFunction.Always);

        backgroundImage.material = _overlayMaterial;
        _image.material = _overlayMaterial;
    }

    void BuildPlayer() => (_player, _audio) = CreatePlayer();

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

        // ignoreListenerPause는 기본값 그대로 둔다. PauseService가 AudioListener.pause를 켜므로
        // 일시정지하면 소리도 함께 멎는다.
        audio.spatialBlend = 0f;

        var player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.waitForFirstFrame = true;
        player.source = VideoSource.Url;

        // RenderTexture를 직접 잡지 않는다. APIOnly는 VideoPlayer가 자기 출력 텍스처를 들고 있어
        // 해상도별 할당과 해제를 관리할 필요가 없다.
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.skipOnDrop = true;

        player.loopPointReached += OnLoopPointReached;
        player.errorReceived += OnErrorReceived;
        return (player, audio);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void BindCamera()
    {
        if (_canvas == null) return;
        var camera = ResolveCamera();
        if (camera == null) return;

        _canvas.worldCamera = camera;

        // 페이더보다 카메라에서 살짝 멀리. 같은 평면에 두면 어느 쪽이 앞인지 정렬 순서만으로
        // 결정되지 않는다.
        _canvas.planeDistance = Mathf.Max(camera.nearClipPlane + CameraPlaneOffset, 0.1f);
    }

    /// <summary>
    /// <see cref="ScreenFader.ResolveCamera"/>와 같은 규칙. Camera.main은 MainCamera 태그가 붙은
    /// 활성 카메라만 돌려주므로, 컷씬이 주 카메라를 꺼둔 동안에는 null이 된다.
    /// </summary>
    static Camera ResolveCamera()
    {
        var main = Camera.main;
        if (main != null) return main;

        var cameras = Camera.allCameras;
        Camera last = null;
        for (var i = 0; i < cameras.Length; i++)
            if (last == null || cameras[i].depth > last.depth)
                last = cameras[i];

        return last;
    }

    /// <summary>단일 영상 재생. 1개짜리 목록으로 취급한다 — video_test 등 종전 호출 경로.</summary>
    public Task<bool> PrepareAsync(string source) => PrepareAsync(new[] { source });

    /// <summary>
    /// 영상 목록을 열고 첫 편의 첫 프레임까지 준비한다. 나머지 편은 재생과 겹쳐 대기 플레이어가
    /// 미리 연다. 준비에 실패해도 예외를 던지지 않고 false를 돌려준다 — 호출자가 컷씬을 접을지 씬
    /// 연출만으로 이어갈지 정한다.
    /// </summary>
    public Task<bool> PrepareAsync(IReadOnlyList<string> sources)
    {
        Stop();

        if (sources != null)
            for (var i = 0; i < sources.Count; i++)
                if (!string.IsNullOrWhiteSpace(sources[i]))
                    _playlist.Add(sources[i]);

        if (_playlist.Count == 0)
        {
            Debug.LogWarning("[Cutscene] 영상 경로가 비어 있습니다.");
            return Task.FromResult(false);
        }

        IsFinished = false;
        _activeIndex = 0;

        // RunContinuationsAsynchronously: 없으면 TrySetResult가 대기 중인 이어짓기를 그 자리에서
        // 실행해, 호출자의 Play()와 그 안의 대기 플레이어 생성(AddComponent<VideoPlayer>)·Prepare가
        // VideoPlayer의 prepareCompleted 네이티브 콜백 안에서 재진입으로 벌어진다. 이어짓기를 다음
        // 메인 루프 틱으로 미뤄 콜백 밖에서 돌게 한다.
        _preparation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        OpenSource(_player, _audio, _playlist[0]);
        _player.prepareCompleted += OnPrepareCompleted;
        _player.Prepare();

        return _preparation.Task;
    }

    /// <summary>
    /// 소스와 오디오를 플레이어에 건다. 트랙 활성화는 Prepare 이후에는 반영되지 않고, 트랙 수는
    /// 준비가 끝나야 알 수 있다. 첫 트랙만 미리 잡고 나머지는 준비 완료 시점에 맞춘다.
    /// </summary>
    static void OpenSource(VideoPlayer player, AudioSource audio, string source)
    {
        player.url = ResolveUrl(source);
        player.controlledAudioTrackCount = 1;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, audio);
    }

    /// <summary>
    /// StreamingAssets 기준 상대 경로를 절대 경로로 바꾼다. 이미 스킴이 붙어 있으면 그대로 쓴다.
    ///
    /// Path.Combine을 쓰지 않는 이유: Android에서 streamingAssetsPath는 APK 안을 가리키는
    /// `jar:file://...!/assets` 형태이고, Windows 에디터의 Path.Combine은 역슬래시를 넣는다. URL로
    /// 넘길 문자열이므로 구분자를 직접 맞춘다.
    /// </summary>
    static string ResolveUrl(string source)
    {
        if (source.Contains("://")) return source;

        var relative = source.Replace('\\', '/').TrimStart('/');
        return $"{Application.streamingAssetsPath}/{relative}";
    }

    void OnPrepareCompleted(VideoPlayer player)
    {
        _player.prepareCompleted -= OnPrepareCompleted;

        var width = (int)_player.width;
        var height = (int)_player.height;
        if (width <= 0 || height <= 0)
        {
            Debug.LogError($"[Cutscene] 영상 해상도를 읽지 못했습니다: {_player.url}");
            CompletePreparation(false);
            return;
        }

        // 화면 비율과의 차이는 AspectRatioFitter가 여백으로 처리한다.
        _image.texture = _player.texture;
        _fitter.aspectRatio = (float)width / height;

        for (ushort track = 0; track < _player.controlledAudioTrackCount; track++)
            _player.SetTargetAudioSource(track, _audio);

        CompletePreparation(true);
    }

    void OnErrorReceived(VideoPlayer player, string message)
    {
        Debug.LogError($"[Cutscene] 영상 재생 오류: {message} ({player.url})");

        if (player == _player)
        {
            // 준비 중이었다면 실패로 닫고, 재생 중이었다면 끝난 것으로 처리해 컷씬이 빠져나가게 한다.
            if (!CompletePreparation(false)) IsFinished = true;
            return;
        }

        // 다음 편 준비 실패. 오류로 끝났으면 prepareCompleted는 오지 않으므로 직접 내리고, 그 편은
        // 건너뛰어 그다음 편을 시도한다 — 남은 편이 없는데 현재 편이 이미 끝나 기다리는 중이었다면
        // 여기서 컷씬을 닫는다.
        player.prepareCompleted -= OnStandbyPrepareCompleted;
        var waiting = _waitingForStandby;
        PrepareStandby(_standbyIndex + 1);
        if (_standbyIndex < 0 && waiting) IsFinished = true;
    }

    /// <summary>준비 대기를 닫는다. 대기 중이 아니었으면 false.</summary>
    bool CompletePreparation(bool result)
    {
        var pending = _preparation;
        if (pending == null) return false;

        _preparation = null;
        pending.TrySetResult(result);
        return true;
    }

    /// <summary>준비된 영상을 재생하고 화면에 띄운다.</summary>
    public void Play()
    {
        if (_player == null || !_player.isPrepared)
        {
            Debug.LogWarning("[Cutscene] 준비되지 않은 영상을 재생하려 했습니다.");
            IsFinished = true;
            return;
        }

        IsActive = true;
        IsFinished = false;

        ApplyVolume();
        BindCamera();
        SetVisible(true);

        _player.Play();

        // 일시정지 중에 컷씬이 시작되는 경로는 없어야 하지만, 있더라도 첫 프레임만 세워 둔다.
        if (PauseService.IsPaused) _player.Pause();

        // 다음 편이 있으면 재생과 겹쳐 미리 연다 — 편 사이 전환 갭을 줄이는 더블 버퍼.
        PrepareStandby(_activeIndex + 1);
    }

    /// <summary>목록의 index 편을 대기 플레이어에 미리 연다. 남은 편이 없으면 대기를 비운다.</summary>
    void PrepareStandby(int index)
    {
        _standbyPrepared = false;
        _standbyIndex = -1;
        if (index >= _playlist.Count) return;

        EnsureStandbyPlayer();
        _standbyIndex = index;
        OpenSource(_standby, _standbyAudio, _playlist[index]);
        _standby.prepareCompleted += OnStandbyPrepareCompleted;
        _standby.Prepare();
    }

    void OnStandbyPrepareCompleted(VideoPlayer player)
    {
        player.prepareCompleted -= OnStandbyPrepareCompleted;

        // Stop() 이후 뒤늦게 도착한 완료. 역할 교대 뒤라면 _standby가 아니므로 함께 걸러진다.
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
        if (_player.width > 0 && _player.height > 0)
            _fitter.aspectRatio = (float)_player.width / _player.height;
        _image.texture = _player.texture;

        ApplyVolume();
        _player.Play();
        if (PauseService.IsPaused) _player.Pause();

        PrepareStandby(_activeIndex + 1);
    }

    /// <summary>
    /// 재생을 멈추고 화면에서 내린다. 여러 번 불러도 안전하다 — 종료·건너뛰기·중단 경로가 모두
    /// 이걸 지난다.
    /// </summary>
    public void Stop()
    {
        CompletePreparation(false);

        ResetPlayer(_player);
        ResetPlayer(_standby);

        _playlist.Clear();
        _activeIndex = 0;
        _standbyIndex = -1;
        _standbyPrepared = false;
        _waitingForStandby = false;

        if (_image != null) _image.texture = null;

        IsActive = false;
        IsFinished = false;
        SetVisible(false);
    }

    void ResetPlayer(VideoPlayer player)
    {
        if (player == null) return;

        player.prepareCompleted -= OnPrepareCompleted;
        player.prepareCompleted -= OnStandbyPrepareCompleted;
        player.Stop();
        player.url = string.Empty;
    }

    /// <summary>
    /// 영상 볼륨을 적용한다 (F-002 2.2). mp4는 배경음악·내레이션·효과음이 하나로 믹스되어 있어
    /// 나머지 세 분류로 나눌 수 없다. 쪼갤 수 없으므로 분류를 하나 늘렸다 (ISSUE-020).
    ///
    /// VIDEO 버스를 지난다 (ISSUE-015). 마스터 볼륨과 뮤트가 함께 걸린다.
    /// </summary>
    public void ApplyVolume()
    {
        var volume = AudioBusVolume.Resolve(Core.Audio.AudioBus.Video);
        if (_audio != null) _audio.volume = volume;
        if (_standbyAudio != null) _standbyAudio.volume = volume;
    }

    void SetVisible(bool value)
    {
        if (_canvas != null) _canvas.enabled = value;
    }

    /// <summary>
    /// 일시정지 대응. AudioListener.pause가 소리는 멈추지만 영상 프레임은 계속 넘어가므로,
    /// 재생 자체를 세우지 않으면 메뉴를 닫았을 때 그림과 소리가 어긋난다.
    /// </summary>
    void OnPauseChanged(bool paused)
    {
        if (!IsActive || _player == null) return;

        // 다음 편을 기다리는 동안에는 Play를 다시 걸지 않는다 — 끝 지점의 플레이어를 재개하면
        // loopPointReached가 중복으로 온다. 전환은 준비 완료 통지가 잇는다.
        if (paused) _player.Pause();
        else if (!IsFinished && !_waitingForStandby) _player.Play();
    }

    void OnLoopPointReached(VideoPlayer player)
    {
        // 역할 교대 직후 옛 활성 플레이어에서 늦게 도착하는 통지는 무시한다.
        if (player != _player) return;

        // 다음 편이 없으면 여기가 끝. 준비돼 있으면 곧장 잇고, 아직이면 마지막 프레임을 띄운 채
        // 준비 완료를 기다린다 (OnStandbyPrepareCompleted가 이어 준다).
        if (_standbyIndex < 0) IsFinished = true;
        else if (_standbyPrepared) SwitchToStandby();
        else _waitingForStandby = true;
    }

    void OnDestroy()
    {
        CleanupPlayer(_player);
        CleanupPlayer(_standby);

        CompletePreparation(false);

        if (_overlayMaterial != null)
        {
            Destroy(_overlayMaterial);
            _overlayMaterial = null;
        }
    }

    void CleanupPlayer(VideoPlayer player)
    {
        if (player == null) return;

        player.prepareCompleted -= OnPrepareCompleted;
        player.prepareCompleted -= OnStandbyPrepareCompleted;
        player.loopPointReached -= OnLoopPointReached;
        player.errorReceived -= OnErrorReceived;
    }
}
