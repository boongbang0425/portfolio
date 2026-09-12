using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 완료 시 재생하는 숭례문 건설 연출. 컷씬 씬에는 카메라와 이 스크립트만 있고, 실제로
/// 지어지는 건물은 주 씬(Play)에 그대로 남아 있는 <see cref="BuildingStageDirector"/>가 맡는다.
///
/// 한 번에 올릴 층은 <see cref="SungnyemunBuildTrigger"/>가 재생 직전에 지정한다. 도리 설치가
/// 끝나면 1층만, 공포 조립이 끝나면 나머지가 올라가므로 이 스테이지는 매번 "남은 층 전부"를
/// 짓지 않는다. 지정이 없으면(컷씬을 디버그로 직접 재생한 경우) 종전처럼 남은 층 전부를 짓는다.
///
/// 연출 대상을 컷씬 씬으로 복제하지 않는 이유는 두 가지다. 하나는 팀원 소유인 디렉터를 건드리지
/// 않고 public 멤버만으로 몰아갈 수 있다는 것, 다른 하나는 연출이 끝난 뒤 주 씬에 지어진 결과가
/// 그대로 남는다는 것이다 — 컷씬이 내려가도 플레이어 눈앞의 숭례문은 완성된 상태로 유지된다.
///
/// 진행은 코루틴이 아니라 Update 상태 기계로 돈다. <see cref="CutsceneDirector"/>가 같은 이유를
/// 주석으로 남겨 두었듯, 코루틴의 대기는 일시정지를 지나쳐 계속 흐르기 때문이다. 다만 디렉터의
/// 빌드 코루틴 자체는 팀원 코드라 손대지 않았고 <see cref="Time.deltaTime"/>으로 도므로 일시정지
/// 중에도 부재가 계속 올라온다 — 알려진 한계다. 이 스테이지가 멈추는 것은 다음 스테이지 호출과
/// 카메라 이동까지다.
/// </summary>
public sealed class SungnyemunBuildStage : CutsceneStage
{
    enum Phase
    {
        Idle,

        /// <summary>한 스테이지를 호출해 두고 그 스테이지의 isCompleted를 기다리는 중.</summary>
        Building,

        /// <summary>마지막 스테이지가 끝난 뒤의 여운.</summary>
        Tail,

        Done
    }

    /// <summary>
    /// 스테이지 하나가 이 시간 안에 끝나지 않으면 포기하고 다음으로 넘어간다. 디렉터의 코루틴이
    /// 어떤 이유로든 멈추면 컷씬이 영원히 끝나지 않고 입력 잠금을 계속 쥐게 되므로(스테이지가
    /// 끝을 보고하지 않으면 <see cref="CutsceneDirector"/>가 기다리기만 한다) 마지막 방어선을 둔다.
    /// </summary>
    const float StageTimeoutSeconds = 60f;

    [Header("연출 길이")]
    [Tooltip("건설 전체가 이 시간 안팎으로 끝나도록 부재 설치 간격을 역산한다.")]
    [SerializeField, Min(1f)] float targetSeconds = 25f;

    [Tooltip("마지막 부재가 올라간 뒤 완성된 모습을 보여주는 시간.")]
    [SerializeField, Min(0f)] float tailSeconds = 2f;

    [Tooltip("부재 하나가 나타나는 데 걸리는 시간. 디렉터의 appearDuration에 그대로 넣는다.")]
    [SerializeField, Min(0.05f)] float appearDuration = 0.4f;

    [Tooltip("설치 간격의 하한. 부재가 아주 많아도 이보다 촘촘해지지는 않는다.")]
    [SerializeField, Min(0.001f)] float minInstallDelay = 0.01f;

    [Tooltip("설치 간격의 상한. 부재가 적어도 이보다 늘어지지는 않는다.")]
    [SerializeField, Min(0.01f)] float maxInstallDelay = 0.15f;

    [Header("카메라")]
    [Tooltip("시작 방위각. 0이면 건물 바운드 중심의 -Z 쪽에서 바라본다.")]
    [SerializeField] float startYaw = -30f;

    [Tooltip("연출 전체에 걸쳐 도는 각도. 절제된 궤도 이동을 위해 90도 이하로 둔다.")]
    [SerializeField] float orbitDegrees = 75f;

