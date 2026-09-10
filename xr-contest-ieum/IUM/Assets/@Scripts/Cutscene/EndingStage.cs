using UnityEngine;

/// <summary>
/// 엔딩 연출 (F-021 6.3). 복원된 숭례문을 보여주고 노장과 이음이의 마무리 대사를 지난 뒤 최종
/// 문구를 남긴다. 메인 화면 복귀는 cutscene.json의 nextScene이 처리한다.
///
/// 비추는 대상은 두 갈래다. 정상 진행이면 주 씬(Play)에 그대로 서 있는 실물 숭례문
/// (<see cref="BuildingStageDirector"/>가 세운 것)을 궤도 카메라로 담고, 컷씬 씬의 더미
/// 지오메트리는 꺼 버린다. <see cref="SungnyemunBuildStage"/>가 쓰는 것과 같은 방식이며,
/// 팀원 소유인 디렉터는 public 멤버만 읽는다.
///
/// 디렉터가 없거나(컷씬 디버그 직접 재생) 아직 아무것도 지어지지 않아 바운드를 잡을 수 없으면
/// 종전의 더미 연출로 그대로 떨어진다 — 어느 쪽이든 엔딩이 멈추지는 않는다.
///
/// 6.5의 결과 표시(완료한 공정, 공정별 최고 평가)는 아직 넣지 않았다. 표시할 공정 자체가 없어
/// 빈 표가 되기 때문으로, 공정이 생기면 <see cref="ShowResults"/> 자리에 채운다.
/// </summary>
public sealed class EndingStage : CutsceneStage
{
    enum Beat
    {
        /// <summary>복원된 숭례문 모습. 카메라가 천천히 물러나며 전경을 보여준다.</summary>
        Reveal,

        Nojang,
        Ieumi,

        /// <summary>최종 문구 (F-021 6.4).</summary>
        Message,

        Done
    }

    [Header("연출 대상")]
    [Tooltip("복원된 모습을 보여주며 물러나는 카메라 리그.")]
    [SerializeField] Transform cameraRig;

    [Tooltip("리그가 이동할 로컬 오프셋. 카메라 워크가 필요 없으면 0으로 둔다.")]
    [SerializeField] Vector3 cameraTravel = new(0f, 1.5f, -6f);

    [Header("대사")]
    [SerializeField] string nojangSequenceId = "ending_nojang";
    [SerializeField] string ieumiSequenceId = "ending_ieumi";

    [Header("길이")]
    [SerializeField, Min(0f)] float revealSeconds = 4f;
    [SerializeField, Min(0f)] float messageSeconds = 6f;

    [Tooltip("대사가 끝난 뒤 잠시 두는 시간.")]
    [SerializeField, Min(0f)] float lineTailSeconds = 0.8f;

    [Header("실물 숭례문")]
    [Tooltip("끄면 주 씬에 지어진 건물을 찾지 않고 언제나 컷씬 씬의 더미 지오메트리를 쓴다.")]
    [SerializeField] bool useBuiltSungnyemun = true;

    [Tooltip("건물의 정면 방위를 '창고 → 건물' 방향에서 자동 계산한다. 끄면 startYaw를 그대로 쓴다.")]
    [SerializeField] bool faceFromWarehouse = true;

    [Tooltip("정면 기준점. 비우면 warehouseObjectName의 오브젝트를, 그것도 없으면 주 씬의 Player를 쓴다.")]
    [SerializeField] Transform warehouseReference;

    [Tooltip("정면 기준점을 이름으로 찾을 때 쓰는 이름. Play 씬의 작업장 루트다.")]
    [SerializeField] string warehouseObjectName = "WorkshopImport";

    [Tooltip("수동 방위각. 자동 계산이 꺼져 있거나 기준점을 찾지 못하면 이 값을 쓴다. " +
             "0이면 건물 바운드 중심의 -Z 쪽에서 바라본다. 156은 Play 씬의 창고 → 숭례문 방향이다.")]
    [SerializeField] float startYaw = 156f;

