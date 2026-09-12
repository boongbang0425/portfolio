using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>부재 단계에서 존 하나가 요구하는 작업 종류. 존 타입(과 대패의 도구 종류)으로 정해진다.</summary>
public enum PartCraft
{
    None,
    Ink,
    Saw,
    Adze,
    FlatPlane,
    CurvedPlane,
    Chisel
}

/// <summary>도구 잠금을 켤 대상. 목표 하나가 여러 도구를 요구할 수 있어 비트 조합으로 둔다.</summary>
[Flags]
public enum PartToolMask
{
    None = 0,
    Ink = 1 << 0,
    Saw = 1 << 1,
    FlatPlane = 1 << 2,
    CurvedPlane = 1 << 3,
    Adze = 1 << 4,
    Chisel = 1 << 5,
    Hammer = 1 << 6
}

/// <summary>
/// 부재 단계(<see cref="ProcessId.ShortPart"/>·<see cref="ProcessId.MediumPart"/>·
/// <see cref="ProcessId.LongPart"/>)와 조립 단계의 신호 키 표. 브릿지와 시각 가이드가 같은 판정을
/// 쓰도록 한곳에 모았다.
///
/// 키를 공정×작업 종류로 쪼갠 이유는 누계 계산을 없애기 위해서다. 옛 구조는 한 공정이 신호 키
/// 하나를 나눠 써서 분할 목표의 amount를 누계(1 → 2)로 적어야 했지만, 지금은 목표마다 키가 달라
/// <c>amount</c>가 곧 그 부재의 해당 존 개수다. 예외는 <see cref="GongpoAssembled"/> 하나로, 공포
/// 조립만 한 키를 세 목표(1 → 18 → 37)가 나눠 쓴다.
///
/// 파일을 따로 두지 않고 브릿지와 같은 파일에 둔 것은 새 자산(.cs + .meta)을 늘리지 않기 위해서다.
/// 둘 다 Assembly-CSharp이라 <see cref="ProcessGuideService"/>에서도 그대로 보인다.
/// </summary>
public static class PartProcessSignals
{
    public const string ShortInk = "main.short.ink";
    public const string ShortSaw = "main.short.saw";
    public const string ShortPlane = "main.short.plane";
    public const string ShortChisel = "main.short.chisel";

    public const string MediumInk = "main.medium.ink";
    public const string MediumSaw = "main.medium.saw";
    public const string MediumAdze = "main.medium.adze";
    public const string MediumPlane = "main.medium.plane";
    public const string MediumCurved = "main.medium.curved";
    public const string MediumChisel = "main.medium.chisel";

    public const string LongInk = "main.long.ink";
    public const string LongSaw = "main.long.saw";
    public const string LongAdze = "main.long.adze";
    public const string LongPlane = "main.long.plane";
    public const string LongChisel = "main.long.chisel";

    public const string PurlinInstall = "main.purlin.install";
    public const string GongpoAssembled = "main.gongpo.assembled";

    /// <summary>부재 루트 이름. 씬 계층에서 존이 어느 부재 소속인지 판정하는 폴백 기준이다.</summary>
    public const string ShortPartRootName = "ShortPart";
    public const string MediumPartRootName = "MediumPart";
    public const string LongPartRootName = "LongPart";

    static readonly string[] AllSignals =
    {
        ShortInk, ShortSaw, ShortPlane, ShortChisel,
        MediumInk, MediumSaw, MediumAdze, MediumPlane, MediumCurved, MediumChisel,
        LongInk, LongSaw, LongAdze, LongPlane, LongChisel,
        PurlinInstall, GongpoAssembled
    };

    static readonly string[] ShortSignals = { ShortInk, ShortSaw, ShortPlane, ShortChisel };

    static readonly string[] MediumSignals =
        { MediumInk, MediumSaw, MediumAdze, MediumPlane, MediumCurved, MediumChisel };

    static readonly string[] LongSignals = { LongInk, LongSaw, LongAdze, LongPlane, LongChisel };

    static readonly string[] AssemblySignals = { PurlinInstall, GongpoAssembled };

    static readonly string[] NoSignals = Array.Empty<string>();

    static readonly ProcessId[] SignalProcesses =
        { ProcessId.ShortPart, ProcessId.MediumPart, ProcessId.LongPart, ProcessId.GongpoPuzzle };

    public static IReadOnlyList<string> All => AllSignals;

    /// <summary>부재를 가공하는 단계인지. 조립 단계는 제외한다.</summary>
    public static bool IsPartProcess(ProcessId process) =>
        process is ProcessId.ShortPart or ProcessId.MediumPart or ProcessId.LongPart;

