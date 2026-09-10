using UnityEngine;

public class HammerTool : MonoBehaviour, IWoodTool
{
    public ToolType GetToolType() => ToolType.Hammer;
    public bool IsActive() => true;

    [Header("Hammer Settings")]
    // 주의: 이 값은 m/s가 아니라 '월드 유닛/초'로 비교된다(currentSpeed가 월드 이동량 기반).
    // Play 씬은 1m ≈ 12유닛이라 3유닛/초 = 인체감 0.25m/s로, 걷기(20유닛/초)만으로도 충분히 넘는다.
    // 배율을 다시 곱하지 않는 이유는 망치의 lossyScale이 ×300이라(작업장 ×20 × 프리팹 ×15)
    // 곱하는 순간 어떤 조작으로도 넘길 수 없는 문턱이 되기 때문이다.
    [Tooltip("유효한 타격으로 인정될 최소 속도 (월드 유닛/초)")]
    public float minHitSpeed = 1.0f;

    [Header("터널링 방지 스윕")]
    // 종전에는 반경 0.05f·최소 이동 0.01f가 하드코딩되어 있었다. 둘 다 월드 거리라 ×20 작업장에서
    // 각각 인체감 4mm·0.8mm가 되어 스윕이 사실상 무효였고, 게다가 시작점이 망치의 그립 피벗이라
    // 머리(실제 타격부)가 지나간 경로를 훑지도 못했다. 반경은 망치 콜라이더 크기에서 자동 산출하고
    // 스윕 원점도 콜라이더 중심으로 옮겨 두 문제를 함께 없앤다. AdzeTool.tipCheckRadius가 같은
    // 이유로 보정된 것과 같은 조치다.
    [Tooltip("스윕 반경(월드 유닛). 0이면 망치 콜라이더 크기에서 자동 산출한다.")]
    public float sweepRadius = 0f;

    [Header("데스크톱 대체 조작")]
    // 데스크톱에서는 두 손 앵커가 머리에 고정된 채 좌우로 6유닛 떨어져 있어(PlayerInputModule의
    // 손 포즈 고정) 망치를 끌 쪽으로 가져가는 동작 자체가 성립하지 않는다. 그래서 '쥔 상태에서
    // 상호작용 키'를 한 번의 타격으로 해석한다 — 키보드·마우스로도 모든 핵심 흐름이 돌아야 한다는
    // 개발 규칙에 맞춘 대체 경로다. VR에서도 팔 길이 안의 끌을 칠 수 있어 해가 없다.
    [Tooltip("쥔 상태에서 상호작용(데스크톱 R)을 누르면 사거리 안의 끌을 한 번 친다.")]
    public bool allowManualStrike = true;

    [Tooltip("수동 타격 사거리(월드 유닛). 0이면 스윕 반경의 8배를 쓴다. " +
             "데스크톱 두 손 간격(약 6유닛)보다 커야 한 손에 끌, 한 손에 망치를 든 자세가 성립한다.")]
    public float manualStrikeRange = 0f;

    private Vector3 lastPosition;
    private float currentSpeed;

    /// <summary>콜라이더 중심의 로컬 오프셋. 스윕 원점을 그립 피벗이 아니라 망치 머리로 옮긴다.</summary>
    private Vector3 headOffsetLocal;

    private float resolvedSweepRadius;
    private Grabbable grabbable;

    /// <summary>수동 타격 중에만 0보다 크다. 그동안 <see cref="GetCurrentSpeed"/>가 이 값을 돌려준다.</summary>
    private float manualStrikeSpeed;

    /// <summary>스윕·수동 타격의 기준점. 망치 콜라이더 중심의 월드 좌표다.</summary>
    private Vector3 HeadPosition => transform.TransformPoint(headOffsetLocal);

    private float ManualStrikeRange =>
        manualStrikeRange > 0f ? manualStrikeRange : resolvedSweepRadius * 8f;

    private void Start()
    {
        ResolveHeadAndRadius();
        lastPosition = HeadPosition;

        grabbable = GetComponent<Grabbable>();
        if (grabbable != null) grabbable.Activated += OnActivated;
    }

