using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 숭례문이나 기타 건축 오브젝트를 층별(Stage)로 구성하여 순차적으로 지어지게 만드는 디렉터 스크립트입니다.
/// 각 단계가 트리거될 때 등록된 오브젝트들이 순서대로 뾰로롱(효과음/파티클) 하며 제자리에서 피벗을 보정하여 스케일업 등장합니다.
/// </summary>
public class BuildingStageDirector : MonoBehaviour
{
    public enum SpawnSortOrder
    {
        [Tooltip("하이어라키 창에 나열된 순서 그대로 위에서 아래로 스폰합니다. (가장 직관적이고 직접 편집 가능)")]
        HierarchyOrder,
        [Tooltip("물리적 높이(Y축)가 낮은 곳부터 높은 곳으로 스폰합니다. (바닥 -> 지붕 순서)")]
        BottomToTopY,
        [Tooltip("물리적 높이(Y축)가 높은 곳부터 낮은 곳으로 스폰합니다. (지붕 -> 바닥 순서)")]
        TopToBottomY,
        [Tooltip("계층 구조상 상위 부모 오브젝트가 먼저 스폰되고 하위 자식들이 나중에 스폰됩니다.")]
        DepthAscending,
        [Tooltip("계층 구조상 하위 자식 오브젝트가 먼저 스폰되고 상위 부모가 나중에 스폰됩니다.")]
        DepthDescending
    }

    [System.Serializable]
    public class BuildingStage
    {
        public string stageName;
        
        [Tooltip("이 단계에서 지어질 부재들의 부모 객체들. 부모 밑의 자식들이 지정한 순서대로 나타납니다.")]
        public List<Transform> groupParents = new List<Transform>();

        [Tooltip("부모 자식 관계와 상관없이 개별적으로 등록하여 연출하고 싶은 오브젝트들.")]
        public List<Transform> individualObjects = new List<Transform>();

        [Header("Stage Events")]
        public UnityEvent OnStageStart;
        public UnityEvent OnStageComplete;

        [HideInInspector]
        public bool isCompleted = false;
    }

    [Header("Stage Settings")]
    public List<BuildingStage> stages = new List<BuildingStage>();

    [Header("Animation & Timing")]
    [Tooltip("각 부재가 설치되는 시간 간격 (도로로롱 연출 속도)")]
    public float delayBetweenInstalls = 0.1f;
    [Tooltip("부재 하나가 나타나는 데 걸리는 시간")]
    public float appearDuration = 0.5f;
    [Tooltip("나타날 때의 애니메이션 커브 (스케일 변화)")]
    public AnimationCurve appearCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Scale Center Correction (피벗 보정)")]
    [Tooltip("체크 시 오브젝트의 피벗(기준축)이 구석에 있어도 실제 모델의 정중앙을 기준으로 제자리에서 커집니다. 체크 해제 시 피벗을 기준으로 커집니다.")]
    public bool scaleFromCenter = true;

    [Header("Effects")]
    [Tooltip("설치 시 발생할 파티클 이펙트")]
    public ParticleSystem installEffectPrefab;
    [Tooltip("설치 시 재생할 효과음")]
    public AudioClip installSound;

    [Header("Global Settings")]
    [Tooltip("게임 시작 시 등록된 모든 스테이지 부재들의 크기를 0으로 만들어 자동으로 숨깁니다.")]
    public bool hideOnStart = true;

    [Tooltip("체크 시 그룹 부모 아래의 모든 하위 자식(Renderer가 있는 실제 모델)들을 찾아 하나씩 개별적으로 순차 연출합니다. 체크 해제 시 등록된 부모의 바로 아래 직계 자식들만 순차 연출합니다.")]
    public bool recursiveSearch = true;

    [Tooltip("부재들이 순차적으로 지어지는(스폰되는) 순서를 정렬하는 방식입니다.")]
    public SpawnSortOrder sortOrder = SpawnSortOrder.HierarchyOrder;

    [Tooltip("자동 빌드 진행 시 각 스테이지 빌드 시작 사이의 대기 시간(초)입니다.")]
    public float autoBuildInterval = 10f;

    [Tooltip("게임 시작 시 자동으로 모든 스테이지를 일정 간격으로 지어지게 합니다.")]
    public bool autoBuildOnStart = false;

    private AudioSource audioSource;
    
    // 캐싱을 위한 자료 구조들
    private Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Vector3> originalLocalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> originalLocalRotations = new Dictionary<Transform, Quaternion>();
    