    [Tooltip("건물이 화면에 꽉 차지 않도록 두는 여유. 1이면 바운드가 화면에 딱 맞는 거리다.")]
    [SerializeField, Min(1f)] float distanceScale = 1.15f;

    [Tooltip("첫 건설 재생에서 궤도 거리에 추가로 곱하는 배율. 첫 재생은 부재가 전부 스케일 0이라 " +
        "바운드가 중심점 상자로 무너져 거리가 짧게 잡히므로, 그만큼 더 물러나 원경으로 보정한다.")]
    [SerializeField, Min(1f)] float firstBuildDistanceBoost = 1.5f;

    [Tooltip("연출이 진행되는 동안 뒤로 물러나는 비율.")]
    [SerializeField, Range(0f, 0.5f)] float pullBackRatio = 0.12f;

    [Tooltip("연출이 진행되는 동안 올라가는 높이. 건물 높이의 절반 기준 비율이다.")]
    [SerializeField, Range(0f, 1f)] float riseRatio = 0.35f;

    BuildingStageDirector _director;
    readonly List<int> _pending = new();

    /// <summary>
    /// 이번 재생에서 올릴 층. <see cref="SungnyemunBuildTrigger"/>가 재생 직전에 놓아 둔 값을
    /// 그대로 들고 있다가 건너뛰기 시 같은 대상만 넘긴다. 비어 있으면 남은 층 전부가 대상이다.
    /// </summary>
    readonly List<int> _targets = new();

    Phase _phase = Phase.Idle;
    int _cursor;
    float _stageDeadline;
    float _tailEndTime;

    // 카메라 궤도. 바운드에서 한 번 계산해 두고 매 프레임 보간만 한다.
    Camera _camera;
    Vector3 _orbitCenter;
    float _orbitDistance;
    float _orbitHeight;
    float _orbitRise;
    float _startTime;
    float _plannedSeconds = 1f;

    protected override void OnBegin()
    {
        // 대상 목록은 재생마다 한 번만 놓이므로 디렉터를 찾기 전에 먼저 걷어 간다 — 여기서
        // 중도 반환해도 다음 재생에 남은 값이 새어 나가지 않게 한다.
        _targets.Clear();
        var requested = SungnyemunBuildTrigger.ConsumeRequestedStages();
        if (requested != null) _targets.AddRange(requested);

        _director = FindAnyObjectByType<BuildingStageDirector>();
        if (_director == null)
        {
            // 연출할 대상이 없으면 컷씬을 붙잡아 둘 이유가 없다. 즉시 끝내면 암전만 스치고
            // 퀘스트 흐름은 그대로 이어진다.
            Debug.LogWarning("[Sungnyemun] 씬에서 BuildingStageDirector를 찾지 못해 건설 연출을 건너뜁니다.", this);
            Finish();
            return;
        }

        CollectPending();
        if (_pending.Count == 0)
        {
            Debug.Log("[Sungnyemun] 이번에 올릴 스테이지가 이미 모두 완료되어 건설 연출을 건너뜁니다.");
            Finish();
            return;
        }

        _director.appearDuration = appearDuration;

        _startTime = PauseService.Now;
        SetUpCamera();

        _cursor = 0;
        BeginStage();
    }

    void Update()
    {
        if (!ShouldTick) return;

        var now = PauseService.Now;
        DriveCamera(now);

        switch (_phase)
        {
            case Phase.Building:
                if (_director.stages[_pending[_cursor]].isCompleted)
                {
                    Advance(now);
                    break;
                }

                if (now < _stageDeadline) break;

                Debug.LogWarning(
                    $"[Sungnyemun] 스테이지 {_pending[_cursor]}가 {StageTimeoutSeconds:F0}초 안에 끝나지 않아 " +
                    "다음 단계로 넘어갑니다.", this);
                Advance(now);
                break;

            case Phase.Tail:
                if (now >= _tailEndTime)
                {
                    _phase = Phase.Done;
                    Finish();
                }

                break;
        }
    }

    /// <summary>
    /// 이번에 올릴 스테이지를 순서대로 모은다. 트리거가 대상을 지정했으면 그중 아직 지어지지
    /// 않은 것만, 지정이 없으면(컷씬 직접 재생 등) 남은 층 전부를 대상으로 한다. 이미 지어진 층은
    /// 어느 쪽이든 다시 세우지 않는다.
    /// </summary>
    void CollectPending()
    {
        _pending.Clear();
        var stages = _director.stages;
        if (stages == null) return;

        if (_targets.Count > 0)
        {
            foreach (var index in _targets)
                if (IsPending(stages, index) && !_pending.Contains(index))
                    _pending.Add(index);

            return;
        }

        for (var i = 0; i < stages.Count; i++)
            if (IsPending(stages, i))
                _pending.Add(i);
    }