    [Tooltip("엔딩 전체에 걸쳐 도는 각도. 정면을 한가운데 두고 좌우로 절반씩 나눠 돈다.")]
    [SerializeField] float orbitDegrees = 30f;

    [Tooltip("궤도가 이 시간에 걸쳐 끝까지 돈다. 엔딩 길이는 대사에 따라 달라지므로 시간으로 잡는다.")]
    [SerializeField, Min(1f)] float orbitSeconds = 45f;

    [Tooltip("건물이 화면에 꽉 차지 않도록 두는 여유. 1이면 바운드가 화면에 딱 맞는 거리다.")]
    [SerializeField, Min(1f)] float distanceScale = 1.15f;

    [Tooltip("궤도가 도는 동안 뒤로 물러나는 비율.")]
    [SerializeField, Range(0f, 0.5f)] float pullBackRatio = 0.08f;

    [Tooltip("궤도가 도는 동안 올라가는 높이. 건물 높이의 절반 기준 비율이다.")]
    [SerializeField, Range(0f, 1f)] float riseRatio = 0.25f;

    /// <summary>
    /// 실물 모드에서 끌 컷씬 씬의 더미 오브젝트 이름. 씬 루트부터 이름으로만 훑으므로 판정은
    /// <see cref="HideDummyGeometry"/>가 씬 소속을 먼저 확인한 뒤에만 이뤄진다 — 주 씬에 같은
    /// 이름이 있어도 절대 꺼지지 않는다.
    /// </summary>
    static readonly string[] DummyNames =
    {
        "Ground", "Gate_Restored", "Gate_Base", "Roof", "Beam_0", "Beam_1", "Beam_2"
    };

    Beat _beat;
    float _beatEndTime;
    float _tailEndTime;
    float _revealElapsed;
    Vector3 _cameraStart;

    // 실물 모드. 바운드에서 한 번 계산해 두고 매 프레임 보간만 한다.
    Camera _orbitCamera;
    Vector3 _orbitCenter;
    float _orbitDistance;
    float _orbitHeight;
    float _orbitRise;
    float _orbitStartTime;

    /// <summary>궤도의 한가운데가 될 방위각. 건물 정면이다.</summary>
    float _frontYaw;

    /// <summary>실물 모드의 최종 문구. 화면 캡션은 VR에서 보이지 않으므로 월드에도 같이 띄운다.</summary>
    WorldCaptionText _worldCaption;

    /// <summary>True면 주 씬의 실물 숭례문을 궤도 카메라로 비추는 중이다.</summary>
    bool IsLive => _orbitCamera != null;

    protected override void OnBegin()
    {
        if (cameraRig != null) _cameraStart = cameraRig.localPosition;

        if (useBuiltSungnyemun) TrySetUpLiveStage();

        ShowResults();
        EnterBeat(Beat.Reveal, revealSeconds);
    }