    private Dictionary<Transform, bool> originalColliderStates = new Dictionary<Transform, bool>();
    private Dictionary<Rigidbody, bool> originalKinematicStates = new Dictionary<Rigidbody, bool>();
    private Dictionary<Rigidbody, bool> originalUseGravityStates = new Dictionary<Rigidbody, bool>();

    private int currentActiveStageIndex = -1;
    private Coroutine buildCoroutine;
    private Coroutine autoBuildCoroutine;

    private void Awake()
    {
        // AudioSource 세팅
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        // 모든 스테이지 오브젝트들의 원래 크기/위치를 캐싱하고 시작 시 숨김 처리
        CacheAndInitializeObjects();
    }

    private void Start()
    {
        if (autoBuildOnStart)
        {
            StartAutoBuild();
        }
    }

    private void CacheAndInitializeObjects()
    {
        List<Transform> allTargets = new List<Transform>();

        // 1. 모든 스테이지의 연출 타겟들을 수집
        foreach (var stage in stages)
        {
            if (stage == null) continue;
            
            List<Transform> targets = GetStageTargets(stage);
            
            Debug.Log($"[BuildingStageDirector] '{stage.stageName}' 수집된 타겟 순서 (총 {targets.Count}개, 정렬 방식: {sortOrder}):");
            for (int idx = 0; idx < targets.Count; idx++)
            {
                Debug.Log($"  [{idx}] {targets[idx].name} (Depth: {GetTransformDepth(targets[idx])}, Y: {targets[idx].position.y:F2})");
            }

            foreach (var target in targets)
            {
                if (target != null && !allTargets.Contains(target))
                {
                    allTargets.Add(target);
                }
            }
        }

        // 2. 첫 번째 루프: 모든 오브젝트의 스케일이 정상적일 때 좌표 및 상태를 먼저 캐싱합니다.
        // (스케일이 0이 되기 전에 계산해야 인스펙터 보정 연산이 정확하게 동작합니다!)
        foreach (var target in allTargets)
        {
            if (!originalScales.ContainsKey(target))
            {
                originalScales.Add(target, target.localScale);
            }
            if (!originalLocalPositions.ContainsKey(target))
            {
                originalLocalPositions.Add(target, target.localPosition);
                originalLocalRotations.Add(target, target.localRotation);
            }

            // 물리 컴포넌트 상태 캐싱
            Collider col = target.GetComponent<Collider>();
            if (col != null && !originalColliderStates.ContainsKey(target))
            {
                originalColliderStates.Add(target, col.enabled);
            }

            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null && !originalKinematicStates.ContainsKey(rb))
            {
                originalKinematicStates.Add(rb, rb.isKinematic);
                originalUseGravityStates.Add(rb, rb.useGravity);
            }
        }