    static bool IsPending(List<BuildingStageDirector.BuildingStage> stages, int index)
    {
        if (index < 0 || index >= stages.Count) return false;

        var stage = stages[index];
        return stage != null && !stage.isCompleted;
    }

    /// <summary>
    /// 다음 스테이지를 호출한다. 설치 간격은 스테이지마다 다시 계산해서 넣는다 — 층마다 부재 수가
    /// 크게 다르므로, 하나의 간격을 공유하면 부재가 적은 층은 순식간에, 많은 층은 한없이 길어진다.
    /// 디렉터는 코루틴 시작 시점의 delayBetweenInstalls를 캡처하므로 호출 직전에 써 넣으면 된다.
    /// </summary>
    void BeginStage()
    {
        var stageIndex = _pending[_cursor];
        var stage = _director.stages[stageIndex];

        var perStage = targetSeconds / _pending.Count;
        var count = Mathf.Max(1, CountTargets(stage));
        _director.delayBetweenInstalls = Mathf.Clamp(perStage / count, minInstallDelay, maxInstallDelay);

        _director.BuildStage(stageIndex);

        _phase = Phase.Building;
        _stageDeadline = PauseService.Now + StageTimeoutSeconds;
    }

    void Advance(float now)
    {
        _cursor++;
        if (_cursor < _pending.Count)
        {
            BeginStage();
            return;
        }

        _phase = Phase.Tail;
        _tailEndTime = now + tailSeconds;
    }

    /// <summary>
    /// 디렉터가 이 스테이지에서 실제로 연출할 오브젝트 수. 디렉터의 수집 규칙(recursiveSearch)을
    /// 그대로 따라야 예상 시간이 맞는다.
    /// </summary>
    int CountTargets(BuildingStageDirector.BuildingStage stage)
    {
        var count = stage.individualObjects?.Count ?? 0;
        if (stage.groupParents == null) return count;

        foreach (var parent in stage.groupParents)
        {
            if (parent == null) continue;
            count += _director.recursiveSearch
                ? parent.GetComponentsInChildren<Renderer>(true).Length
                : parent.childCount;
        }

        return count;
    }