    /// <summary>
    /// 주 씬에 완성된 숭례문을 찾아 궤도를 잡는다. 하나라도 어긋나면 아무것도 바꾸지 않고
    /// false로 떨어져 더미 연출이 그대로 산다 — 더미를 끄는 것은 궤도가 확정된 뒤다.
    /// </summary>
    bool TrySetUpLiveStage()
    {
        var camera = StageCamera;
        if (camera == null)
        {
            Debug.LogWarning("[Ending] 컷씬 카메라가 없어 더미 연출로 진행합니다.", this);
            return false;
        }

        var director = FindAnyObjectByType<BuildingStageDirector>();
        if (director == null)
        {
            Debug.Log("[Ending] BuildingStageDirector를 찾지 못해 더미 연출로 진행합니다.");
            return false;
        }

        if (!TryGetBuiltBounds(director, out var bounds))
        {
            Debug.Log("[Ending] 지어진 부재가 없어(바운드 없음) 더미 연출로 진행합니다.");
            return false;
        }

        HideDummyGeometry();

        _orbitCamera = camera;
        _orbitCenter = bounds.center;

        var radius = Mathf.Max(bounds.extents.magnitude, 1f);
        var halfFov = Mathf.Max(1f, camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
        _orbitDistance = radius / Mathf.Tan(halfFov) * distanceScale;

        // 눈높이를 중심보다 조금 위로 둬야 지붕이 화면 위로 잘리지 않는다.
        _orbitHeight = _orbitCenter.y + bounds.extents.y * 0.25f;
        _orbitRise = bounds.extents.y * riseRatio;

        _frontYaw = ResolveFrontYaw(_orbitCenter);
        _orbitStartTime = PauseService.Now;

        // 암전이 걷히기 전에 제자리를 잡아 둔다. 한 프레임이라도 원점에서 그려지면 빈 화면이 뜬다.
        DriveOrbit(_orbitStartTime);
        return true;
    }

    /// <summary>
    /// 건물의 <b>정면</b> 방위각. 숭례문 모델 자체에는 앞뒤를 알려 주는 표식이 없으므로 씬 배치에서
    /// 읽어 낸다 — 플레이어가 이 건물을 세운 곳, 즉 창고(작업장)에서 건물을 바라보는 방향이 정면이다
    /// (리드 지시). 각도를 상수로 박지 않는 이유는 건물이나 작업장을 옮기면 곧바로 어긋나기 때문이다.
    ///
    /// <see cref="DriveOrbit"/>의 카메라 오프셋이 <c>Euler(yaw) * (0,0,-d)</c>이므로, 카메라를
    /// 창고 쪽에 놓으려면 오프셋이 (창고 − 건물) 방향이어야 하고 그 조건이 곧
    /// <c>yaw = Atan2(건물 − 창고)</c>다.
    ///
    /// 기준점은 인스펙터 지정 → 이름으로 찾은 작업장 루트 → 주 씬의 플레이어 순으로 찾는다.
    /// 셋 다 없으면(컷씬 디버그 단독 재생 등) <see cref="startYaw"/>를 그대로 쓴다.
    /// </summary>
    float ResolveFrontYaw(Vector3 center)
    {
        if (!faceFromWarehouse) return startYaw;

        var reference = ResolveWarehouse();
        if (reference == null)
        {
            Debug.Log($"[Ending] 창고 기준점을 찾지 못해 startYaw({startYaw:0.#})를 씁니다.");
            return startYaw;
        }

        var direction = center - reference.position;
        direction.y = 0f;

        // 창고와 건물이 사실상 같은 자리면 방향을 정할 수 없다.
        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning($"[Ending] 창고 기준점 '{reference.name}'이 건물 중심과 겹쳐 startYaw를 씁니다.", this);
            return startYaw;
        }

        var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        Debug.Log($"[Ending] 정면 방위 {yaw:0.#}도 — 기준 '{reference.name}' {reference.position:F1} → 건물 {center:F1}");
        return yaw;
    }

    /// <summary>
    /// 창고 기준점. 컷씬 씬 안의 오브젝트는 후보에서 뺀다 — 더미 지오메트리와 이름이 겹치면
    /// 엉뚱한 곳을 정면으로 잡는다.
    /// </summary>
    Transform ResolveWarehouse()
    {
        if (warehouseReference != null) return warehouseReference;

        if (!string.IsNullOrWhiteSpace(warehouseObjectName))
        {
            var named = GameObject.Find(warehouseObjectName);
            if (named != null && named.scene != gameObject.scene) return named.transform;
        }

        var player = FindAnyObjectByType<Player>();
        if (player != null && player.gameObject.scene != gameObject.scene) return player.transform;

        return null;
    }