        // 3. 두 번째 루프: 실제로 숨김 처리(scale = 0) 및 피벗 보정 위치 설정, 물리 정지를 적용합니다.
        if (hideOnStart)
        {
            foreach (var target in allTargets)
            {
                target.localScale = Vector3.zero;

                if (scaleFromCenter)
                {
                    // 피벗이 구석에 있으면 시작 위치를 센터 보정값으로 세팅
                    Renderer r = target.GetComponent<Renderer>();
                    if (r != null)
                    {
                        Vector3 localCenter = r.localBounds.center;
                        target.localPosition = originalLocalPositions[target] + originalLocalRotations[target] * localCenter;
                    }
                }

                Collider col = target.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                Rigidbody rb = target.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 오브젝트의 계층 구조 깊이(Depth)를 구합니다. (루트노드에 가까울수록 낮은 값)
    /// </summary>
    private int GetTransformDepth(Transform t)
    {
        int depth = 0;
        while (t.parent != null)
        {
            depth++;
            t = t.parent;
        }
        return depth;
    }

    /// <summary>
    /// 설정에 따라 해당 스테이지의 애니메이션 대상들을 수집하고 정렬합니다.
    /// </summary>
    private List<Transform> GetStageTargets(BuildingStage stage)
    {
        List<Transform> targets = new List<Transform>();

        // 1. 개별 등록된 오브젝트 수집
        foreach (var obj in stage.individualObjects)
        {
            if (obj != null && !targets.Contains(obj))
            {
                targets.Add(obj);
            }
        }

        // 2. 그룹 부모 아래의 오브젝트 수집
        foreach (var parent in stage.groupParents)
        {
            if (parent == null) continue;

            if (recursiveSearch)
            {
                // 재귀 탐색: 하위 모든 렌더러가 있는 실제 모델 수집
                Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    Transform t = r.transform;
                    if (t != null && !targets.Contains(t))
                    {
                        targets.Add(t);
                    }
                }
            }
            else
            {
                // 직계 탐색: 바로 밑의 첫 번째 자식 레벨만 수집
                foreach (Transform child in parent)
                {
                    if (child != null && !targets.Contains(child))
                    {
                        targets.Add(child);
                    }
                }
            }
        }

        // 3. 정렬 순서 적용
        switch (sortOrder)
        {
            case SpawnSortOrder.HierarchyOrder:
                // GetComponentsInChildren가 반환하는 깊이 우선 순서(하이어라키에 보이는 순서 그대로 위에서 아래로) 유지
                break;
            case SpawnSortOrder.BottomToTopY:
                // 물리적 Y 높이 기준 낮은 순서부터 (바닥 -> 지붕)
                targets.Sort((a, b) => a.position.y.CompareTo(b.position.y));
                break;
            case SpawnSortOrder.TopToBottomY:
                // 물리적 Y 높이 기준 높은 순서부터 (지붕 -> 바닥)
                targets.Sort((a, b) => b.position.y.CompareTo(a.position.y));
                break;
            case SpawnSortOrder.DepthAscending:
                // 계층 구조 깊이(Depth) 기준 얕은 순서부터 (부모 -> 자식)
                targets.Sort((a, b) => GetTransformDepth(a).CompareTo(GetTransformDepth(b)));
                break;
            case SpawnSortOrder.DepthDescending:
                // 계층 구조 깊이(Depth) 기준 깊은 순서부터 (자식 -> 부모)
                targets.Sort((a, b) => GetTransformDepth(b).CompareTo(GetTransformDepth(a)));
                break;
        }

        return targets;
    }

    /// <summary>
    /// 모든 스테이지를 설정된 시간 간격(기본 10초)으로 1 -> 2 -> 3 순차 빌드하도록 작동시킵니다.
    /// </summary>
    [ContextMenu("Start Auto Build All")]
    public void StartAutoBuild()
    {
        if (autoBuildCoroutine != null)
        {
            Debug.LogWarning("[BuildingStageDirector] 이미 자동 빌드가 진행 중입니다.");
            return;
        }
        autoBuildCoroutine = StartCoroutine(AutoBuildAllStagesRoutine());
    }

    /// <summary>
    /// 진행 중인 자동 순차 빌드를 중단합니다.
    /// </summary>
    [ContextMenu("Stop Auto Build")]
    public void StopAutoBuild()
    {
        if (autoBuildCoroutine != null)
        {
            StopCoroutine(autoBuildCoroutine);
            autoBuildCoroutine = null;
            Debug.Log("[BuildingStageDirector] 자동 빌드가 중단되었습니다.");
        }
    }

    private IEnumerator AutoBuildAllStagesRoutine()
    {
        Debug.Log("[BuildingStageDirector] 10초 간격 순차 자동 빌드를 시작합니다.");
        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i].isCompleted) continue;

            // 현재 스테이지 빌드 호출 (1 -> 2 -> 3 순서)
            BuildStage(i);

            // 해당 스테이지 연출(조립)이 완전히 끝날 때까지 대기
            while (buildCoroutine != null || currentActiveStageIndex == i)
            {
                yield return null;
            }