    /// <summary>
    /// 건물 전체를 담는 궤도를 잡는다. 이 시점의 부재들은 디렉터가 Awake에서 스케일 0으로 숨겨
    /// 둔 상태라 각 렌더러의 바운드가 한 점으로 무너져 있다 — 즉 여기서 나오는 바운드는 부재
    /// 중심점들의 상자이며 실제 외곽보다 부재 하나 크기만큼 작다. 프레이밍 여유
    /// (<see cref="distanceScale"/>)로 흡수할 수 있는 오차이고, 이미 지어진 층은 실제 바운드로
    /// 잡히므로 어느 쪽이든 중심은 맞는다.
    /// </summary>
    void SetUpCamera()
    {
        _camera = StageCamera;
        if (_camera == null) return;

        _plannedSeconds = EstimateSeconds();

        if (!TryGetBuildingBounds(out var bounds))
        {
            Debug.LogWarning("[Sungnyemun] 건물 바운드를 계산하지 못해 카메라를 고정합니다.", this);
            _camera = null;
            return;
        }

        _orbitCenter = bounds.center;

        // 첫 건설(아직 완료된 스테이지가 하나도 없는 재생)은 위 오차가 가장 큰 경우다 — 바운드
        // 전체가 중심점 상자라 distanceScale만으로는 부족하게 가까워지므로 배율을 더 곱해 물러난다.
        // 두 번째 재생부터는 이미 지어진 층이 실제 바운드를 제공해 보정이 필요 없다.
        var boost = IsFirstBuild() ? firstBuildDistanceBoost : 1f;

        var radius = Mathf.Max(bounds.extents.magnitude, 1f);
        var halfFov = Mathf.Max(1f, _camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
        _orbitDistance = radius / Mathf.Tan(halfFov) * distanceScale * boost;

        // 눈높이를 중심보다 조금 위로 둬야 지붕이 화면 위로 잘리지 않는다. 첫 건설 보정 시에는
        // 이 항에도 같은 배율을 곱해 멀어진 만큼 균형을 맞춘다(중심 y는 그대로).
        _orbitHeight = _orbitCenter.y + bounds.extents.y * 0.25f * boost;
        _orbitRise = bounds.extents.y * riseRatio;

        // 첫 프레임부터 제자리를 잡아 둔다. 암전이 걷히기 전에 호출되므로 이동이 보이지는 않지만,
        // 카메라가 원점에 남은 채로 한 프레임이라도 그려지면 아무것도 없는 화면이 뜬다.
        DriveCamera(_startTime);
    }

    /// <summary>계획한 전체 재생 시간. 카메라 이동의 진행도 기준이 된다.</summary>
    float EstimateSeconds()
    {
        var perStage = targetSeconds / _pending.Count;
        var total = tailSeconds;

        foreach (var stageIndex in _pending)
        {
            var count = Mathf.Max(1, CountTargets(_director.stages[stageIndex]));
            var delay = Mathf.Clamp(perStage / count, minInstallDelay, maxInstallDelay);
            total += count * delay + appearDuration;
        }

        return Mathf.Max(1f, total);
    }

    /// <summary>
    /// 완료된 스테이지가 하나도 없으면 첫 건설로 본다. stages가 없거나 비어 있으면 판단 근거가
    /// 없으므로 보정하지 않는 쪽(false)을 택한다.
    /// </summary>
    bool IsFirstBuild()
    {
        var stages = _director.stages;
        if (stages == null || stages.Count == 0) return false;

        foreach (var stage in stages)
            if (stage != null && stage.isCompleted)
                return false;

        return true;
    }

    bool TryGetBuildingBounds(out Bounds bounds)
    {
        bounds = default;
        var found = false;

        foreach (var stage in _director.stages)
        {
            if (stage == null) continue;
            Encapsulate(stage.groupParents, ref bounds, ref found);
            Encapsulate(stage.individualObjects, ref bounds, ref found);
        }

        return found;
    }

    static void Encapsulate(List<Transform> sources, ref Bounds bounds, ref bool found)
    {
        if (sources == null) return;

        foreach (var source in sources)
        {
            if (source == null) continue;

            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
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
    /// 천천히 도는 궤도. 화면이 흐르는 정도만 움직이고 급격한 이동은 하지 않는다 — VR에서 시점이
    /// 스스로 움직이는 것은 그 자체로 부담이라 회전각·상승·후퇴 모두 완만하게 잡았다.
    /// </summary>
    void DriveCamera(float now)
    {
        if (_camera == null) return;

        var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - _startTime) / _plannedSeconds));

        var yaw = startYaw + orbitDegrees * t;
        var distance = _orbitDistance * (1f + pullBackRatio * t);
        var offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -distance);

        var position = new Vector3(
            _orbitCenter.x + offset.x,
            _orbitHeight + _orbitRise * t,
            _orbitCenter.z + offset.z);

        _camera.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(_orbitCenter - position, Vector3.up));
    }

    /// <summary>
    /// 건너뛰기·중단. 남은 층은 주 씬의 <see cref="SungnyemunBuildTrigger"/>가 고속으로 마저 세운다.
    /// 이 스테이지는 곧 씬과 함께 사라지므로 자기 코루틴이나 Update로는 뒷정리를 끝낼 수 없다.
    /// 대상은 연출과 같아야 한다 — 이번 컷씬이 1층만 올리기로 했다면 건너뛰어도 1층까지다.
    ///
    /// 지금 진행 중인 스테이지가 있으면 트리거의 첫 호출은 디렉터가 "빌드 중"이라며 무시하지만,
    /// 트리거가 isCompleted를 기다렸다가 다음 층으로 넘어가므로 결과는 같다.
    /// </summary>
    protected override void OnCancel()
    {
        var trigger = FindAnyObjectByType<SungnyemunBuildTrigger>();
        if (trigger == null)
        {
            Debug.LogWarning(
                "[Sungnyemun] 건너뛰기 후 남은 층을 세울 SungnyemunBuildTrigger를 찾지 못했습니다. " +
                "숭례문이 미완성 상태로 남습니다.", this);
            return;
        }

        trigger.FastForwardRemaining(_targets.Count > 0 ? _targets : null);
    }
}