    /// <summary>
    /// 실제로 서 있는 부재만 담는 바운드. 디렉터는 아직 짓지 않은 부재를 스케일 0으로 숨겨 두므로
    /// 그것까지 넣으면 바운드가 미완성 부분으로 끌려간다. 스케일이 살아 있고 실제로 그려지는
    /// 렌더러만 모으면 "지금 화면에 보이는 건물"이 그대로 잡힌다.
    /// </summary>
    static bool TryGetBuiltBounds(BuildingStageDirector director, out Bounds bounds)
    {
        bounds = default;
        var found = false;

        var stages = director.stages;
        if (stages == null) return false;

        foreach (var stage in stages)
        {
            if (stage == null) continue;
            Encapsulate(stage.groupParents, ref bounds, ref found);
            Encapsulate(stage.individualObjects, ref bounds, ref found);
        }

        // 부재 하나만 잡힌 경우처럼 상자가 사실상 한 점이면 궤도 거리가 무의미해진다.
        return found && bounds.extents.magnitude > 0.01f;
    }

    static void Encapsulate(System.Collections.Generic.List<Transform> sources, ref Bounds bounds, ref bool found)
    {
        if (sources == null) return;

        foreach (var source in sources)
        {
            if (source == null) continue;

            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                var scale = renderer.transform.lossyScale;
                if (scale.sqrMagnitude < 1e-6f) continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }
        }
    }

    /// <summary>
    /// 컷씬 씬의 더미 성문을 끈다. 대상은 이 스테이지가 속한 씬의 루트에서만 훑으므로 주 씬
    /// 오브젝트는 후보에도 오르지 않는다.
    /// </summary>
    void HideDummyGeometry()
    {
        var scene = gameObject.scene;
        if (!scene.IsValid()) return;

        foreach (var root in scene.GetRootGameObjects())
            HideDummyBranch(root.transform);
    }

    static void HideDummyBranch(Transform node)
    {
        foreach (var name in DummyNames)
        {
            if (!string.Equals(node.name, name, System.StringComparison.Ordinal)) continue;

            node.gameObject.SetActive(false);
            return; // 껐으면 그 아래는 볼 필요가 없다.
        }

        for (var i = 0; i < node.childCount; i++)
            HideDummyBranch(node.GetChild(i));
    }

    /// <summary>
    /// 결과 표시 자리 (F-021 6.5). 총점과 순위는 표시하지 않는다는 것이 문서의 요구이므로, 채울
    /// 때도 완료한 공정과 공정별 최고 평가만 나열한다. <see cref="UserProgressData.GetGrade"/>가
    /// 그 값을 이미 들고 있다.
    /// </summary>
    void ShowResults() { }

    void Update()
    {
        if (!ShouldTick) return;

        var now = PauseService.Now;

        // 실물 모드의 궤도는 비트와 무관하게 엔딩 내내 돈다. 더미 모드는 종전대로 Reveal 동안만
        // 리그를 물린다.
        if (IsLive) DriveOrbit(now);
        else if (_beat == Beat.Reveal) DriveCamera();

        switch (_beat)
        {
            case Beat.Reveal:
                if (now >= _beatEndTime) BeginLine(Beat.Nojang, nojangSequenceId);
                break;

            case Beat.Nojang:
                if (IsLineOver(now)) BeginLine(Beat.Ieumi, ieumiSequenceId);
                break;

            case Beat.Ieumi:
                if (IsLineOver(now)) BeginMessage();
                break;

            case Beat.Message:
                if (now >= _beatEndTime) EndEnding();
                break;
        }
    }

    void EnterBeat(Beat beat, float seconds)
    {
        _beat = beat;
        _beatEndTime = PauseService.Now + Mathf.Max(0f, seconds);
    }

    void BeginLine(Beat beat, string sequenceId)
    {
        _beat = beat;
        _tailEndTime = 0f;

        // A missing sequence must not stall the ending, so failure falls through to the tail timer.
        if (!Context.PlayDialogue(sequenceId))
            _tailEndTime = PauseService.Now + lineTailSeconds;
    }

    /// <summary>True once the line has finished and its tail has elapsed.</summary>
    bool IsLineOver(float now)
    {
        if (Context.IsDialoguePlaying)
        {
            _tailEndTime = 0f;
            return false;
        }

        if (_tailEndTime <= 0f)
        {
            _tailEndTime = now + lineTailSeconds;
            return false;
        }

        return now >= _tailEndTime;
    }

    void BeginMessage()
    {
        const string Message = "기술은 기록으로 남을 수 있지만,\n전통은 사람이 이어갈 때 비로소 살아남는다.";

        // 화면 캡션은 그대로 둔다. 데스크톱에서는 이쪽이 정상 경로이고, VR에서는 아무것도 그리지
        // 않으므로 둘을 함께 불러도 겹쳐 보이지 않는다.
        Context.SetCaption(Message);
        ShowWorldMessage(Message);

        EnterBeat(Beat.Message, messageSeconds);
    }

    /// <summary>
    /// 최종 문구를 월드 공간에도 띄운다 (F-021 6.4). 실물 모드에서만 만든다 — 더미 연출은
    /// 종전 그대로 두라는 것이 이번 작업의 범위다.
    ///
    /// 거리는 궤도 반경의 일부로 잡는다. Play 씬은 월드가 ×20이라 "몇 미터 앞"을 상수로 박으면
    /// 눈 안이나 건물 뒤에 놓인다. 반경 대비 비율로 두면 어느 배율에서도 건물보다 확실히 앞이고
    /// 근평면보다는 뒤다.
    /// </summary>
    void ShowWorldMessage(string message)
    {
        if (!IsLive) return;

        if (_worldCaption == null)
        {
            _worldCaption = WorldCaptionText.Create(gameObject, "EndingWorldCaption");
            _worldCaption.SetCamera(_orbitCamera);
            _worldCaption.SetDistance(Mathf.Max(_orbitDistance * 0.12f, _orbitCamera.nearClipPlane * 4f));
            _worldCaption.SetFadeSeconds(Mathf.Clamp(messageSeconds * 0.25f, 0.4f, 2f));
        }

        _worldCaption.Show(message);
    }

    void HideWorldMessage()
    {
        if (_worldCaption != null) _worldCaption.Hide();
    }

    void EndEnding()
    {
        Context.SetCaption(string.Empty);
        HideWorldMessage();
        _beat = Beat.Done;

        // The director fades out and unloads from here, then cutscene.json's nextScene takes the
        // player back to the main menu (F-021 6.3).
        Finish();
    }

    void DriveCamera()
    {
        if (cameraRig == null) return;

        _revealElapsed += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(_revealElapsed / Mathf.Max(0.01f, revealSeconds));
        cameraRig.localPosition = _cameraStart + cameraTravel * Mathf.SmoothStep(0f, 1f, t);
    }

    /// <summary>
    /// 실물 숭례문 둘레를 천천히 도는 궤도. 진행도는 <see cref="PauseService.Now"/> 기준이라
    /// 일시정지 동안 멈춘다. 회전각·상승·후퇴 모두 완만하게 잡았다 — VR에서 시점이 스스로
    /// 움직이는 것은 그 자체로 부담이다.
    ///
    /// 카메라 리그가 아니라 카메라 트랜스폼을 월드로 직접 놓는다. 리그를 움직이면 카메라가
    /// 리그에 대해 물고 있는 로컬 회전(약 8도 내려봄)만큼 조준이 어긋난다.
    /// </summary>
    void DriveOrbit(float now)
    {
        if (_orbitCamera == null) return;

        var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - _orbitStartTime) / Mathf.Max(1f, orbitSeconds)));

        // 정면을 한가운데 둔다. 시작은 정면에서 절반만큼 틀어진 곳, 끝은 반대쪽 절반이다 —
        // 어느 순간에도 시선이 정면에서 orbitDegrees/2 이상 벌어지지 않는다.
        var yaw = _frontYaw + orbitDegrees * (t - 0.5f);
        var distance = _orbitDistance * (1f + pullBackRatio * t);
        var offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -distance);

        var position = new Vector3(
            _orbitCenter.x + offset.x,
            _orbitHeight + _orbitRise * t,
            _orbitCenter.z + offset.z);

        _orbitCamera.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(_orbitCenter - position, Vector3.up));
    }

    protected override void OnCancel()
    {
        Context.SetCaption(string.Empty);
        HideWorldMessage();
    }
}