    private void OnDestroy()
    {
        if (grabbable != null) grabbable.Activated -= OnActivated;
    }

    /// <summary>
    /// 망치의 타격부 위치와 스윕 반경을 콜라이더에서 뽑는다. 프리팹 배율이 씬마다 달라도(Play는
    /// lossyScale ≈ 300) 스윕이 실제 머리 크기를 따라가게 하기 위해서다.
    /// </summary>
    private void ResolveHeadAndRadius()
    {
        var colliders = GetComponentsInChildren<Collider>(true);
        var found = false;
        var bounds = new Bounds(transform.position, Vector3.zero);

        foreach (var col in colliders)
        {
            if (col == null) continue;

            if (!found)
            {
                bounds = col.bounds;
                found = true;
                continue;
            }

            bounds.Encapsulate(col.bounds);
        }

        headOffsetLocal = found ? transform.InverseTransformPoint(bounds.center) : Vector3.zero;

        if (sweepRadius > 0f)
        {
            resolvedSweepRadius = sweepRadius;
            return;
        }

        var extents = found ? bounds.extents : Vector3.zero;
        var largest = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
        resolvedSweepRadius = largest > 0.0001f ? largest : 0.05f;
    }

    private void FixedUpdate()
    {
        var head = HeadPosition;

        // 프레임 간 이동 거리를 통해 현재 속력 계산
        Vector3 delta = head - lastPosition;
        float distance = delta.magnitude;
        currentSpeed = distance / Time.fixedDeltaTime;

        // 터널링(빠른 스윙 시 충돌 무시 현상) 방지 스윕. 문턱은 반경에 비례시켜 배율과 무관하게
        // "제자리에 가까운 프레임"만 걸러 낸다.
        if (distance > resolvedSweepRadius * 0.1f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                lastPosition, resolvedSweepRadius, delta.normalized, distance,
                Physics.AllLayers, QueryTriggerInteraction.Collide);

            foreach (var hit in hits)
            {
                ChiselTool chisel = hit.collider.GetComponentInParent<ChiselTool>();
                if (chisel != null)
                {
                    chisel.ProcessHit(this);
                }
            }
        }

        lastPosition = head;
    }

    /// <summary>
    /// 상호작용 입력 한 번을 타격 한 번으로 해석한다. 실제 스윙 속도와 무관하게
    /// <see cref="minHitSpeed"/>를 만족시키되, 마침 빠르게 움직이는 중이면 그 속도를 그대로 쓴다
    /// (깊이가 속도에 비례하는 기존 규칙 유지).
    /// </summary>
    private void OnActivated(Grabbable source)
    {
        if (!allowManualStrike) return;

        var origin = HeadPosition;
        var range = ManualStrikeRange;

        ChiselTool target = null;
        var minSqrDistance = float.MaxValue;

        var colliders = Physics.OverlapSphere(
            origin, range, Physics.AllLayers, QueryTriggerInteraction.Collide);

        foreach (var col in colliders)
        {
            if (col == null) continue;

            var chisel = col.GetComponentInParent<ChiselTool>();
            if (chisel == null) continue;

            var sqrDistance = (chisel.transform.position - origin).sqrMagnitude;
            if (sqrDistance >= minSqrDistance) continue;

            minSqrDistance = sqrDistance;
            target = chisel;
        }

        if (target == null)
        {
            Debug.Log($"[HammerTool] 사거리({range:F1}유닛) 안에 끌이 없어 타격을 건너뜁니다.", this);
            return;
        }

        manualStrikeSpeed = Mathf.Max(minHitSpeed, currentSpeed);
        try
        {
            target.ProcessHit(this);
        }
        finally
        {
            manualStrikeSpeed = 0f;
        }
    }

    public float GetCurrentSpeed()
    {
        return manualStrikeSpeed > 0f ? manualStrikeSpeed : currentSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        var radius = resolvedSweepRadius > 0f ? resolvedSweepRadius : sweepRadius;
        if (radius <= 0f) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(HeadPosition, radius);
    }
}
