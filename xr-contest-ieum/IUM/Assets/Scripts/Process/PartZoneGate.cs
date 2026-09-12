using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 부재(Part) 루트에 붙어 자식 존 사이의 선행 관계를 강제한다. 선행 존(<see cref="GateRule.prerequisiteZoneName"/>)이
/// 완료되기 전까지 의존 존들의 Collider를 꺼서 도구 작업을 차단하고, 완료 이벤트가 오면 다시 켠다.
/// 규칙은 이름 문자열로 직렬화한다 — 존들이 팀원 소유 프리팹·씬 계층 안에 있어 직접 참조를 빌더가
/// 채우면 프리팹 인스턴스 오버라이드가 커지고, 이름 기준이면 <see cref="PlayWorkshopBuilder"/>가
/// 재이식해도 규칙 데이터가 그대로 유효하기 때문이다.
///
/// 잠금 수단이 <c>Collider.enabled = false</c>인 근거 — 도구 5종의 존 감지가 전부 물리 질의라
/// 비활성 콜라이더에는 닿지 않는다(각 도구 코드에서 확인).
/// <list type="bullet">
/// <item>SawTool·FlatPlaneTool·CurvedPlaneTool: 도구 쪽 OnTriggerEnter/Exit — 꺼진 존 콜라이더는 트리거 이벤트를 만들지 않는다.</item>
/// <item>ChiselTool: <c>Physics.OverlapSphere</c> — 비활성 콜라이더는 결과에 포함되지 않는다.</item>
/// <item>AdzeTool: <c>Physics.SphereCastAll</c> + OnTriggerEnter — 마찬가지로 차단된다.</item>
/// <item>InkLineTool: OverlapSphere를 쓰지만 먹매김 존은 게이트 대상이 아니다.</item>
/// </list>
/// GameObject.SetActive(false)를 쓰지 않는 이유: 콜라이더만으로 전 도구가 차단됨을 확인했고,
/// 존을 끄면 <see cref="WorkZone"/>.Start의 isTrigger 세팅과 woodModifier 폴백이 활성화 시점까지
/// 미뤄지는 부작용이 생긴다. 이벤트 구독도 살아 있는 오브젝트 쪽이 단순하다.
///
/// 체인(A→B, B→C)은 규칙 두 개로 자동 성립한다 — B가 잠겨 있어도 B의 완료 이벤트 구독은
/// 콜라이더와 무관하게 동작하므로, 각 규칙이 독립적으로 잠그고 푼다. 한 존이 여러 규칙의 의존
/// 존이면 모든 선행이 완료돼야 풀린다. 이미 완료된 선행(저장 복원 등)은 Start에서
/// <see cref="WorkZone.IsCompleted"/>를 확인해 처음부터 잠그지 않는다.
///
/// 잠금 사유는 둘이다. 선행 규칙(위)과 <b>부재 단위 활성 플래그</b>(<see cref="SetPartActive"/>)이며,
/// 하나라도 잠금이면 존은 잠긴다. 뒤엣것은 <see cref="MainPlayProcessBridge"/>가 "지금 단계의 부재가
/// 아니다"를 알릴 때 쓴다 — 잠그는 주체를 게이트 하나로 모아, 규칙이 풀어 준 존을 브릿지가 다시
/// 잠그거나 그 반대가 되는 순서 문제를 없앤다.
///
/// 잠금 상태는 정적 집합으로 공개한다(<see cref="IsLocked(WorkZone)"/>) —
/// <see cref="ProcessGuideService"/>가 잠긴 존을 하이라이트에서 제외할 때 쓴다. 게이트는 부재마다
/// 하나씩, 서로 겹치지 않는 자식 존만 다루는 전제라 존 하나를 두 게이트가 잠그는 경우는 없다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PartZoneGate : MonoBehaviour
{
    [Serializable]
    public sealed class GateRule
    {
        [Tooltip("선행 존의 GameObject 이름. 이 부재 아래에서 찾는다.")]
        public string prerequisiteZoneName;

        [Tooltip("선행 존이 완료되기 전까지 잠글 존들의 GameObject 이름.")]
        public string[] dependentZoneNames;
    }

    [Tooltip("선행 → 의존 존 규칙 목록. PlayWorkshopBuilder가 이식 시 채운다.")]
    [SerializeField] List<GateRule> rules = new();

    /// <summary>존 잠금 상태 변화 알림 (zone, locked). 잠글 때 true, 풀 때 false.</summary>
    public static event Action<WorkZone, bool> ZoneLockChanged;

    static readonly HashSet<WorkZone> LockedZones = new();

    public static bool IsLocked(WorkZone zone) => zone != null && LockedZones.Contains(zone);

    public static bool IsLocked(GameObject target) =>
        target != null && target.TryGetComponent<WorkZone>(out var zone) && LockedZones.Contains(zone);

    public IReadOnlyList<GateRule> Rules => rules;

    /// <summary>빌더 주입용. 런타임 시작 후 교체는 지원하지 않는다(Start에서 한 번 적용).</summary>
    public void SetRules(List<GateRule> value) => rules = value ?? new List<GateRule>();

    /// <summary>의존 존 → 아직 완료되지 않은 선행 존들.</summary>
    readonly Dictionary<WorkZone, HashSet<WorkZone>> _pendingByDependent = new();

    /// <summary>이 부재의 모든 존. 부재 단위 잠금은 규칙과 무관하게 전부를 대상으로 한다.</summary>
    readonly List<WorkZone> _zones = new();

    /// <summary>
    /// 부재 단위 활성 여부. <see cref="MainPlayProcessBridge"/>가 "지금 단계의 부재가 아니다"를
    /// 알릴 때 false가 된다. 선행 규칙과 독립적인 두 번째 잠금 사유이며, 둘 중 하나라도 잠금이면
    /// 존은 잠긴 상태다 — 그래서 브릿지가 콜라이더를 직접 만지지 않아도 되고, 게이트가 규칙을
    /// 풀어 준 존을 브릿지가 다시 잠그는 순서 문제도 생기지 않는다.
    /// </summary>
    bool _partActive = true;

    bool _started;

    /// <summary>구독 해제용. 선행 존과 그 존에 단 핸들러의 짝.</summary>
    readonly List<(WorkZone prerequisite, Action<WorkResult> handler)> _subscriptions = new();

    /// <summary>이 게이트가 잠근 존. 파괴 시 정적 집합 정리에 쓴다.</summary>
    readonly HashSet<WorkZone> _lockedByThis = new();

    /// <summary>
    /// 부재 단위 활성 전환. 브릿지가 공정 진입마다 부른다. <see cref="Start"/>보다 먼저 불려도
    /// 값만 기억했다가 Start에서 함께 반영한다.
    /// </summary>
    public void SetPartActive(bool value)
    {
        if (_partActive == value) return;

        _partActive = value;
        if (!_started) return;

        for (var i = 0; i < _zones.Count; i++) ApplyLock(_zones[i]);
    }

    void Start()
    {
        // WorkZone.Start(isTrigger 세팅·woodModifier 폴백)와 같은 프레임의 Start 단계에서 잠근다.
        // 콜라이더 enabled는 그 세팅과 무관하고, 도구 상호작용은 플레이어 입력이 있어야 시작되므로
        // Awake/Start 사이의 순서 경합은 없다.
        var zones = GetComponentsInChildren<WorkZone>(true);

        _zones.Clear();
        _zones.AddRange(zones);

        foreach (var rule in rules)
        {
            if (rule == null) continue;

            var prerequisite = FindZone(zones, rule.prerequisiteZoneName);
            if (prerequisite == null)
            {
                Debug.LogWarning(
                    $"[ZoneGate] '{name}' 아래에서 선행 존 '{rule.prerequisiteZoneName}'을 찾지 못해 규칙을 건너뜁니다.", this);
                continue;
            }

            if (rule.dependentZoneNames == null) continue;

            foreach (var dependentName in rule.dependentZoneNames)
            {
                var dependent = FindZone(zones, dependentName);
                if (dependent == null)
                {
                    Debug.LogWarning(
                        $"[ZoneGate] '{name}' 아래에서 의존 존 '{dependentName}'을 찾지 못해 잠그지 않습니다.", this);
                    continue;
                }

                if (dependent == prerequisite)
                {
                    Debug.LogWarning(
                        $"[ZoneGate] '{dependentName}'이 자기 자신의 선행 존입니다. 무시합니다.", this);
                    continue;
                }

                // 이미 완료된 선행(저장 복원 등)은 잠금 사유가 아니다.
                if (prerequisite.IsCompleted) continue;

                if (!_pendingByDependent.TryGetValue(dependent, out var pending))
                {
                    pending = new HashSet<WorkZone>();
                    _pendingByDependent.Add(dependent, pending);
                }

                pending.Add(prerequisite);
            }
        }

        // 선행 존마다 완료 구독 하나. 잠긴 존이라도 C# 이벤트 구독은 콜라이더와 무관하므로 체인이 성립한다.
        var subscribed = new HashSet<WorkZone>();
        foreach (var pending in _pendingByDependent.Values)
        foreach (var prerequisite in pending)
        {
            if (!subscribed.Add(prerequisite)) continue;

            var captured = prerequisite;
            Action<WorkResult> handler = _ => OnPrerequisiteCompleted(captured);
            captured.OnWorkCompletedEvent += handler;
            _subscriptions.Add((captured, handler));
        }

        _started = true;
        for (var i = 0; i < _zones.Count; i++) ApplyLock(_zones[i]);
    }

    void OnDestroy()
    {
        foreach (var (prerequisite, handler) in _subscriptions)
            if (prerequisite != null)
                prerequisite.OnWorkCompletedEvent -= handler;
        _subscriptions.Clear();

        // 씬 전환·게이트 제거 시 정적 집합에 죽은 참조를 남기지 않는다. 이벤트는 쏘지 않는다 —
        // 잠금 해제가 아니라 게이트 소멸이고, 구독자(가이드)도 대개 같은 씬과 함께 사라진다.
        foreach (var zone in _lockedByThis)
        {
            if (zone != null) SetZoneEnabled(zone, true);
            LockedZones.Remove(zone);
        }

        _lockedByThis.Clear();
        _pendingByDependent.Clear();
        _zones.Clear();
    }

    void OnPrerequisiteCompleted(WorkZone prerequisite)
    {
        foreach (var pair in _pendingByDependent)
        {
            if (!pair.Value.Remove(prerequisite)) continue;
            if (pair.Value.Count == 0) ApplyLock(pair.Key);
        }
    }

    /// <summary>선행 규칙과 부재 활성 상태를 합쳐 하나라도 잠금이면 잠근다.</summary>
    bool ShouldLock(WorkZone zone)
    {
        if (zone == null) return false;
        if (!_partActive) return true;
        return _pendingByDependent.TryGetValue(zone, out var pending) && pending.Count > 0;
    }

    /// <summary>
    /// 존 하나의 잠금 상태를 판정 결과에 맞춘다. 잠금 사유가 둘(규칙·부재)이므로 각각 잠그고 푸는
    /// 대신 계산된 상태를 적용한다 — 두 사유가 겹칠 때 한쪽이 먼저 풀려도 다른 쪽 잠금이 살아 있다.
    /// </summary>
    void ApplyLock(WorkZone zone)
    {
        if (zone == null) return;

        var shouldLock = ShouldLock(zone);
        var locked = _lockedByThis.Contains(zone);
        if (shouldLock == locked) return;

        if (shouldLock)
        {
            if (!LockedZones.Add(zone)) return;
            _lockedByThis.Add(zone);
            SetZoneEnabled(zone, false);
            ZoneLockChanged?.Invoke(zone, true);
            return;
        }

        _lockedByThis.Remove(zone);
        LockedZones.Remove(zone);
        SetZoneEnabled(zone, true);
        ZoneLockChanged?.Invoke(zone, false);
    }

    /// <summary>
    /// 콜라이더를 잠금 상태에 맞춘다. 존 GameObject에 렌더러가 있으면(현재 존들은 MeshFilter뿐이라
    /// 없다) 시각 표시도 함께 끈다 — 잠긴 존이 그려져 있으면 작업 가능한 곳으로 오해하게 된다.
    /// </summary>
    static void SetZoneEnabled(WorkZone zone, bool value)
    {
        foreach (var collider in zone.GetComponents<Collider>())
            collider.enabled = value;

        foreach (var renderer in zone.GetComponents<Renderer>())
            renderer.enabled = value;
    }

    /// <summary>이름으로 자식 존을 찾는다. 앞뒤 공백과 대소문자 차이는 무시한다.</summary>
    static WorkZone FindZone(WorkZone[] zones, string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName)) return null;

        var wanted = zoneName.Trim();
        WorkZone found = null;

        foreach (var zone in zones)
        {
            if (zone == null ||
                !string.Equals(zone.name.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                continue;

            if (found != null)
            {
                Debug.LogWarning(
                    $"[ZoneGate] 이름 '{zoneName}'인 존이 여러 개입니다. 첫 번째 것을 사용합니다.", found);
                break;
            }

            found = zone;
        }

        return found;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        LockedZones.Clear();
        ZoneLockChanged = null;
    }
}