    /// <summary>브릿지가 도구·부재 접근을 통제하는 구간. 그 밖은 원래 상태로 둔다.</summary>
    public static bool IsProductionProcess(ProcessId process) =>
        process is >= ProcessId.ShortPart and <= ProcessId.GongpoPuzzle;

    /// <summary>한 공정이 쓰는 신호 키 전부. 진입·재시작 시 초기화 대상이기도 하다.</summary>
    public static IReadOnlyList<string> SignalsForProcess(ProcessId process) => process switch
    {
        ProcessId.ShortPart => ShortSignals,
        ProcessId.MediumPart => MediumSignals,
        ProcessId.LongPart => LongSignals,
        ProcessId.GongpoPuzzle => AssemblySignals,
        _ => NoSignals
    };

    /// <summary>공정의 첫 신호 키. 데이터 계약 검증이 공정↔신호 대응을 확인할 때 쓴다.</summary>
    public static string SignalForProcess(ProcessId process)
    {
        var signals = SignalsForProcess(process);
        return signals.Count > 0 ? signals[0] : null;
    }

    /// <summary>
    /// 존이 요구하는 작업 종류. 대패 존은 한 타입이 두 도구(평대패·배대패)로 갈리므로
    /// <see cref="WorkZone.requiredToolType"/>로 구분한다 — 이름 규칙(<c>CurvedPlaneZone*</c>)은
    /// LongPart의 <c>PlaneZoneTailHigh/Low</c>처럼 이름과 실제 도구가 어긋난 존이 있어 믿을 수 없다.
    /// </summary>
    public static PartCraft CraftOfZone(WorkZone zone) => zone switch
    {
        null => PartCraft.None,
        InkLineZone => PartCraft.Ink,
        SawZone => PartCraft.Saw,
        AdzeZone => PartCraft.Adze,
        ChiselZone => PartCraft.Chisel,
        PlaneZone plane => IsCurvedPlaneZone(plane) ? PartCraft.CurvedPlane : PartCraft.FlatPlane,
        _ => PartCraft.None
    };

    /// <summary>
    /// 배대패 존인지. <see cref="PlaneZone.Start"/>가 두 필드를 서로 동기화하므로 어느 쪽이든
    /// 켜져 있으면 곡면 존이다. 브릿지는 Awake·OnEnable에서 읽어 Start보다 이를 수 있어 두 필드를
    /// 모두 본다 — 씬에 직렬화되어 있는 것은 <c>requiredToolType</c> 쪽이다.
    /// </summary>
    static bool IsCurvedPlaneZone(PlaneZone plane) =>
        plane.isCurvedPlaneZone || plane.requiredToolType == ToolType.CurvedPlane;

    /// <summary>
    /// 존의 작업 종류를 그 부재의 신호 키로 접는다.
    ///
    /// 배대패를 독립 목표로 두는 부재는 MediumPart 하나다(배대패가 처음 등장하는 단계). ShortPart에는
    /// 곡면 존이 없고, LongPart는 숙련 단계라 평·곡면 대패 존 셋을 목표 하나로 묶는다 — 그래서 그
    /// 목표에서는 두 대패를 다 쥘 수 있어야 한다(<see cref="ToolsForSignal"/>).
    /// </summary>
    public static string SignalForZone(ProcessId part, PartCraft craft)
    {
        if (craft == PartCraft.CurvedPlane && part != ProcessId.MediumPart) craft = PartCraft.FlatPlane;

        return part switch
        {
            ProcessId.ShortPart => craft switch
            {
                PartCraft.Ink => ShortInk,
                PartCraft.Saw => ShortSaw,
                PartCraft.FlatPlane => ShortPlane,
                PartCraft.Chisel => ShortChisel,
                _ => null
            },
            ProcessId.MediumPart => craft switch
            {
                PartCraft.Ink => MediumInk,
                PartCraft.Saw => MediumSaw,
                PartCraft.Adze => MediumAdze,
                PartCraft.FlatPlane => MediumPlane,
                PartCraft.CurvedPlane => MediumCurved,
                PartCraft.Chisel => MediumChisel,
                _ => null
            },
            ProcessId.LongPart => craft switch
            {
                PartCraft.Ink => LongInk,
                PartCraft.Saw => LongSaw,
                PartCraft.Adze => LongAdze,
                PartCraft.FlatPlane => LongPlane,
                PartCraft.Chisel => LongChisel,
                _ => null
            },
            _ => null
        };
    }

