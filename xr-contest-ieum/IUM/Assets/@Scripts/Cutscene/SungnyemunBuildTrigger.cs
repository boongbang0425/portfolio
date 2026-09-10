using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 주 씬(Play)의 숭례문 묶음에 붙어, 지정한 퀘스트가 끝나는 순간 건설 컷씬을 걸고 그 밖의 경우에는
/// 건물을 조용히 완성 상태로 되돌린다. 컷씬 씬은 연출만 들고 있고, 건물과
/// <see cref="BuildingStageDirector"/>는 이 씬에 남아 있으므로 두 시점을 아는 쪽은 여기다.
///
/// 어느 퀘스트에서 어느 층이 올라가는지는 <see cref="stageMappings"/>가 정한다. 기본값은 조립
/// 공정(<see cref="ProcessId.GongpoPuzzle"/>) 완료 → 전 층이다. 도리 설치와 공포 조립이 한 공정으로
/// 합쳐지면서 두 시점을 나눌 수 없어졌기 때문이다. 매핑에 없는 퀘스트가 끝날 때는 아무것도 하지
/// 않는다 — 제작 공정이 한 씬에서 이어지므로 완료 이벤트 자체는 공정마다 들어온다. 씬에 남아 있는
/// 옛 매핑은 <see cref="NormalizeMappings"/>가 실행 시점에 같은 형태로 정리한다.
///
/// 재입장 복원이 필요한 이유는 디렉터가 Awake에서 모든 부재를 스케일 0으로 되돌리기 때문이다.
/// 이미 건설을 본 뒤라도 씬을 다시 열면 숭례문이 사라진 채로 시작한다 — 저장된 진행이 매핑의
/// 공정을 지났다면 그 매핑의 스테이지만 연출 없이 즉시 세운다.
///
/// 컷씬을 거는 시점은 <see cref="QuestRuntimeState.Complete"/> 진입 직후다. QuestManager는 컷씬이
/// 재생 중이면 Update를 조기 반환하므로, 이 시점에 컷씬을 걸면 완료 보고(=다음 퀘스트 또는 씬
/// 전환)가 컷씬이 끝날 때까지 미뤄진다. 컷씬 쪽에는 completesProcess를 적지 않는다 — 진행 기록은
/// 지금까지처럼 QuestManager와 GameFlow가 맡는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SungnyemunBuildTrigger : MonoBehaviour
{
    /// <summary>
    /// 한 스테이지가 이 시간 안에 끝나지 않으면 포기한다. 고속 완성은 몇 초면 끝나므로 여기에
    /// 걸린다는 것은 디렉터의 코루틴이 멈췄다는 뜻이고, 그때 무한 대기에 빠지지 않기 위한 것이다.
    /// </summary>
    const float StageTimeoutSeconds = 60f;

    /// <summary>퀘스트 하나와 그 퀘스트가 올리는 층들의 대응.</summary>
    [Serializable]
    public sealed class StageMapping
    {
        [Tooltip("이 공정의 퀘스트가 완료되면 아래 스테이지를 건설한다.")]
        public ProcessId process = ProcessId.GongpoPuzzle;

        [Tooltip("BuildingStageDirector.stages의 인덱스. 여럿이면 적은 순서대로 올린다.")]
        public int[] stages = { 0 };

        /// <summary>
        /// 이 매핑으로 컷씬을 이미 걸었는지. 매핑마다 따로 두는 이유는 제작 공정이 같은 씬에서
        /// 이어지기 때문이다 — 트리거 인스턴스가 살아남으므로 하나짜리 가드로는 두 번째 컷씬이
        /// 막힌다. 직렬화하지 않아 씬을 다시 열면 자연히 풀린다.
        /// </summary>
        [NonSerialized] public bool Requested;
    }

    [Header("컷씬")]
    [Tooltip("퀘스트 완료 시 재생할 컷씬 id. cutscene.json에 있어야 한다.")]
    [SerializeField] string cutsceneId = "sungnyemun_build";

    [Tooltip("퀘스트 완료와 건설 스테이지의 대응. 여기 없는 퀘스트가 끝날 때는 컷씬을 걸지 않는다.")]
    [SerializeField] StageMapping[] stageMappings =
    {
        new() { process = ProcessId.GongpoPuzzle, stages = new[] { 0, 1, 2 } }
    };

    /// <summary>
    /// 이번 컷씬이 올릴 스테이지. 컷씬 씬의 <see cref="SungnyemunBuildStage"/>는 주 씬의 이
    /// 컴포넌트를 직접 참조할 수 없으므로(로드 순서상 씬이 아직 없다) 재생 직전에 여기 놓고
    /// 스테이지가 <see cref="ConsumeRequestedStages"/>로 가져간다. null이면 스테이지는 남은
    /// 층 전부를 연출한다 — 컷씬을 디버그로 직접 재생했을 때의 안전 폴백이다.
    /// </summary>
    static int[] _requestedStages;

    BuildingStageDirector _director;
    QuestManager _quest;
    Coroutine _fastForward;

    /// <summary>컷씬 스테이지가 한 번만 읽어 가는 대상 목록. 읽는 즉시 비운다.</summary>
    public static IReadOnlyList<int> ConsumeRequestedStages()
    {
        var stages = _requestedStages;
        _requestedStages = null;
        return stages;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _requestedStages = null;

    void Awake()
    {
        // 디렉터는 같은 이식 루트 아래에 있다. 배치가 바뀌어도 동작하도록 씬 전체 탐색을 폴백으로 둔다.
        _director = GetComponentInChildren<BuildingStageDirector>(true);
        if (_director == null) _director = FindAnyObjectByType<BuildingStageDirector>();
        if (_director == null)
        {
            Debug.LogWarning("[Sungnyemun] BuildingStageDirector를 찾지 못해 건설 트리거를 끕니다.", this);
            enabled = false;
            return;
        }

        NormalizeMappings();
        WarnOnInvalidMappings();

        _quest = FindAnyObjectByType<QuestManager>();
        if (_quest == null)
        {
            Debug.LogWarning(
                "[Sungnyemun] 씬에 QuestManager가 없어 퀘스트 완료로 건설 컷씬을 걸 수 없습니다. " +
                "복원 동작만 수행합니다.", this);
            return;
        }

        _quest.StateChanged += OnQuestStateChanged;
    }

    void Start() => _ = RestoreAsync();

    /// <summary>
    /// 씬에 직렬화된 매핑을 실행 가능한 형태로 정리한다. 두 가지를 한다.
    ///
    /// <list type="number">
    /// <item>퀘스트로 완료될 수 없는 공정(<see cref="ProcessId.Ending"/> 이상)을 마지막 제작 공정인
    /// <see cref="ProcessId.GongpoPuzzle"/>로 옮긴다. 이 트리거는 퀘스트가
    /// <see cref="QuestRuntimeState.Complete"/>에 드는 순간에만 동작하는데 엔딩에는 퀘스트가 없어
    /// 그 매핑의 층은 영영 올라가지 않는다.</item>
    /// <item>같은 공정을 가리키게 된 매핑들을 하나로 합친다. <see cref="TryGetMapping"/>이 첫
    /// 매핑만 돌려주므로 합치지 않으면 뒤엣것의 층이 사라진다.</item>
    /// </list>
    ///
    /// 공정 단위가 도구에서 부재로 재편되면서 ProcessId의 정수 값이 한 칸씩 당겨졌다. 씬은 공용
    /// 자산이라 이 작업에서 건드리지 않았고, 옛 값(도리 설치=5 · 공포=6)이 새 표에서 각각
    /// 공포 조립·엔딩으로 읽힌다. 위 두 정리를 거치면 결과는 "조립 퀘스트 완료 시 전 층 건설"로
    /// 수렴하며, 도리와 공포가 한 공정에 합쳐진 새 구조에서 그것이 옳은 동작이다.
    /// </summary>
    void NormalizeMappings()
    {
        if (stageMappings == null || stageMappings.Length == 0) return;

        var merged = new List<StageMapping>();

        foreach (var mapping in stageMappings)
        {
            if (mapping == null) continue;

            var process = mapping.process;
            if (process >= ProcessId.Ending)
            {
                Debug.LogWarning(
                    $"[Sungnyemun] 퀘스트로 완료되지 않는 공정 '{process}' 매핑을 " +
                    $"'{ProcessId.GongpoPuzzle}'로 옮깁니다.", this);
                process = ProcessId.GongpoPuzzle;
            }

            var existing = merged.Find(candidate => candidate.process == process);
            if (existing == null)
            {
                merged.Add(new StageMapping { process = process, stages = mapping.stages ?? Array.Empty<int>() });
                continue;
            }

            if (mapping.stages == null) continue;

            var stages = new List<int>(existing.stages ?? Array.Empty<int>());
            foreach (var index in mapping.stages)
                if (!stages.Contains(index))
                    stages.Add(index);

            existing.stages = stages.ToArray();
        }

        stageMappings = merged.ToArray();
    }

    /// <summary>
    /// 인덱스가 어긋난 매핑은 조용히 무시되면 원인을 찾기 어렵다. 층 구성이 바뀌었을 때 바로
    /// 드러나도록 시작 시 한 번만 알린다.
    /// </summary>
    void WarnOnInvalidMappings()
    {
        var count = _director.stages?.Count ?? 0;
        foreach (var mapping in stageMappings)
        {
            if (mapping?.stages == null) continue;

            foreach (var index in mapping.stages)
                if (index < 0 || index >= count)
                    Debug.LogWarning(
                        $"[Sungnyemun] '{mapping.process}' 매핑의 스테이지 인덱스 {index}가 " +
                        $"디렉터의 스테이지 수({count})를 벗어납니다. 이 인덱스는 무시됩니다.", this);
        }
    }

    /// <summary>
    /// 저장된 진행이 이미 지난 공정의 층은 세워 둔 상태로 시작한다. 데이터 계층을 기다리는 이유는
    /// 에디터에서 씬을 바로 실행하면 Start가 사용자 파일 로드보다 한참 앞서기 때문이다.
    /// </summary>
    async Task RestoreAsync()
    {
        try
        {
            await DataManager.Instance.InitializeAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Sungnyemun] 데이터를 준비하지 못해 건설 상태를 복원하지 않습니다: {exception.Message}");
            return;
        }

        if (this == null || _director == null) return;

        var progress = DataManager.Instance.Progress;
        if (progress == null) return;

        var restore = new List<int>();
        foreach (var mapping in stageMappings)
        {
            if (mapping?.stages == null || !progress.IsCompleted(mapping.process)) continue;

            foreach (var index in mapping.stages)
                if (!restore.Contains(index))
                    restore.Add(index);
        }

        if (restore.Count == 0) return;

        FastForwardRemaining(restore);
    }

    void OnQuestStateChanged(QuestRuntimeState state)
    {
        if (state != QuestRuntimeState.Complete || _quest == null) return;
        if (!TryGetMapping(_quest.Process, out var mapping) || mapping.Requested) return;
        if (!HasPendingStage(mapping.stages)) return;

        if (!CutsceneDirector.TryGetInstance(out var director) || !director.IsReady)
        {
            Debug.LogWarning($"[Sungnyemun] 컷씬 재생기가 준비되지 않아 '{cutsceneId}'를 걸지 못했습니다.", this);
            return;
        }

        // 성공 여부와 무관하게 매핑마다 한 번만 시도한다. 실패해도 퀘스트는 그대로 완료로 이어져야
        // 하므로 여기서는 경고만 남긴다 — 연출이 없다고 진행이 막히면 안 된다.
        mapping.Requested = true;
        _requestedStages = mapping.stages;

        if (director.Play(cutsceneId)) return;

        _requestedStages = null;
        Debug.LogWarning($"[Sungnyemun] 건설 컷씬 '{cutsceneId}' 재생에 실패했습니다. 연출 없이 진행합니다.", this);
    }

    bool TryGetMapping(ProcessId process, out StageMapping mapping)
    {
        mapping = null;
        if (stageMappings == null) return false;

        foreach (var candidate in stageMappings)
        {
            if (candidate == null || candidate.process != process) continue;

            mapping = candidate;
            return true;
        }

        return false;
    }

    /// <summary>지정한 인덱스 중 아직 지어지지 않은 층이 하나라도 있는지. null이면 전체를 본다.</summary>
    bool HasPendingStage(IReadOnlyList<int> stages)
    {
        if (_director?.stages == null) return false;

        if (stages == null)
        {
            for (var i = 0; i < _director.stages.Count; i++)
                if (IsPending(i))
                    return true;

            return false;
        }

        for (var i = 0; i < stages.Count; i++)
            if (IsPending(stages[i]))
                return true;

        return false;
    }

    bool IsPending(int index)
    {
        var list = _director?.stages;
        if (list == null || index < 0 || index >= list.Count) return false;

        var stage = list[index];
        return stage != null && !stage.isCompleted;
    }

    /// <summary>
    /// 지정한 층을 연출 없이 고속으로 세운다. <paramref name="stages"/>가 null이면 남은 층 전부가
    /// 대상이다. 재입장 복원과 컷씬 건너뛰기가 같은 결과를 필요로 하므로 한 경로로 모았다. 컷씬
    /// 스테이지가 사라진 뒤에도 끝까지 돌아야 해서 주 씬에 남는 이쪽이 맡는다.
    /// </summary>
    public void FastForwardRemaining(IReadOnlyList<int> stages = null)
    {
        if (_fastForward != null || _director == null) return;

        var targets = ResolveTargets(stages);
        if (targets.Count == 0) return;

        _fastForward = StartCoroutine(FastForwardRoutine(targets));
    }

    /// <summary>대상 인덱스 중 실제로 지을 것만 순서대로 추린다.</summary>
    List<int> ResolveTargets(IReadOnlyList<int> stages)
    {
        var targets = new List<int>();
        if (_director?.stages == null) return targets;

        if (stages == null)
        {
            for (var i = 0; i < _director.stages.Count; i++)
                if (IsPending(i))
                    targets.Add(i);

            return targets;
        }

        for (var i = 0; i < stages.Count; i++)
            if (IsPending(stages[i]) && !targets.Contains(stages[i]))
                targets.Add(stages[i]);

        return targets;
    }

    IEnumerator FastForwardRoutine(List<int> targets)
    {
        _director.delayBetweenInstalls = 0.01f;
        _director.appearDuration = 0.05f;

        foreach (var index in targets)
        {
            var stage = _director.stages[index];
            if (stage == null || stage.isCompleted) continue;

            // 디렉터가 다른 스테이지를 짓고 있으면 이 호출은 경고만 남기고 무시된다. 건너뛰기로
            // 들어온 경우가 그렇고, 그때 진행 중인 것이 바로 이 스테이지이므로 isCompleted를
            // 기다리는 것으로 충분하다.
            _director.BuildStage(index);

            var deadline = Time.realtimeSinceStartup + StageTimeoutSeconds;
            while (!stage.isCompleted)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning(
                        $"[Sungnyemun] 스테이지 '{stage.stageName}' 고속 완성이 끝나지 않아 중단합니다.", this);
                    _fastForward = null;
                    yield break;
                }

                yield return null;
            }
        }

        _fastForward = null;
    }

    void OnDestroy()
    {
        if (_quest != null) _quest.StateChanged -= OnQuestStateChanged;
    }
}