            // 다음 스테이지로 넘어가기 전 대기시간(10초) 적용 (단, 마지막 단계 완료 후에는 패스)
            if (i < stages.Count - 1)
            {
                Debug.Log($"[BuildingStageDirector] {autoBuildInterval}초 대기 후 다음 단계를 호출합니다...");
                yield return new WaitForSeconds(autoBuildInterval);
            }
        }
        autoBuildCoroutine = null;
        Debug.Log("[BuildingStageDirector] 모든 스테이지 자동 순차 빌드가 완료되었습니다!");
    }

    /// <summary>
    /// 아직 조립되지 않은 다음 단계를 찾아 자동으로 연출을 시작합니다.
    /// </summary>
    [ContextMenu("Build Next Stage")]
    public void BuildNextStage()
    {
        for (int i = 0; i < stages.Count; i++)
        {
            if (!stages[i].isCompleted)
            {
                BuildStage(i);
                return;
            }
        }
        Debug.LogWarning("[BuildingStageDirector] 모든 단계의 조립이 완료되었습니다.");
    }

    /// <summary>
    /// 지정한 인덱스의 스테이지 빌드를 시작합니다. (0부터 시작)
    /// </summary>
    public void BuildStage(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count)
        {
            Debug.LogError($"[BuildingStageDirector] 유효하지 않은 스테이지 인덱스입니다: {stageIndex}");
            return;
        }

        var stage = stages[stageIndex];
        if (stage.isCompleted)
        {
            Debug.LogWarning($"[BuildingStageDirector] 스테이지 '{stage.stageName}' (인덱스: {stageIndex})는 이미 완료되었습니다.");
            return;
        }

        if (buildCoroutine != null)
        {
            Debug.LogWarning("[BuildingStageDirector] 현재 다른 스테이지가 빌드 중입니다. 대기해 주세요.");
            return;
        }

        buildCoroutine = StartCoroutine(BuildStageRoutine(stageIndex));
    }

    // Unity Inspector 등에서 버튼이나 트리거 이벤트로 바인딩하기 쉽게 도우미 함수 제공
    public void BuildStage1() => BuildStage(0);
    public void BuildStage2() => BuildStage(1);
    public void BuildStage3() => BuildStage(2);

    private IEnumerator BuildStageRoutine(int stageIndex)
    {
        currentActiveStageIndex = stageIndex;
        var stage = stages[stageIndex];
        
        Debug.Log($"[BuildingStageDirector] '{stage.stageName}' 빌드 연출 시작!");
        stage.OnStageStart?.Invoke();

        WaitForSeconds wait = new WaitForSeconds(delayBetweenInstalls);

        // 해당 단계에서 지어져야 할 모든 타겟(오브젝트)들을 가져옵니다.
        List<Transform> targets = GetStageTargets(stage);

        // 가져온 타겟들을 순차적으로 연출합니다.
        foreach (var target in targets)
        {
            if (target == null) continue;
            PlayEffectsAndAnimate(target);
            yield return wait;
        }

        // 전체 애니메이션이 완전히 끝날 때까지 대기
        yield return new WaitForSeconds(appearDuration);

        stage.isCompleted = true;
        buildCoroutine = null;
        currentActiveStageIndex = -1;

        Debug.Log($"🎉 [BuildingStageDirector] '{stage.stageName}' 빌드 연출 완료!");
        stage.OnStageComplete?.Invoke();
    }

    private void PlayEffectsAndAnimate(Transform targetTransform)
    {
        // 설치 효과음
        if (installSound != null)
        {
            audioSource.PlayOneShot(installSound);
        }

        // 설치 파티클 이펙트
        if (installEffectPrefab != null)
        {
            Instantiate(installEffectPrefab, targetTransform.position, Quaternion.identity);
        }

        // 스케일 애니메이션 시작
        StartCoroutine(AnimateScale(targetTransform));
    }

    private IEnumerator AnimateScale(Transform targetTransform)
    {
        float time = 0f;
        
        // 캐싱된 원래 스케일 및 로컬 포지션 가져오기
        Vector3 targetScale = Vector3.one;
        originalScales.TryGetValue(targetTransform, out targetScale);

        Vector3 targetLocalPos = Vector3.zero;
        originalLocalPositions.TryGetValue(targetTransform, out targetLocalPos);

        Quaternion targetLocalRot = Quaternion.identity;
        originalLocalRotations.TryGetValue(targetTransform, out targetLocalRot);

        // 메쉬의 로컬 중심점 구하기
        Vector3 localCenter = Vector3.zero;
        if (scaleFromCenter)
        {
            Renderer r = targetTransform.GetComponent<Renderer>();
            if (r != null)
            {
                localCenter = r.localBounds.center;
            }
        }

        targetTransform.localScale = Vector3.zero;
        
        if (scaleFromCenter)
        {
            targetTransform.localPosition = targetLocalPos + targetLocalRot * localCenter;
        }

        // 애니메이션 도중에는 물리를 꺼둡니다.
        Collider col = targetTransform.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Rigidbody rb = targetTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        while (time < appearDuration)
        {
            time += Time.deltaTime;
            float normalizedTime = time / appearDuration;
            float curveValue = appearCurve.Evaluate(normalizedTime);

            targetTransform.localScale = targetScale * curveValue;

            if (scaleFromCenter)
            {
                // 피벗 보정 보간: 중심축이 구석에 있어도 모델 정중앙을 고정하여 스케일되도록 위치 보정
                targetTransform.localPosition = targetLocalPos + targetLocalRot * (localCenter * (1f - curveValue));
            }

            yield return null;
        }

        targetTransform.localScale = targetScale;
        targetTransform.localPosition = targetLocalPos;

        // 원래 물리 상태 복원
        if (col != null && originalColliderStates.TryGetValue(targetTransform, out bool colEnabled))
        {
            col.enabled = colEnabled;
        }
        if (rb != null && originalKinematicStates.TryGetValue(rb, out bool isKinematic))
        {
            rb.isKinematic = isKinematic;
            if (originalUseGravityStates.TryGetValue(rb, out bool useGravity))
            {
                rb.useGravity = useGravity;
            }
        }
    }
}