    /// <summary>신호 키가 속한 공정. 알 수 없는 키면 false.</summary>
    public static bool TryGetProcess(string signal, out ProcessId process)
    {
        process = ProcessId.Prologue;
        if (string.IsNullOrWhiteSpace(signal)) return false;

        foreach (var candidate in SignalProcesses)
        {
            var signals = SignalsForProcess(candidate);
            for (var i = 0; i < signals.Count; i++)
            {
                if (!string.Equals(signals[i], signal, StringComparison.Ordinal)) continue;
                process = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 이 목표를 수행하는 데 필요한 도구. 잠금은 목표 단위이므로 지금 목표의 신호 키 하나가
    /// 열어 둘 도구를 전부 결정한다 (F-014 5.3).
    /// </summary>
    public static PartToolMask ToolsForSignal(string signal) => signal switch
    {
        ShortInk or MediumInk or LongInk => PartToolMask.Ink,
        ShortSaw or MediumSaw or LongSaw => PartToolMask.Saw,
        MediumAdze or LongAdze => PartToolMask.Adze,
        MediumPlane => PartToolMask.FlatPlane,
        MediumCurved => PartToolMask.CurvedPlane,

        // ShortPart에는 곡면 존이 없다 — 배대패까지 열면 잡히는데 깎을 대상이 없어 "고장"으로
        // 오인된다(실사용 보고). 곡면 존을 함께 접은 부재는 LongPart뿐이라 거기서만 둘을 연다.
        ShortPlane => PartToolMask.FlatPlane,
        LongPlane => PartToolMask.FlatPlane | PartToolMask.CurvedPlane,

        ShortChisel or MediumChisel or LongChisel => PartToolMask.Chisel | PartToolMask.Hammer,

        // 조립 목표는 도구가 아니라 부재를 연다.
        _ => PartToolMask.None
    };

    /// <summary>
    /// 존이 속한 부재. <see cref="PartZoneGate"/>가 부재 루트마다 하나씩 붙어 있어 그것을 1순위
    /// 기준으로 쓰고, 게이트가 없는 배치에 대비해 루트 이름으로 폴백한다. 연습용 목재
    /// (InkWood·SawWood)처럼 어느 부재에도 속하지 않는 존은 false다 — 공정 수량에 넣지 않는다.
    /// </summary>
    public static bool TryResolvePart(Component component, out ProcessId part)
    {
        part = ProcessId.Prologue;
        if (component == null) return false;

        var gate = component.GetComponentInParent<PartZoneGate>(true);
        if (gate != null && TryParsePartName(gate.name, out part)) return true;

        for (var transform = component.transform; transform != null; transform = transform.parent)
            if (TryParsePartName(transform.name, out part))
                return true;

        return false;
    }

    public static bool TryParsePartName(string value, out ProcessId part)
    {
        part = ProcessId.Prologue;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var name = value.Trim();
        if (string.Equals(name, ShortPartRootName, StringComparison.OrdinalIgnoreCase))
        {
            part = ProcessId.ShortPart;
            return true;
        }

        if (string.Equals(name, MediumPartRootName, StringComparison.OrdinalIgnoreCase))
        {
            part = ProcessId.MediumPart;
            return true;
        }

        if (string.Equals(name, LongPartRootName, StringComparison.OrdinalIgnoreCase))
        {
            part = ProcessId.LongPart;
            return true;
        }

        return false;
    }
}

/// <summary>
/// 메인 플레이 씬의 제작 오브젝트를 데이터 기반 퀘스트 러너(<see cref="QuestManager"/>)에 연결한다.
/// 팀원 소유 존·도구 코드는 수정하지 않고 공개된 완료 이벤트만 구독한다.
///
/// 공정 단위가 도구에서 부재로 바뀌면서 이 브릿지가 하는 일이 셋으로 늘었다.
/// <list type="number">
/// <item><b>신호</b>: 존이 완료되면 그 존이 속한 부재와 작업 종류로 신호 키를 정해 버스에 올린다
/// (<see cref="PartProcessSignals.SignalForZone"/>). 어느 부재에도 속하지 않는 연습용 존은 세지 않는다.</item>
/// <item><b>부재 잠금</b>: 지금 단계가 아닌 부재의 존을 통째로 잠근다. 잠금 수단은
/// <see cref="PartZoneGate.SetPartActive"/>다 — 게이트가 이미 선행 규칙으로 같은 존들의 콜라이더를
/// 여닫고 있어, 브릿지가 콜라이더를 직접 건드리면 두 주인이 생긴다.</item>
/// <item><b>도구 잠금</b>: 공정이 아니라 <b>현재 목표</b> 단위다. 목표의 신호 키가 곧 쓸 도구를
/// 결정하므로(<see cref="PartProcessSignals.ToolsForSignal"/>), 먹매김 목표에서는 먹통만, 끌 목표에서는
/// 끌과 망치만 잡힌다.</item>
/// </list>
///
/// 러너가 하나로 통일되면서 이 브릿지는 조작 튜토리얼이 도는 동안에도 살아 있게 됐다. 잠금은
/// 제작 공정 구간에서만 적용한다 — <see cref="PartProcessSignals.IsProductionProcess"/>를 참고한다.
/// </summary>
public sealed class MainPlayProcessBridge : MonoBehaviour
{
    public const string PurlinPartId = "1floor";
    public const int GongpoRequiredPartCount = 37;

    [SerializeField] QuestManager quest;

    [Tooltip("하위 호환용 존 참조. 비워 두면 씬 전체를 수집한다. 지정한 존은 중복 없이 합쳐진다.")]
    [SerializeField] List<InkLineZone> inkLineZones = new();

    [SerializeField] List<ChiselZone> chiselZones = new();
    [SerializeField] List<AssemblyTarget> assemblyTargets = new();

    [Header("개발 도구")]
    [SerializeField] bool showOverlay = true;

    /// <summary>씬의 모든 작업 존. 부재 소속 여부와 무관하게 구독한다.</summary>
    readonly List<WorkZone> _zones = new();

    /// <summary>부재에 속한 존만. 값은 (부재, 신호 키)다.</summary>
    readonly Dictionary<WorkZone, (ProcessId Part, string Signal)> _zoneSignals = new();

    /// <summary>신호 키별 존 개수. 오버레이 분모이자 quest.json amount의 근거다.</summary>
    readonly Dictionary<string, int> _signalZoneCounts = new(StringComparer.Ordinal);

    readonly Dictionary<ProcessId, PartZoneGate> _gates = new();
    readonly Dictionary<WorkZone, UnityAction> _zoneListeners = new();
    readonly HashSet<WorkZone> _countedZones = new();
    readonly Dictionary<AssemblyTarget, Action<Grabbable>> _assemblyListeners = new();
    readonly HashSet<AssemblyTarget> _completedAssemblyTargets = new();
    readonly Dictionary<Grabbable, bool> _originalGrabAccess = new();
    readonly Dictionary<Grabbable, string> _assemblyPartIds = new();
    readonly List<Grabbable> _inkTools = new();
    readonly List<Grabbable> _flatPlaneTools = new();
    readonly List<Grabbable> _curvedPlaneTools = new();
    readonly List<Grabbable> _sawTools = new();
    readonly List<Grabbable> _adzeTools = new();
    readonly List<Grabbable> _chiselTools = new();
    readonly List<Grabbable> _hammerTools = new();

    PauseController _pauseController;
    ProcessId? _activeProcess;

    /// <summary>지금 목표의 신호 키. 도구 잠금의 유일한 입력이다. 목표가 없으면 null.</summary>
    string _activeSignal;

    int _completedPurlinCount;
    int _completedGongpoCount;
    bool _started;
    bool _restartRequested;

    void Awake()
    {
        if (quest == null) quest = FindAnyObjectByType<QuestManager>(FindObjectsInactive.Include);

        // 프리팹 자산은 씬 참조를 담지 못한다. 인스턴스 오버라이드를 채우지 않아도 목록이 스스로
        // 차야 프리팹 하나를 놓는 것만으로 공정이 돈다. 비활성까지 세는 것은 씬 빌더가 쓰는
        // GetComponentsInChildren(true)와 같은 범위를 유지하기 위해서다.
        CollectZones();
        CollectGates();
        CacheGatedObjects();

        assemblyTargets ??= new List<AssemblyTarget>();
        if (assemblyTargets.Count == 0)
            assemblyTargets.AddRange(FindObjectsByType<AssemblyTarget>(FindObjectsSortMode.None));
    }

    void OnEnable()
    {
        ResetAllSignals();

        if (quest != null)
        {
            quest.QuestChanged += OnQuestChanged;
            quest.ObjectiveChanged += OnObjectiveChanged;
            quest.Completed += OnProcessCompleted;
        }

        for (var i = 0; i < _zones.Count; i++)
        {
            var zone = _zones[i];
            if (zone == null || _zoneListeners.ContainsKey(zone)) continue;

            UnityAction listener = () => OnZoneCompleted(zone);
            _zoneListeners.Add(zone, listener);
            zone.OnWorkCompleted.AddListener(listener);
        }

        BindPauseController();
        ApplyAccess(null, null);

        if (_started)
        {
            BindAssemblyTargets();
            SynchronizeRunningProcess();
        }
    }

    void Start()
    {
        _started = true;
        BindAssemblyTargets();
        BindPauseController();
        SynchronizeRunningProcess();
    }

    void OnDisable()
    {
        foreach (var pair in _zoneListeners)
            if (pair.Key != null) pair.Key.OnWorkCompleted.RemoveListener(pair.Value);

        if (quest != null)
        {
            quest.QuestChanged -= OnQuestChanged;
            quest.ObjectiveChanged -= OnObjectiveChanged;
            quest.Completed -= OnProcessCompleted;
        }

        if (_pauseController != null)
            _pauseController.ProcessRestartRequested -= OnProcessRestartRequested;

        foreach (var pair in _assemblyListeners)
            if (pair.Key != null && pair.Key.Snap != null)
                pair.Key.Snap.Assembled -= pair.Value;

        _zoneListeners.Clear();
        _assemblyListeners.Clear();
        _completedAssemblyTargets.Clear();
        _countedZones.Clear();
        _pauseController = null;
        _activeProcess = null;
        _activeSignal = null;
        _completedPurlinCount = 0;
        _completedGongpoCount = 0;
        _restartRequested = false;
    }

    // ---- 수집 ----

    void CollectZones()
    {
        _zones.Clear();
        _zoneSignals.Clear();
        _signalZoneCounts.Clear();

        // 직렬화된 하위 호환 목록을 먼저 넣어 씬에 없는 참조까지 살린다.
        AppendZones(inkLineZones);
        AppendZones(chiselZones);

        foreach (var zone in FindObjectsByType<WorkZone>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            AppendZone(zone);

        for (var i = 0; i < _zones.Count; i++)
        {
            var zone = _zones[i];
            if (!PartProcessSignals.TryResolvePart(zone, out var part)) continue;

            var signal = PartProcessSignals.SignalForZone(part, PartProcessSignals.CraftOfZone(zone));
            if (string.IsNullOrEmpty(signal))
            {
                Debug.LogWarning(
                    $"[MainPlay] '{zone.name}'({zone.GetType().Name})은 '{part}'에 대응하는 신호 키가 없어 " +
                    "공정 수량에서 제외합니다.", zone);
                continue;
            }

            _zoneSignals[zone] = (part, signal);
            _signalZoneCounts[signal] = _signalZoneCounts.GetValueOrDefault(signal) + 1;
        }
    }

    void AppendZones<T>(List<T> zones) where T : WorkZone
    {
        if (zones == null) return;
        for (var i = 0; i < zones.Count; i++) AppendZone(zones[i]);
    }

    void AppendZone(WorkZone zone)
    {
        if (zone != null && !_zones.Contains(zone)) _zones.Add(zone);
    }

    void CollectGates()
    {
        _gates.Clear();
        foreach (var gate in FindObjectsByType<PartZoneGate>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (gate == null || !PartProcessSignals.TryParsePartName(gate.name, out var part)) continue;
            if (!_gates.TryAdd(part, gate))
                Debug.LogWarning($"[MainPlay] 부재 '{part}'의 PartZoneGate가 둘 이상입니다. 첫 번째만 씁니다.", gate);
        }
    }

    void CacheGatedObjects()
    {
        CacheTools<InkLineTool>(_inkTools);
        CacheTools<FlatPlaneTool>(_flatPlaneTools);
        CacheTools<CurvedPlaneTool>(_curvedPlaneTools);
        CacheTools<SawTool>(_sawTools);
        CacheTools<AdzeTool>(_adzeTools);
        CacheTools<ChiselTool>(_chiselTools);
        CacheTools<HammerTool>(_hammerTools);

        foreach (var male in FindObjectsByType<MaleSnapPoint>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (male == null) continue;

            var part = male.GetComponentInParent<Grabbable>(true);
            if (part == null) continue;

            RememberAccess(part);
            _assemblyPartIds.TryAdd(part, male.mySnapID);
        }
    }

    void CacheTools<T>(List<Grabbable> collection) where T : Component
    {
        foreach (var tool in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            RememberTool(tool != null ? tool.GetComponentInParent<Grabbable>(true) : null, collection);
    }

    void RememberTool(Grabbable tool, List<Grabbable> collection)
    {
        if (tool == null || collection.Contains(tool)) return;
        collection.Add(tool);
        RememberAccess(tool);
    }

    void RememberAccess(Grabbable target)
    {
        if (target != null) _originalGrabAccess.TryAdd(target, target.GrabEnabled);
    }

    void BindPauseController()
    {
        if (_pauseController != null) return;
        if (!PauseController.TryGetInstance(out var pause)) return;

        _pauseController = pause;
        _pauseController.ProcessRestartRequested += OnProcessRestartRequested;
    }

    void SynchronizeRunningProcess()
    {
        if (quest == null || !quest.IsRunning) return;

        OnProcessChanged(quest.Process);
        ApplySignalFromNode(quest.CurrentNode);
    }

    // ---- 공정·목표 전환 ----

    void OnQuestChanged(QuestDefinition definition)
    {
        if (definition == null) return;
        OnProcessChanged(definition.Process);
    }

    void OnProcessChanged(ProcessId process)
    {
        if (_activeProcess == process) return;

        _activeProcess = process;
        _activeSignal = null;
        ResetProcessState(process);
        ApplyPartGating(process);
        ApplyAccess(process, null);

        BindAssemblyTargets();
        SynchronizeAssemblyState(process);

        Debug.Log($"[MainPlay] 공정 연결 상태를 '{process}'에 맞췄습니다.", this);
    }

    void OnObjectiveChanged(QuestNodeData node, int index) => ApplySignalFromNode(node);

    /// <summary>
    /// 현재 목표의 신호 키를 읽어 도구 잠금을 다시 건다. QuestManager가 목표 노드를 그대로
    /// 공개하므로(<see cref="QuestManager.ObjectiveChanged"/>·<see cref="QuestManager.CurrentNode"/>)
    /// 러너를 고치지 않고 목표 단위 잠금을 만들 수 있다.
    /// </summary>
    void ApplySignalFromNode(QuestNodeData node)
    {
        var objective = node?.Objective;
        var signal = objective != null && objective.Condition == StepCondition.Signal
            ? objective.Target
            : null;

        if (string.Equals(_activeSignal, signal, StringComparison.Ordinal)) return;

        _activeSignal = signal;
        ApplyAccess(_activeProcess, signal);
    }

    void OnProcessCompleted(ProcessId process)
    {
        if (_activeProcess != process) return;

        _activeSignal = null;
        ApplyAccess(null, null);
    }

    void ResetProcessState(ProcessId process)
    {
        foreach (var signal in PartProcessSignals.SignalsForProcess(process))
            ProcessSignalBus.Reset(signal);

        if (process == ProcessId.GongpoPuzzle)
        {
            _completedAssemblyTargets.Clear();
            _completedPurlinCount = 0;
            _completedGongpoCount = 0;
            return;
        }

        // 이 부재의 존만 다시 셀 수 있게 되돌린다. 다른 부재의 진행은 건드리지 않는다.
        _countedZones.RemoveWhere(zone =>
            zone == null || (_zoneSignals.TryGetValue(zone, out var entry) && entry.Part == process));
    }

    void ResetAllSignals()
    {
        foreach (var signal in PartProcessSignals.All) ProcessSignalBus.Reset(signal);
        _countedZones.Clear();
    }

    /// <summary>
    /// 지금 단계의 부재만 열어 둔다. 잠그는 주체를 게이트로 일원화해, 선행 규칙으로 이미 잠긴 존을
    /// 브릿지가 잘못 열어 주는 일이 없게 한다. 부재 단계가 아니면(튜토리얼·조립) 전부 원래대로 둔다.
    /// </summary>
    void ApplyPartGating(ProcessId process)
    {
        var gateByPart = PartProcessSignals.IsPartProcess(process);

        foreach (var pair in _gates)
        {
            if (pair.Value == null) continue;
            pair.Value.SetPartActive(!gateByPart || pair.Key == process);
        }
    }

    // ---- 접근 통제 ----

    void ApplyAccess(ProcessId? process, string signal)
    {
        // 활성 공정이 없으면 게이팅하지 않고 원래 상태로 되돌린다. 러너가 대기 중인 씬(튜토리얼,
        // 자유 테스트)에서 공포 파츠까지 전부 잠겨 버리면 공정 밖 검증이 불가능하다. 공정 잠금
        // (F-014 5.3)은 어떤 공정이 실제로 도는 동안에만 의미가 있다.
        if (!process.HasValue || !PartProcessSignals.IsProductionProcess(process.Value))
        {
            foreach (var pair in _originalGrabAccess)
            {
                // 이미 안착된 조립 파츠는 Freeze로 잠근 것이므로 되살리지 않는다.
                if (pair.Key != null && IsSeatedAssemblyPart(pair.Key)) continue;
                SetGrabAccess(pair.Key, pair.Value);
            }

            return;
        }

        var tools = PartProcessSignals.ToolsForSignal(signal);
        SetToolAccess(_inkTools, tools.HasFlag(PartToolMask.Ink));
        SetToolAccess(_sawTools, tools.HasFlag(PartToolMask.Saw));
        SetToolAccess(_flatPlaneTools, tools.HasFlag(PartToolMask.FlatPlane));
        SetToolAccess(_curvedPlaneTools, tools.HasFlag(PartToolMask.CurvedPlane));
        SetToolAccess(_adzeTools, tools.HasFlag(PartToolMask.Adze));
        SetToolAccess(_chiselTools, tools.HasFlag(PartToolMask.Chisel));
        SetToolAccess(_hammerTools, tools.HasFlag(PartToolMask.Hammer));

        foreach (var pair in _assemblyPartIds)
        {
            var part = pair.Key;
            if (part == null) continue;

            var assemblyPart = part.GetComponent<AssemblyPart>() ?? part.GetComponentInChildren<AssemblyPart>();
            var assembled = assemblyPart != null && assemblyPart.isAssembled;
            var available = IsAssemblyPartAvailable(signal, pair.Value, assembled) &&
                            _originalGrabAccess.GetValueOrDefault(part);

            SetGrabAccess(part, available);
        }
    }

    void SetToolAccess(List<Grabbable> tools, bool available)
    {
        for (var i = 0; i < tools.Count; i++)
        {
            var tool = tools[i];
            if (tool == null) continue;
            SetGrabAccess(tool, available && _originalGrabAccess.GetValueOrDefault(tool));
        }
    }

    static void SetGrabAccess(Grabbable target, bool available)
    {
        if (target != null && target.GrabEnabled != available) target.GrabEnabled = available;
    }

    static bool IsSeatedAssemblyPart(Grabbable part)
    {
        var assemblyPart = part.GetComponent<AssemblyPart>() ?? part.GetComponentInChildren<AssemblyPart>();
        return assemblyPart != null && assemblyPart.isAssembled;
    }

    /// <summary>
    /// 조립 부재를 지금 잡을 수 있는지. 도리와 공포가 한 공정(<see cref="ProcessId.GongpoPuzzle"/>)에
    /// 합쳐졌으므로 판정 기준은 공정이 아니라 <b>현재 목표의 신호 키</b>다 — 도리 목표에서는 도리만,
    /// 공포 목표에서는 도리가 아닌 부재만 열린다.
    /// </summary>
    public static bool IsAssemblyPartAvailable(string signal, string partId, bool assembled)
    {
        if (assembled || string.IsNullOrWhiteSpace(partId)) return false;

        var isPurlin = string.Equals(partId, PurlinPartId, StringComparison.Ordinal);
        return signal switch
        {
            PartProcessSignals.PurlinInstall => isPurlin,
            PartProcessSignals.GongpoAssembled => !isPurlin,
            _ => false
        };
    }

    // ---- 재시작 ----

    void OnProcessRestartRequested()
    {
        if (_restartRequested) return;

        var sceneName = gameObject.scene.name;
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[MainPlay] 공정 재시작 씬 '{sceneName}'을 Build Settings에서 찾을 수 없습니다.", this);
            return;
        }

        if (!SceneController.TryGetInstance(out var scenes))
        {
            Debug.LogError("[MainPlay] SceneController가 없어 공정을 다시 시작할 수 없습니다.", this);
            return;
        }

        _restartRequested = true;
        InGameDialogue.TryGetInstance(out var dialogue);
        dialogue?.Stop();
        ResetAllSignals();

        Debug.Log($"[MainPlay] 현재 공정 '{quest?.Process}'을 처음부터 다시 시작합니다.", this);
        scenes.LoadScene(sceneName);
    }

    // ---- 존 완료 ----

    void OnZoneCompleted(WorkZone zone)
    {
        if (zone == null || quest == null) return;

        // 어느 부재에도 속하지 않는 연습용 존(InkWood·SawWood 등)은 세지 않는다.
        if (!_zoneSignals.TryGetValue(zone, out var entry)) return;
        if (quest.Process != entry.Part) return;
        if (!_countedZones.Add(zone)) return;

        ProcessSignalBus.Add(entry.Signal);

        var required = _signalZoneCounts.GetValueOrDefault(entry.Signal, 1);
        Debug.Log(
            $"[MainPlay] {entry.Part} · {entry.Signal} " +
            $"{Mathf.RoundToInt(ProcessSignalBus.Read(entry.Signal))}/{required} ({zone.name})", zone);
    }

    // ---- 조립 ----

    void BindAssemblyTargets()
    {
        for (var i = 0; i < assemblyTargets.Count; i++)
        {
            var target = assemblyTargets[i];
            if (target == null || target.Snap == null || _assemblyListeners.ContainsKey(target)) continue;

            Action<Grabbable> listener = part => OnAssemblyCompleted(target, part);
            _assemblyListeners.Add(target, listener);
            target.Snap.Assembled += listener;
        }
    }

    void SynchronizeAssemblyState(ProcessId process)
    {
        if (process != ProcessId.GongpoPuzzle) return;

        for (var i = 0; i < assemblyTargets.Count; i++)
        {
            var target = assemblyTargets[i];
            if (target == null || target.Snap == null || !target.Snap.IsOccupied) continue;
            OnAssemblyCompleted(target, target.Snap.Occupant);
        }
    }

    void OnAssemblyCompleted(AssemblyTarget target, Grabbable part)
    {
        if (target == null || part == null || quest == null) return;
        if (quest.Process != ProcessId.GongpoPuzzle) return;

        var male = part.GetComponentInChildren<MaleSnapPoint>();
        if (male == null) return;

        if (!_completedAssemblyTargets.Add(target)) return;

        if (string.Equals(male.mySnapID, PurlinPartId, StringComparison.Ordinal))
        {
            _completedPurlinCount++;
            ProcessSignalBus.Add(PartProcessSignals.PurlinInstall);
            Debug.Log($"[MainPlay] 도리 설치 {_completedPurlinCount}/1", part);
            return;
        }

        _completedGongpoCount++;
        ProcessSignalBus.Add(PartProcessSignals.GongpoAssembled);
        Debug.Log($"[MainPlay] 공포 조립 {_completedGongpoCount}/{GongpoRequiredPartCount}", part);
    }

    // ---- 개발 오버레이 ----

    void OnGUI()
    {
        // 제작 공정 구간에서만 연결 상태를 표시한다. 러너가 하나로 합쳐진 뒤로는 조작 튜토리얼도
        // IsRunning이라 공정 범위 확인이 함께 필요하다 — 튜토리얼 화면에 빈 상자가 남으면 안 된다.
        if (!showOverlay || quest == null || !quest.IsRunning ||
            !PartProcessSignals.IsProductionProcess(quest.Process))
            return;

        GUI.Box(new Rect(10f, 132f, 620f, 56f), string.Empty);

        if (string.IsNullOrEmpty(_activeSignal))
        {
            GUI.Label(new Rect(20f, 140f, 600f, 22f), $"{quest.Process}: 목표 대기 중");
            GUI.Label(new Rect(20f, 162f, 600f, 22f), "설명이 끝나면 첫 목표가 시작됩니다.");
            return;
        }

        var done = Mathf.RoundToInt(ProcessSignalBus.Read(_activeSignal));
        var required = _activeSignal == PartProcessSignals.GongpoAssembled
            ? GongpoRequiredPartCount
            : _activeSignal == PartProcessSignals.PurlinInstall
                ? 1
                : _signalZoneCounts.GetValueOrDefault(_activeSignal, 1);

        GUI.Label(new Rect(20f, 140f, 600f, 22f), $"{quest.Process}: {_activeSignal} {done}/{required}");
        GUI.Label(new Rect(20f, 162f, 600f, 22f), DescribeTools(_activeSignal));
    }

    static string DescribeTools(string signal)
    {
        var tools = PartProcessSignals.ToolsForSignal(signal);
        if (tools == PartToolMask.None) return "조립 부재만 열려 있습니다.";

        var names = new List<string>();
        if (tools.HasFlag(PartToolMask.Ink)) names.Add("먹통");
        if (tools.HasFlag(PartToolMask.Saw)) names.Add("톱");
        if (tools.HasFlag(PartToolMask.FlatPlane)) names.Add("평대패");
        if (tools.HasFlag(PartToolMask.CurvedPlane)) names.Add("배대패");
        if (tools.HasFlag(PartToolMask.Adze)) names.Add("자귀");
        if (tools.HasFlag(PartToolMask.Chisel)) names.Add("끌");
        if (tools.HasFlag(PartToolMask.Hammer)) names.Add("망치");
        return "지금 쥘 수 있는 도구: " + string.Join(" · ", names);
    }
}
