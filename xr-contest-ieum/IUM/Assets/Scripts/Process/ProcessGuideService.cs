using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 시각 공정 가이드. 씬에 심어 둔 <see cref="ProcessGuideAnchor"/>를 강조해 "지금 어디서 무엇을
/// 다뤄야 하는지"를 보여 준다. 기본은 상시 표시다 — 현재 목표의 대상을 목표가 바뀔 때까지 계속
/// 강조하고, 목표가 바뀌면 새 대상으로 갱신한다(인스펙터에서 끄면 종전처럼 공정별 최초 1회
/// <c>displaySeconds</c> 동안만 표시). 강조는 알파 펄스로 은은하게 깜빡여 시인성을 확보한다.
///
/// 대상에 따라 강조 방식이 둘로 갈린다.
/// <list type="bullet">
/// <item><b>존(<see cref="WorkZone"/> 파생)</b>: <see cref="LineRenderer"/> 안내 라인.
/// 먹줄·절단선은 "이 선을 그어라"가, 끌·자귀는 "이 사각형 안을 파라"가 목표라서 존의 부피를
/// 통째로 칠하는 판보다 선이 정확한 지시다. 경로는 존의 BoxCollider 로컬 치수와 존 타입별
/// 방향 필드(<c>pointA/pointB</c>·<c>sawDirection</c>·<c>planeDirection</c>)로 산출한다.</item>
/// <item><b>그 밖의 대상(도구·부재·결합 자리)</b>: 종전대로 QuickOutline 외곽선. 렌더러가
/// 없거나 읽기 불가 메시라 외곽선을 얹을 수 없으면 콜라이더 크기의 반투명 프록시를 세운다.
/// 라인 경로를 만들지 못한 존도 이 경로로 폴백한다.</item>
/// </list>
///
/// 존 안내와 별개로 <b>지금 목표가 요구하는 도구</b>도 함께 강조한다. "어디를"만 짚고 "무엇으로"를
/// 빼면 배대패·자귀처럼 이름과 생김새가 낯선 도구를 고를 수 없다. 어떤 도구인지는
/// <see cref="PartProcessSignals.ToolsForSignal"/>로 정한다 — <see cref="MainPlayProcessBridge"/>가
/// 도구 잠금에 쓰는 것과 같은 표라, 강조되는 도구와 잡히는 도구가 어긋날 수 없다. 도구 강조는
/// 그 도구를 쥐면 꺼지고 놓으면(목표가 그대로면) 다시 켜진다.
///
/// 이 시스템은 F-012의 '이음이 직접 조작 안내(말)'를 대체한다. 이음이는 어떤 경우에도 세세한
/// 조작을 읊지 않고, 안내가 필요한 순간은 전부 여기로 수렴한다. 그래서 발동 경로가 셋이다.
/// <list type="number">
/// <item>공정별 세션 내 최초 목표 진입 — 처음 보는 작업대는 한 번 짚어 준다.</item>
/// <item>같은 목표에서 재안내(<see cref="QuestManager.RetryHinted"/>)가
/// <see cref="AiProcessContext.DirectAnswerFailureCount"/>회 누적 — 종전에 이음이가 말로
/// 직접 안내하던 문턱과 같은 수다. 그 문턱의 의미가 "가이드 자동 표시"로 바뀌었다.</item>
/// <item>이음이가 응답에서 showGuide를 켠 경우 — <see cref="AiConversationManager"/>가 부른다.</item>
/// </list>
///
/// 외곽선 강조는 <see cref="TutorialOutlineGuide"/>와 같게 맞췄다. 생성물(라인·프록시·재질·메시)은
/// 전부 <see cref="HideFlags.DontSave"/>이고 파괴 시 되돌린다.
///
/// 색은 대상의 존 타입으로 판정한 작업 종류(<see cref="GuideCraft"/>)별 팔레트를 쓴다. 또
/// <see cref="PartZoneGate"/>가 잠근 존은 수집에서 제외하고, 잠금 상태가 바뀌면 표시 중인
/// 강조를 같은 요청으로 다시 수집해 갱신한다.
///
/// 씬 컴포넌트다. 강조 대상이 씬 오브젝트라 <see cref="Singleton{T}"/>처럼
/// DontDestroyOnLoad로 살아남으면 다음 씬에서 죽은 참조를 들게 된다. 대신
/// <see cref="ProcessTarget"/>과 같은 씬 수명 정적 등록 방식을 쓴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProcessGuideService : MonoBehaviour
{
    /// <summary>
    /// 강조 색을 고르는 작업 종류. 공정(ProcessId)이 아니라 존 타입으로 판정한다 — 대패·자귀는
    /// 아직 독립 공정이 아니고(ISSUE-029), 한 공정 안에서도 여러 종류의 존이 함께 강조되기 때문이다.
    /// </summary>
    public enum GuideCraft
    {
        /// <summary>존이 아닌 대상(도구·부재·결합 자리). outlineColor를 그대로 쓴다.</summary>
        Default,
        Makmeok,
        Sawing,
        Chiseling,
        Planing,
        Adzing
    }

    [Serializable]
    public struct CraftColor
    {
        public GuideCraft craft;
        public Color color;
    }

    sealed class HighlightEntry
    {
        public Outline Outline;
        public GameObject Proxy;

        /// <summary>존 대상의 안내 라인. 이 값이 있으면 <see cref="Outline"/>·<see cref="Proxy"/>는 비어 있다.</summary>
        public LineRenderer Line;
    }

    /// <summary>씬에서 찾아 둔 도구 하나. 강조 대상 GameObject와 잡기 상태를 함께 들고 있다.</summary>
    sealed class ToolEntry
    {
        public GameObject Target;
        public Grabbable Grab;

        /// <summary>이 도구가 대응하는 잠금 비트. 한 오브젝트가 두 도구를 겸하면 합쳐진다.</summary>
        public PartToolMask Mask;

        /// <summary>강조 색을 고를 작업 종류. 존 색과 같은 팔레트를 쓴다.</summary>
        public GuideCraft Craft;
    }

    static ProcessGuideService _instance;

    /// <summary>로컬 축 인덱스 → 단위 벡터. 0=X, 1=Y, 2=Z.</summary>
    static readonly Vector3[] LocalAxes = { Vector3.right, Vector3.up, Vector3.forward };

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>
    /// 자동 최초 표시를 마친 공정. 씬을 다시 로드해도 같은 플레이 세션이면 다시 뜨지 않아야 하므로
    /// 정적으로 둔다. 플레이 모드 진입마다 초기화된다.
    /// </summary>
    static readonly HashSet<ProcessId> AutoShownProcesses = new();

    [Header("참조")]
    [Tooltip("비우면 씬에서 찾는다. 없으면 자동 발동 없이 수동 호출만 동작한다.")]
    [SerializeField] QuestManager questManager;

    [Header("표시")]
    [Tooltip("켜면 현재 목표의 대상을 목표가 바뀌거나 공정이 끝날 때까지 계속 표시하고, 목표가 " +
             "바뀔 때마다 새 목표의 대상으로 갱신한다. 끄면 공정별 최초 1회, displaySeconds 동안만 표시한다.")]
    [SerializeField] bool alwaysShowCurrentObjective = true;

    [Tooltip("상시 표시가 꺼져 있을 때 강조가 남는 시간(초). 다시 호출하면 갱신된다.")]
    [SerializeField, Min(0.5f)] float displaySeconds = 6f;

    [Tooltip("작업 종류를 판정할 수 없는 대상(도구·부재 등)의 기본 외곽선 색.")]
    [SerializeField] Color outlineColor = new(0.20f, 0.80f, 1f, 1f);

    // R 컨펌 팔레트: 먹매김=하늘색 / 톱질=주황 / 끌질=노랑 / 대패=연두 / 자귀=보라.
    [Tooltip("존 타입으로 판정한 작업 종류별 외곽선 색. 목록에 없는 종류는 기본 색을 쓴다.")]
    // 필드명을 craftColors에서 개명해 씬에 직렬화된 구 팔레트(주황·노랑이 목재색과 겹침)를
    // 무효화했다. 나무 갈색 위에서 보이도록 전 색을 한색 계열·고채도로 뽑았다.
    [SerializeField] CraftColor[] craftPalette =
    {
        new() { craft = GuideCraft.Makmeok, color = new Color(0.20f, 0.80f, 1.00f, 1f) },
        new() { craft = GuideCraft.Sawing, color = new Color(0.15f, 0.45f, 1.00f, 1f) },
        new() { craft = GuideCraft.Chiseling, color = new Color(1.00f, 0.20f, 0.80f, 1f) },
        new() { craft = GuideCraft.Planing, color = new Color(0.20f, 1.00f, 0.45f, 1f) },
        new() { craft = GuideCraft.Adzing, color = new Color(0.65f, 0.35f, 1.00f, 1f) }
    };

    [SerializeField, Range(0f, 10f)] float outlineWidth = 4f;

    // 종전 proxyAlpha(0.08)는 야간 조명·톤매핑 아래서 사실상 보이지 않아 기준값을 올리고 필드를
    // 개명했다 — 씬에 직렬화된 옛 0.08이 새 기본값을 덮어쓰지 않게 하기 위한 의도적 rename이다.
    [Tooltip("프록시 면의 기준 알파. 외곽선과 별개로 존 면적을 채우는 반투명 판의 진하기다.")]
    [SerializeField, Range(0f, 1f)] float proxyBaseAlpha = 0.3f;

    [Tooltip("프록시 알파 펄스의 진폭(기준 알파 대비 비율). 0.3이면 기준의 ±30%로 오간다. 0은 펄스 없음.")]
    [SerializeField, Range(0f, 1f)] float proxyPulseRatio = 0.3f;

    [Tooltip("프록시 알파 펄스 한 주기(초).")]
    [SerializeField, Min(0.1f)] float proxyPulsePeriod = 1.2f;

    [Header("존 안내 라인")]
    // 존은 박스 프록시(면을 통째로 채우는 반투명 판) 대신 '어디에 무엇을 그어야 하는지'를 그대로
    // 보여 주는 라인으로 안내한다. 먹줄·절단선은 선 자체가 작업 목표이고, 끌·자귀는 파낼 자리의
    // 외곽이 목표라 사각 루프가 된다.
    [Tooltip("안내 라인의 기준 알파. 라인은 면적이 작아 프록시보다 진하게 그린다.")]
    [SerializeField, Range(0f, 1f)] float guideLineAlpha = 0.9f;

    // 폭을 절대값 하나로 고정할 수 없다. Play 씬의 존은 월드 최단 변이 1~8m대(먹줄·톱)인 것과
    // 0.02m대(LongPart·MediumPart 하위 끌·대패)가 섞여 있어 절대 폭을 쓰면 한쪽은 안 보이고
    // 다른 쪽은 존을 통째로 덮는다. 그래서 존의 월드 최단 변에 비례시키고 상·하한으로 자른다.
    // 기본값 검산(WorkshopImport 월드 배율 20 적용 후): ShortPart InkZone 최단 변 1.09m →
    // 폭 0.16m에 길이 3.7m, InkWood InkZone 2m → 상한 직전 0.30m에 길이 18m, LongPart 하위
    // 끌 존은 하한 0.01m로 잘린다.
    [Tooltip("라인 폭 = 존 박스의 월드 최단 변 × 이 비율. 아래 상·하한으로 자른다.")]
    [SerializeField, Range(0.01f, 1f)] float guideLineWidthRatio = 0.15f;

    [Tooltip("라인 폭의 하한(월드 m).")]
    [SerializeField, Min(0.001f)] float guideLineMinWidth = 0.01f;

    [Tooltip("라인 폭의 상한(월드 m).")]
    [SerializeField, Min(0.001f)] float guideLineMaxWidth = 0.4f;

    [Tooltip("라인을 작업면에서 띄우는 거리(라인 폭 배수). z-fighting과 면 관통을 막는다.")]
    [SerializeField, Min(0f)] float guideLineSurfaceOffset = 0.75f;

    // MediumPart·LongPart 하위 대패·끌 존은 월드 최장 변이 0.015~0.10유닛(체감 1~8mm)이다.
    // 라인 폭·길이가 하한(guideLineMinWidth)으로 잘려도 사실상 화면에 찍히지 않아 "안내가 안
    // 보인다"는 보고가 됐다. 그런 존은 라인을 포기하고 존 중심에 최소 가시 크기의 마커를 세운다.
    [Tooltip("존의 월드 최장 변이 이 값(월드 유닛) 미만이면 안내 라인 대신 마커를 세운다. 0이면 항상 라인.")]
    [SerializeField, Min(0f)] float guideMarkerZoneThreshold = 0.5f;

    [Tooltip("작은 존 마커의 월드 지름(유닛). 존 크기와 무관하게 이 크기로 보인다.")]
    [SerializeField, Min(0.01f)] float guideMarkerWorldSize = 1.2f;

    [Header("도구 하이라이트")]
    [Tooltip("현재 목표가 요구하는 도구를 외곽선(또는 박스 프록시)으로 강조한다. " +
             "쥐고 있는 동안에는 꺼지고, 놓으면 목표가 바뀌기 전까지 다시 켜진다.")]
    [SerializeField] bool highlightRequiredTools = true;

    [Tooltip("열린 결합 자리와 함께 그 자리에 들어갈 부재도 강조한다. 끄면 자리만 짚는다.")]
    [SerializeField] bool highlightAssemblyParts = true;

    [Header("개발 도구")]
    [SerializeField] bool logGuides;

    readonly Dictionary<GameObject, HighlightEntry> _entries = new();
    readonly List<ProcessGuideAnchor> _anchors = new();
    readonly List<GameObject> _highlighted = new();
    readonly List<GameObject> _buffer = new();
    readonly List<GameObject> _createdProxies = new();
    readonly List<GameObject> _createdLines = new();
    readonly List<Material> _createdMaterials = new();
    readonly List<Material> _createdLineMaterials = new();
    readonly List<Mesh> _createdMeshes = new();
    readonly List<Vector3> _pathBuffer = new();

    /// <summary>씬의 도구 목록. 목표가 바뀔 때마다 다시 찾지 않도록 한 번만 수집한다.</summary>
    readonly List<ToolEntry> _tools = new();

    /// <summary>잡기 이벤트에서 도구를 되찾기 위한 역인덱스.</summary>
    readonly Dictionary<Grabbable, ToolEntry> _toolsByGrab = new();

    /// <summary>도구 GameObject → 작업 종류. 존이 아닌 대상의 색을 정할 때 쓴다.</summary>
    readonly Dictionary<GameObject, GuideCraft> _toolCrafts = new();

    bool _toolsCollected;

    bool _registered;
    bool _visible;
    float _hideAt;

    string _retryObjectiveId;
    int _retryCount;
    bool _retryGuideShown;

    // 마지막으로 표시한 요청. 존 잠금 상태가 바뀌면 같은 요청으로 대상만 다시 수집한다.
    ProcessId _lastProcess;
    string _lastObjectiveId;

    /// <summary>현재 강조가 떠 있는지.</summary>
    public bool IsShowing => _visible;

    public static bool TryGetInstance(out ProcessGuideService value)
    {
        value = _instance;
        return value != null;
    }

    void Awake()
    {
        if (questManager == null)
            questManager = FindAnyObjectByType<QuestManager>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning(
                $"[Guide] 씬에 ProcessGuideService가 둘 이상 있습니다. '{name}'은 등록하지 않습니다.", this);
            return;
        }

        _instance = this;
        _registered = true;

        // 잠긴 존은 하이라이트에서 제외하고, 잠금이 풀리면 표시 중인 강조를 갱신한다.
        PartZoneGate.ZoneLockChanged += HandleZoneLockChanged;

        if (questManager == null)
            questManager = FindAnyObjectByType<QuestManager>(FindObjectsInactive.Include);

        if (questManager == null) return;

        questManager.ObjectiveChanged += HandleObjectiveChanged;
        questManager.RetryHinted += HandleRetryHinted;
        questManager.QuestChanged += HandleQuestChanged;
        questManager.Completed += HandleCompleted;
    }

    void OnDisable()
    {
        if (!_registered) return;
        _registered = false;

        PartZoneGate.ZoneLockChanged -= HandleZoneLockChanged;

        if (questManager != null)
        {
            questManager.ObjectiveChanged -= HandleObjectiveChanged;
            questManager.RetryHinted -= HandleRetryHinted;
            questManager.QuestChanged -= HandleQuestChanged;
            questManager.Completed -= HandleCompleted;
        }

        ReleaseTools();

        Hide();
        if (_instance == this) _instance = null;
    }

    void OnDestroy()
    {
        foreach (var proxy in _createdProxies) DestroyCreated(proxy);
        foreach (var line in _createdLines) DestroyCreated(line);
        foreach (var material in _createdMaterials) DestroyCreated(material);
        foreach (var material in _createdLineMaterials) DestroyCreated(material);
        foreach (var mesh in _createdMeshes) DestroyCreated(mesh);

        _createdProxies.Clear();
        _createdLines.Clear();
        _createdMaterials.Clear();
        _createdLineMaterials.Clear();
        _createdMeshes.Clear();
        _entries.Clear();
    }

    void Update()
    {
        if (!_visible) return;

        // 일시정지 구간을 뺀 시각으로 재므로(ISSUE-006과 같은 규약) 메뉴를 열어 둔 사이에
        // 강조가 조용히 사라지지 않는다. 상시 표시 모드에서는 _hideAt이 무한대라 여기 걸리지 않는다.
        if (PauseService.Now >= _hideAt)
        {
            Hide();
            return;
        }

        TickProxyPulse();
        TickSignalWatch();
    }

    string _watchedSignal;
    float _watchedCount;
    float _nextSignalCheck;

    /// <summary>
    /// 같은 목표 안에서 수량만 쌓이는 진행(조립 1→18→37, 존 n/3)을 감시한다. 이때는 목표 전환
    /// 이벤트가 오지 않으므로, 신호 카운트가 오르면 마지막 요청 그대로 재수집한다 — 방금 조립된
    /// 부재·완료된 존의 강조가 꺼지고 새로 열린 자리와 부재가 켜진다.
    /// </summary>
    void TickSignalWatch()
    {
        if (PauseService.Now < _nextSignalCheck) return;
        _nextSignalCheck = PauseService.Now + 0.25f;

        var signal = CurrentSignal();
        if (string.IsNullOrWhiteSpace(signal)) return;

        var count = ProcessSignalBus.Read(signal);
        if (string.Equals(signal, _watchedSignal, StringComparison.Ordinal) && count == _watchedCount)
            return;

        var refresh = string.Equals(signal, _watchedSignal, StringComparison.Ordinal);
        _watchedSignal = signal;
        _watchedCount = count;

        // 신호가 바뀐 직후(목표 전환)는 ObjectiveChanged가 이미 재수집했으므로 카운트만 기억한다.
        if (refresh) HandleZoneLockChanged(null, false);
    }

    /// <summary>
    /// 프록시 면·안내 라인의 알파를 각자의 기준값 주변에서 은은하게 진동시켜 정적인 반투명 판보다
    /// 시선을 끌게 한다. 외곽선 색은 건드리지 않는다. 두 계열의 기준 알파가 다르므로(면은 옅게,
    /// 라인은 진하게) 재질 목록을 나눠 같은 위상의 파형만 공유한다.
    /// PauseService.Now 기준이라 일시정지 중에는 멈춘다.
    /// </summary>
    void TickProxyPulse()
    {
        var wave = proxyPulseRatio > 0f
            ? Mathf.Sin(PauseService.Now * (2f * Mathf.PI / proxyPulsePeriod))
            : 0f;

        ApplyPulseAlpha(_createdMaterials, proxyBaseAlpha, wave);
        ApplyPulseAlpha(_createdLineMaterials, guideLineAlpha, wave);
    }

    void ApplyPulseAlpha(List<Material> materials, float baseAlpha, float wave)
    {
        var alpha = Mathf.Clamp01(baseAlpha * (1f + proxyPulseRatio * wave));

        for (var i = 0; i < materials.Count; i++)
        {
            var material = materials[i];
            if (material == null) continue;

            if (material.HasProperty(BaseColorId))
            {
                var color = material.GetColor(BaseColorId);
                color.a = alpha;
                material.SetColor(BaseColorId, color);
            }

            if (material.HasProperty(ColorId))
            {
                var color = material.GetColor(ColorId);
                color.a = alpha;
                material.SetColor(ColorId, color);
            }
        }
    }

    // ---- 공개 API ----

    /// <summary>
    /// 공정 가이드를 띄운다. <paramref name="objectiveId"/>를 지목한 앵커가 있으면 그것만,
    /// 없으면 공정 전체용 앵커를 강조한다. 비워 두면 현재 퀘스트 노드를 목표로 본다.
    /// </summary>
    /// <returns>강조할 대상을 하나라도 찾아 표시했으면 true.</returns>
    public bool ShowGuide(ProcessId process, string objectiveId = null)
    {
        // 일시정지·컷씬 중에는 새로 띄우지 않는다. 이미 떠 있는 강조는 건드리지 않는다.
        if (PauseService.IsPaused) return false;
        if (CutsceneDirector.TryGetInstance(out var director) && director.IsPlaying) return false;

        if (string.IsNullOrWhiteSpace(objectiveId) && questManager != null)
            objectiveId = questManager.CurrentNode?.Id;

        _buffer.Clear();
        CollectTargets(process, objectiveId, _buffer);
        if (_buffer.Count == 0)
        {
            if (logGuides)
                Debug.Log($"[Guide] {process} 가이드를 띄울 앵커가 없습니다.", this);
            return false;
        }

        Apply(_buffer);
        _hideAt = alwaysShowCurrentObjective ? float.PositiveInfinity : PauseService.Now + displaySeconds;
        _visible = true;
        _lastProcess = process;
        _lastObjectiveId = objectiveId;

        if (logGuides)
            Debug.Log(
                $"[Guide] {process} 가이드 {_buffer.Count}곳 강조 " +
                (alwaysShowCurrentObjective ? "(목표가 바뀔 때까지 상시)." : $"({displaySeconds:0.#}초)."),
                this);

        return true;
    }

    /// <summary>강조를 즉시 끈다.</summary>
    public void Hide()
    {
        ClearHighlights();
        _visible = false;
    }

    // ---- 자동 발동 ----

    void HandleObjectiveChanged(QuestNodeData node, int index)
    {
        ResetRetry(node?.Id);
        if (questManager == null) return;

        var process = questManager.Process;

        // 상시 표시 모드: 목표가 바뀔 때마다 새 목표의 대상으로 갱신한다. 새 목표의 대상을 못
        // 찾으면 이전 목표의 강조를 남겨 두지 않는다 — 지난 자리를 계속 짚는 것이 더 큰 오도다.
        if (alwaysShowCurrentObjective)
        {
            if (ShowGuide(process, node?.Id)) AutoShownProcesses.Add(process);
            else if (_visible) Hide();
            return;
        }

        if (AutoShownProcesses.Contains(process)) return;

        // 표시에 성공했을 때만 '봤다'고 기록한다. 컷씬이나 일시정지에 막혀 못 뜬 것을 1회로
        // 세면 그 공정의 최초 안내가 통째로 사라진다.
        if (ShowGuide(process, node?.Id)) AutoShownProcesses.Add(process);
    }

    /// <summary>
    /// 재안내 누적. <see cref="ProcessContextBridge"/>가 실패 1회로 계상하는 것과 같은 신호를
    /// 같은 규약으로 센다 — 문턱에 닿으면 말 대신 가이드가 뜬다.
    /// </summary>
    void HandleRetryHinted(ProcessStepData step)
    {
        var objectiveId = questManager != null ? questManager.CurrentNode?.Id : null;
        if (!string.Equals(objectiveId, _retryObjectiveId, StringComparison.Ordinal))
            ResetRetry(objectiveId);

        _retryCount++;
        if (_retryGuideShown || _retryCount < AiProcessContext.DirectAnswerFailureCount) return;

        var process = questManager != null ? questManager.Process : ProcessId.Tutorial;
        _retryGuideShown = ShowGuide(process, objectiveId);
    }

    void HandleQuestChanged(QuestDefinition definition) => ResetRetry(null);

    void HandleCompleted(ProcessId process) => Hide();

    /// <summary>
    /// 존 잠금 상태 변화. 강조가 떠 있으면 마지막 요청 그대로 대상만 다시 수집한다 — 잠긴 존은
    /// 수집 단계(<see cref="Append"/>)에서 걸러지므로, 재수집이 곧 제외·복귀 갱신이다. 표시 시간은
    /// 늘리지 않는다(_hideAt 유지).
    /// </summary>
    void HandleZoneLockChanged(WorkZone zone, bool locked)
    {
        if (!_visible) return;

        _buffer.Clear();
        CollectTargets(_lastProcess, _lastObjectiveId, _buffer);
        if (_buffer.Count == 0)
        {
            Hide();
            return;
        }

        Apply(_buffer);
    }

    void ResetRetry(string objectiveId)
    {
        _retryObjectiveId = objectiveId;
        _retryCount = 0;
        _retryGuideShown = false;
    }

    // ---- 대상 수집 ----

    void CollectTargets(ProcessId process, string objectiveId, List<GameObject> results)
    {
        var signal = CurrentSignal();

        // 0순위: 현재 목표의 신호 키에서 직접 산출한 대상.
        //
        // 공정이 부재 단위로 바뀌면서 씬에 심어 둔 앵커의 process 값(도구 단위 시절의 것)은 더 이상
        // 지금 공정을 가리키지 않는다. 씬을 고칠 수 없으므로, 신호 키를 아는 목표에서는 앵커를
        // 거치지 않고 그 키에 해당하는 존·결합 자리를 직접 찾는다. 앵커 경로는 그 밖의 목표
        // (튜토리얼 등)용 폴백으로 남는다.
        var found = AppendSignalTargets(signal, results);

        // 같은 신호 키가 쓸 도구도 함께 짚는다. 존을 하나도 못 찾은 목표(전 존 완료 직후 등)에서도
        // 도구만 강조하는 것이 낫다 — 앵커 폴백은 도구 단위 시절의 옛 대상을 짚어 오도한다.
        found += AppendToolTargets(signal, results);
        if (found > 0) return;

        RefreshAnchors();

        // 1순위: 현재 목표를 지목한 앵커.
        if (!string.IsNullOrWhiteSpace(objectiveId))
            AppendAnchors(process, objectiveId, false, results);

        // 2순위: 공정 전체용 앵커(objectiveId 빈 값).
        if (results.Count == 0) AppendAnchors(process, null, false, results);

        // 3순위: 목표를 지목한 앵커만 있고 그 목표가 아닌 경우. 아무것도 못 띄우느니 공정의
        // 앵커 전부를 짚는다.
        if (results.Count == 0) AppendAnchors(process, null, true, results);
    }

    void RefreshAnchors()
    {
        _anchors.Clear();

        var found = FindObjectsByType<ProcessGuideAnchor>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (var i = 0; i < found.Length; i++)
        {
            var anchor = found[i];
            if (anchor == null || anchor.gameObject.scene != gameObject.scene) continue;
            _anchors.Add(anchor);
        }
    }

    void AppendAnchors(ProcessId process, string objectiveId, bool matchAny, List<GameObject> results)
    {
        var wanted = objectiveId ?? string.Empty;

        for (var i = 0; i < _anchors.Count; i++)
        {
            var anchor = _anchors[i];
            if (anchor == null || anchor.Process != process) continue;

            if (!matchAny &&
                !string.Equals(anchor.ObjectiveId ?? string.Empty, wanted, StringComparison.Ordinal))
                continue;

            AppendAnchorTargets(anchor, results);
        }
    }

    void AppendAnchorTargets(ProcessGuideAnchor anchor, List<GameObject> results)
    {
        var targets = anchor.HighlightTargets;
        var added = 0;

        if (targets != null)
            for (var i = 0; i < targets.Count; i++)
                if (Append(results, targets[i]))
                    added++;

        if (added > 0) return;

        // 공포 조립 특례: 다음에 부재가 들어갈 자리는 조립이 진행되며 바뀌므로 정적 앵커로는
        // 적을 수 없다. 표시 시점에 열려 있는 결합 자리를 찾아 그것만 강조한다.
        if (anchor.Process == ProcessId.GongpoPuzzle)
        {
            if (AppendOpenAssemblyTargets(results) > 0) return;

            // 열린 자리가 없으면(조립 완료 직후 등) 빈 앵커 자신을 띄우지 않는다. 렌더러가 있는
            // 실제 오브젝트를 앵커로 놓은 경우에만 폴백한다.
            if (anchor.GetComponentInChildren<Renderer>(true) == null) return;
        }

        Append(results, anchor.gameObject);
    }

    /// <summary>
    /// 현재 목표가 Signal 조건이면 그 키가 가리키는 대상을 직접 모은다. 부재 가공 목표는 그 부재의
    /// 해당 작업 존들(아직 완료되지 않았고 <see cref="PartZoneGate"/>에 잠기지도 않은 것), 조립
    /// 목표는 지금 열려 있는 결합 자리다.
    /// </summary>
    int AppendSignalTargets(string signal, List<GameObject> results)
    {
        if (string.IsNullOrWhiteSpace(signal)) return 0;

        if (string.Equals(signal, PartProcessSignals.PurlinInstall, StringComparison.Ordinal))
            return AppendOpenAssemblyTargets(results, true);

        if (string.Equals(signal, PartProcessSignals.GongpoAssembled, StringComparison.Ordinal))
            return AppendOpenAssemblyTargets(results, false);

        if (!PartProcessSignals.TryGetProcess(signal, out var part)) return 0;

        var added = 0;
        var zones = FindObjectsByType<WorkZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (var i = 0; i < zones.Length; i++)
        {
            var zone = zones[i];
            if (zone == null || zone.gameObject.scene != gameObject.scene || zone.IsCompleted) continue;
            if (!PartProcessSignals.TryResolvePart(zone, out var zonePart) || zonePart != part) continue;

            var zoneSignal = PartProcessSignals.SignalForZone(zonePart, PartProcessSignals.CraftOfZone(zone));
            if (!string.Equals(zoneSignal, signal, StringComparison.Ordinal)) continue;

            if (Append(results, zone.gameObject)) added++;
        }

        return added;
    }

    /// <summary>현재 목표의 신호 키. Signal 조건이 아니거나 비어 있으면 null.</summary>
    string CurrentSignal()
    {
        var objective = questManager != null ? questManager.CurrentObjective : null;
        if (objective == null || objective.Condition != StepCondition.Signal) return null;

        return string.IsNullOrWhiteSpace(objective.Target) ? null : objective.Target;
    }

    // ---- 도구 하이라이트 ----

    /// <summary>
    /// 지금 목표가 요구하는 도구를 강조 대상에 더한다. 판정표는
    /// <see cref="PartProcessSignals.ToolsForSignal"/> 하나뿐이라 <see cref="MainPlayProcessBridge"/>가
    /// 잠금을 푸는 도구와 여기서 강조하는 도구가 항상 같다. 조립 목표(도구가 아니라 부재를 여는
    /// 목표)와 튜토리얼처럼 표에 없는 키는 <see cref="PartToolMask.None"/>이라 그냥 지나간다.
    /// </summary>
    int AppendToolTargets(string signal, List<GameObject> results)
    {
        if (!highlightRequiredTools) return 0;

        var mask = PartProcessSignals.ToolsForSignal(signal);
        if (mask == PartToolMask.None) return 0;

        RefreshTools();

        var added = 0;
        for (var i = 0; i < _tools.Count; i++)
        {
            var tool = _tools[i];
            if (tool.Target == null || (tool.Mask & mask) == 0) continue;
            if (Append(results, tool.Target)) added++;
        }

        return added;
    }

    /// <summary>
    /// 씬의 도구를 한 번만 수집한다. 도구는 공정 중에 생기거나 사라지지 않으므로 목표가 바뀔 때마다
    /// 다시 찾을 이유가 없고, 잡기 이벤트 구독도 수집 시점에 한 번만 걸면 된다.
    /// </summary>
    void RefreshTools()
    {
        if (_toolsCollected) return;
        _toolsCollected = true;

        CollectTools<InkLineTool>(PartToolMask.Ink, GuideCraft.Makmeok);
        CollectTools<SawTool>(PartToolMask.Saw, GuideCraft.Sawing);
        CollectTools<FlatPlaneTool>(PartToolMask.FlatPlane, GuideCraft.Planing);
        CollectTools<CurvedPlaneTool>(PartToolMask.CurvedPlane, GuideCraft.Planing);
        CollectTools<AdzeTool>(PartToolMask.Adze, GuideCraft.Adzing);
        CollectTools<ChiselTool>(PartToolMask.Chisel, GuideCraft.Chiseling);
        CollectTools<HammerTool>(PartToolMask.Hammer, GuideCraft.Chiseling);
    }

    void CollectTools<T>(PartToolMask mask, GuideCraft craft) where T : Component
    {
        foreach (var tool in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tool == null || tool.gameObject.scene != gameObject.scene) continue;

            var target = tool.gameObject;
            var known = false;

            // 한 오브젝트가 두 도구를 겸하는 배치(끌+망치 등)에 대비해 비트만 합친다.
            for (var i = 0; i < _tools.Count; i++)
            {
                if (_tools[i].Target != target) continue;
                _tools[i].Mask |= mask;
                known = true;
                break;
            }

            if (known) continue;

            var entry = new ToolEntry
            {
                Target = target,
                Grab = tool.GetComponentInParent<Grabbable>(true),
                Mask = mask,
                Craft = craft
            };

            _tools.Add(entry);
            _toolCrafts[target] = craft;

            if (entry.Grab == null || !_toolsByGrab.TryAdd(entry.Grab, entry)) continue;

            entry.Grab.Grabbed += HandleToolGrabbed;
            entry.Grab.Released += HandleToolReleased;
        }
    }

    void ReleaseTools()
    {
        foreach (var pair in _toolsByGrab)
        {
            if (pair.Key == null) continue;
            pair.Key.Grabbed -= HandleToolGrabbed;
            pair.Key.Released -= HandleToolReleased;
        }

        _toolsByGrab.Clear();
        _tools.Clear();
        _toolCrafts.Clear();
        _toolsCollected = false;
    }

    /// <summary>이미 쥔 도구는 강조하지 않는다 — 손에 든 것을 계속 짚는 것은 안내가 아니라 잔상이다.</summary>
    void HandleToolGrabbed(Grabbable grabbable, GrabHandModule hand) => SetToolHighlight(grabbable, false);

    /// <summary>놓으면 다시 켠다. 목표가 이미 바뀌었으면 대상 목록에 없으므로 아무 일도 하지 않는다.</summary>
    void HandleToolReleased(Grabbable grabbable, GrabHandModule hand) => SetToolHighlight(grabbable, true);

    void SetToolHighlight(Grabbable grabbable, bool value)
    {
        if (!_visible || grabbable == null) return;
        if (!_toolsByGrab.TryGetValue(grabbable, out var tool) || tool.Target == null) return;
        if (!_highlighted.Contains(tool.Target)) return;
        if (_entries.TryGetValue(tool.Target, out var entry)) SetHighlighted(entry, value);
    }

    /// <summary>강조를 켤 시점에 이미 쥐고 있는 도구인지.</summary>
    bool IsHeldTool(GameObject target)
    {
        for (var i = 0; i < _tools.Count; i++)
            if (_tools[i].Target == target)
                return _tools[i].Grab != null && _tools[i].Grab.IsHeld;

        return false;
    }

    /// <summary>
    /// 지금 부재를 받을 수 있는 결합 자리. <see cref="AssemblySnapModule.IsOpen"/>이 공개돼 있어
    /// 판정 코드를 건드리지 않고 읽을 수 있다. Snap 모듈은 Awake에서 만들어지므로 런타임 전용이다.
    /// </summary>
    /// <param name="purlinOnly">
    /// true면 도리를 받는 자리만, false면 도리가 아닌 자리만. null 판정이 없는 이유는 도리 설치와
    /// 공포 조립이 한 공정에 합쳐졌어도 목표는 여전히 나뉘어 있기 때문이다.
    /// </param>
    int AppendOpenAssemblyTargets(List<GameObject> results, bool? purlinOnly = null)
    {
        var added = 0;
        var targets = FindObjectsByType<AssemblyTarget>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        MaleSnapPoint[] males = null;

        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target == null || target.gameObject.scene != gameObject.scene) continue;
            if (target.Snap == null || !target.Snap.IsOpen) continue;

            if (purlinOnly.HasValue)
            {
                var isPurlin = string.Equals(
                    target.AcceptedPartID, MainPlayProcessBridge.PurlinPartId, StringComparison.Ordinal);
                if (isPurlin != purlinOnly.Value) continue;
            }

            if (Append(results, target.gameObject)) added++;

            if (!highlightAssemblyParts) continue;

            males ??= FindObjectsByType<MaleSnapPoint>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            added += AppendAssemblyPartFor(target, males, results);
        }

        return added;
    }

    /// <summary>
    /// 열린 결합 자리에 들어갈 부재를 함께 짚는다. 자리만 강조하면 "어디에"는 알아도 "무엇을"을
    /// 알 수 없어 38개짜리 부재 더미에서 맞는 것을 고를 수 없다(실기 보고: 결합 위치에는 하이라이트가
    /// 들어오는데 집어야 할 부재 쪽에는 없음).
    ///
    /// 고르는 기준은 <see cref="MainPlayProcessBridge"/>의 잠금과 같은 판정을 재사용한다 —
    /// 지금 목표에서 잡을 수 없는 부재(<see cref="Grabbable.CanGrab"/> false)는 대상이 아니므로,
    /// 강조되는 부재와 실제로 집히는 부재가 어긋날 수 없다. 리드 결정(2026-08-27)으로 자리에
    /// 들어갈 수 있는 부재는 **전부** 강조한다 — 어느 것을 집어도 되는 것이 사실이고, 하나만
    /// 짚으면 나머지가 "안 되는 부재"로 오독된다.
    /// </summary>
    int AppendAssemblyPartFor(AssemblyTarget target, MaleSnapPoint[] males, List<GameObject> results)
    {
        if (males == null || string.IsNullOrWhiteSpace(target.AcceptedPartID)) return 0;

        var added = 0;

        for (var i = 0; i < males.Length; i++)
        {
            var male = males[i];
            if (male == null || male.gameObject.scene != gameObject.scene) continue;
            if (!string.Equals(male.mySnapID, target.AcceptedPartID, StringComparison.Ordinal)) continue;

            var part = male.GetComponentInParent<Grabbable>(true);
            if (part == null || !part.CanGrab || part.IsHeld) continue;

            var assembly = part.GetComponent<AssemblyPart>() ??
                           part.GetComponentInChildren<AssemblyPart>(true);
            if (assembly != null && assembly.isAssembled) continue;

            if (Append(results, part.gameObject)) added++;
        }

        return added;
    }

    static bool Append(List<GameObject> results, GameObject value)
    {
        if (value == null || results.Contains(value)) return false;

        // 선행 존이 끝나지 않아 잠긴 존은 강조하지 않는다. 작업할 수 없는 곳을 짚으면 안내가
        // 아니라 혼선이다. 해제되면 HandleZoneLockChanged의 재수집으로 복귀한다.
        if (PartZoneGate.IsLocked(value)) return false;

        results.Add(value);
        return true;
    }

    // ---- 강조 ----

    void Apply(List<GameObject> targets)
    {
        ClearHighlights();

        for (var i = 0; i < targets.Count; i++)
        {
            var entry = Resolve(targets[i]);
            if (entry == null) continue;

            // 이미 쥔 도구는 목록에는 남기되 꺼 둔다. 놓는 순간 Released가 다시 켠다.
            SetHighlighted(entry, !IsHeldTool(targets[i]));
            _highlighted.Add(targets[i]);
        }
    }

    void ClearHighlights()
    {
        for (var i = 0; i < _highlighted.Count; i++)
            if (_entries.TryGetValue(_highlighted[i], out var entry))
                SetHighlighted(entry, false);

        _highlighted.Clear();
    }

    HighlightEntry Resolve(GameObject target)
    {
        if (target == null) return null;
        if (_entries.TryGetValue(target, out var entry) && (entry.Outline != null || entry.Line != null))
            return entry;

        var color = ColorFor(CraftOf(target));

        // 존은 라인으로 안내한다. 박스 프록시는 존의 부피를 통째로 칠할 뿐이라 "이 선을 그어라",
        // "이 자리를 파라"를 전달하지 못한다. 라인 경로를 만들 수 없는 존(BoxCollider가 없거나
        // 축을 판정할 수 없는 경우)만 종전 프록시로 폴백한다.
        if (target.TryGetComponent<WorkZone>(out var zone))
        {
            // 라인으로는 보이지 않을 만큼 작은 존은 마커로 대신한다. 판정은 표시 방식만 바꾸며
            // 존 자체(콜라이더·완료 판정)는 건드리지 않는다.
            if (IsSubVisibleZone(target, out var markerCenter))
            {
                var marker = CreateMarker(target, markerCenter, color);
                var markerOutline = marker.AddComponent<Outline>();
                markerOutline.enabled = false;
                Configure(markerOutline, color);

                entry = new HighlightEntry { Outline = markerOutline, Proxy = marker };
                _entries[target] = entry;
                return entry;
            }

            var line = CreateGuideLine(target, zone, color);
            if (line != null)
            {
                entry = new HighlightEntry { Line = line };
                _entries[target] = entry;
                return entry;
            }
        }

        var outline = target.GetComponent<Outline>();
        GameObject proxy = null;

        if (outline == null &&
            (target.GetComponentInChildren<Renderer>(true) == null || HasUnreadableMesh(target)))
        {
            // 판정용 구역은 콜라이더만 있고 그릴 것이 없다. 크기를 본뜬 프록시를 만들어 얹는다.
            // Read/Write가 꺼진 임포트 메시도 QuickOutline이 정점을 못 읽어 에러를 내므로 같은 프록시로 대체한다.
            proxy = CreateProxy(target, color);
            outline = proxy.AddComponent<Outline>();
        }
        else if (outline == null)
        {
            outline = target.AddComponent<Outline>();
        }

        outline.enabled = false;
        Configure(outline, color);

        entry = new HighlightEntry
        {
            Outline = outline,
            Proxy = proxy
        };
        _entries[target] = entry;
        return entry;
    }

    /// <summary>
    /// 하위에 Read/Write가 꺼진 메시가 하나라도 있으면 참. QuickOutline은 스무스 노멀 베이크를 위해
    /// 읽기 가능한 메시가 필요하고, 읽기 불가 메시에 붙이면 접근 에러만 쏟아진다.
    /// </summary>
    static bool HasUnreadableMesh(GameObject target)
    {
        foreach (var filter in target.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null && !filter.sharedMesh.isReadable) return true;
        foreach (var skinned in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skinned.sharedMesh != null && !skinned.sharedMesh.isReadable) return true;
        return false;
    }

    /// <summary>
    /// 대상의 작업 종류. 존 타입 하나로 정해진다 — 존 GameObject에는 WorkZone 파생이 하나만 붙는다.
    /// 도구는 존이 아니므로 <see cref="RefreshTools"/>가 채운 표를 먼저 본다. 도구를 그 작업의
    /// 존과 같은 색으로 칠해야 "이 색 자리를 이 색 도구로"가 성립한다.
    /// </summary>
    GuideCraft CraftOf(GameObject target)
    {
        if (_toolCrafts.TryGetValue(target, out var toolCraft)) return toolCraft;

        if (target.TryGetComponent<InkLineZone>(out _)) return GuideCraft.Makmeok;
        if (target.TryGetComponent<SawZone>(out _)) return GuideCraft.Sawing;
        if (target.TryGetComponent<ChiselZone>(out _)) return GuideCraft.Chiseling;
        if (target.TryGetComponent<PlaneZone>(out _)) return GuideCraft.Planing;
        if (target.TryGetComponent<AdzeZone>(out _)) return GuideCraft.Adzing;
        return GuideCraft.Default;
    }

    Color ColorFor(GuideCraft craft)
    {
        if (craftPalette != null)
            for (var i = 0; i < craftPalette.Length; i++)
                if (craftPalette[i].craft == craft)
                    return craftPalette[i].color;

        return outlineColor;
    }

    void Configure(Outline outline, Color color)
    {
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = color;
        outline.OutlineWidth = outlineWidth;
    }

    static void SetHighlighted(HighlightEntry entry, bool value)
    {
        if (entry == null) return;

        if (entry.Line != null)
        {
            entry.Line.gameObject.SetActive(value);
            return;
        }

        if (entry.Outline == null) return;

        if (entry.Proxy != null)
        {
            if (value)
            {
                entry.Proxy.SetActive(true);
                entry.Outline.enabled = true;
            }
            else
            {
                entry.Outline.enabled = false;
                entry.Proxy.SetActive(false);
            }

            return;
        }

        entry.Outline.enabled = value;
    }

    // ---- 존 안내 라인 ----

    /// <summary>
    /// 존 박스의 로컬 축을 라인 방향으로 볼지 판정할 때 쓰는 최소 세로 성분. 이 값 미만이면
    /// 축이 거의 수평이라 "위쪽 면"을 고를 수 없어 목재 바깥 방향으로 판정을 넘긴다.
    /// </summary>
    const float VerticalFaceThreshold = 0.3f;

    /// <summary>
    /// 라인 끝점이 존 박스 경계에 정확히 닿지 않도록 남기는 여유. 존 모서리와 라인 캡이 겹쳐
    /// 보이는 것을 막는다.
    /// </summary>
    const float LineSpanRatio = 0.9f;

    /// <summary>
    /// 존에 안내 라인을 만든다. 실패하면 null을 돌려 호출자가 종전 프록시로 폴백하게 한다.
    ///
    /// 라인 오브젝트는 존의 자식으로 두되 <c>localScale</c>에 부모 lossyScale의 역수를 넣어
    /// 자신의 lossyScale을 1로 만든다. 존이 비균등 스케일(예: (26.6, 1.09, 4.10))을 물고 있어
    /// 그대로 상속하면 라인 폭이 방향마다 다르게 찌그러지고 <see cref="LineRenderer.widthMultiplier"/>가
    /// 월드 단위와 어긋나기 때문이다. 스케일만 지우고 부모의 위치·회전은 그대로 따르므로 부재가
    /// 움직여도 라인이 함께 간다.
    /// </summary>
    LineRenderer CreateGuideLine(GameObject target, WorkZone zone, Color color)
    {
        if (!target.TryGetComponent<BoxCollider>(out var box)) return null;

        var lossy = target.transform.lossyScale;
        if (Mathf.Abs(lossy.x) < 1e-5f || Mathf.Abs(lossy.y) < 1e-5f || Mathf.Abs(lossy.z) < 1e-5f)
        {
            Debug.LogWarning($"[Guide] '{target.name}'의 스케일이 0이라 안내 라인을 만들지 못합니다.", target);
            return null;
        }

        _pathBuffer.Clear();
        if (!TryBuildZonePath(target, zone, box, _pathBuffer, out var loop, out var faceNormalLocal))
            return null;

        var world = WorldExtents(lossy, box.size);
        var shortest = Mathf.Min(world.x, Mathf.Min(world.y, world.z));
        var width = Mathf.Clamp(shortest * guideLineWidthRatio, guideLineMinWidth, guideLineMaxWidth);

        var host = new GameObject("[ProcessGuideLine]")
        {
            hideFlags = HideFlags.DontSave,
            layer = target.layer
        };

        var hostTransform = host.transform;
        hostTransform.SetParent(target.transform, false);
        hostTransform.localPosition = Vector3.zero;
        hostTransform.localRotation = Quaternion.identity;
        hostTransform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);

        var offsetWorld = target.transform.TransformDirection(faceNormalLocal).normalized *
                          (width * guideLineSurfaceOffset);

        var line = host.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 0;
        line.numCornerVertices = loop ? 2 : 0;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // host의 lossyScale이 1이므로 widthMultiplier가 곧 월드 폭이다.
        line.widthMultiplier = width;
        line.positionCount = _pathBuffer.Count;
        for (var i = 0; i < _pathBuffer.Count; i++)
        {
            var worldPoint = target.transform.TransformPoint(_pathBuffer[i]) + offsetWorld;
            line.SetPosition(i, hostTransform.InverseTransformPoint(worldPoint));
        }

        // 정점 색을 쓰는 셰이더(Sprites/Default 등)로 폴백해도 공정색이 유지되도록 그라디언트에도
        // 같은 색을 넣는다. 알파 펄스는 재질 쪽 _BaseColor에서만 돌린다 — 그라디언트 알파를 1로
        // 두면 두 셰이더 계열 어디서든 최종 알파가 펄스 값 하나로 결정된다.
        var solid = color;
        solid.a = 1f;
        line.startColor = solid;
        line.endColor = solid;

        var material = CreateProxyMaterial(color, guideLineAlpha);
        if (material != null)
        {
            material.name = "Process Guide Line Material";
            line.sharedMaterial = material;
            _createdLineMaterials.Add(material);
        }

        host.SetActive(false);
        _createdLines.Add(host);
        return line;
    }

    /// <summary>
    /// 존 타입별 라인 경로를 존 로컬(BoxCollider) 좌표로 채운다. 좌표는 전부 콜라이더의
    /// <c>center</c>·<c>size</c>에서 산출하므로 존의 회전·스케일은 부모 변환이 알아서 흡수한다.
    /// </summary>
    /// <param name="faceNormalLocal">작업면 바깥쪽 법선(로컬). 라인을 면에서 띄우는 방향이다.</param>
    bool TryBuildZonePath(
        GameObject target, WorkZone zone, BoxCollider box,
        List<Vector3> points, out bool loop, out Vector3 faceNormalLocal)
    {
        loop = false;
        faceNormalLocal = Vector3.up;

        var zoneTransform = target.transform;
        var center = box.center;
        var half = box.size * 0.5f;
        var world = WorldExtents(zoneTransform.lossyScale, box.size);

        switch (zone)
        {
            // 끌·자귀는 방향 개념이 없고 "파낼 자리"가 곧 목표다. 슬래브의 가장 얇은 축을 면
            // 법선으로 보고 그 바깥 면의 사각 외곽을 루프로 두른다.
            case ChiselZone or AdzeZone:
            {
                var faceAxis = SmallestAxis(world);
                faceNormalLocal = SignedFaceAxis(zoneTransform, zone, faceAxis);

                var u = LocalAxes[(faceAxis + 1) % 3] * half[(faceAxis + 1) % 3];
                var v = LocalAxes[(faceAxis + 2) % 3] * half[(faceAxis + 2) % 3];
                var face = center + faceNormalLocal * half[faceAxis];

                points.Add(face + u + v);
                points.Add(face + u - v);
                points.Add(face - u - v);
                points.Add(face - u + v);
                loop = true;
                return true;
            }

            // 톱질 존은 부재를 가로지르는 '절단면' 슬래브다(가장 얇은 축이 부재 길이 방향).
            // 절단선은 그 슬래브가 부재 윗면과 만나는 선이므로, 라인 방향은 sawDirection
            // (SawTool이 TransformDirection으로 읽는 로컬 벡터)이고 높이는 남은 축 중 가장
            // 수직인 축의 위쪽 끝이다.
            case SawZone saw:
            {
                var direction = NormalizedOrFallback(saw.sawDirection, world);
                var faceAxis = MostVerticalAxis(zoneTransform, DominantAxis(direction));
                faceNormalLocal = SignedFaceAxis(zoneTransform, zone, faceAxis);

                AddSegment(points, center + faceNormalLocal * half[faceAxis], direction, half);
                return true;
            }

            // 대패 존은 작업면에 얹힌 얇은 판이다. 면 법선은 가장 얇은 축, 라인 방향은
            // planeDirection(밀어야 하는 방향)이다.
            case PlaneZone plane:
            {
                var direction = NormalizedOrFallback(plane.planeDirection, world);
                var faceAxis = SmallestAxisExcept(world, DominantAxis(direction));
                faceNormalLocal = SignedFaceAxis(zoneTransform, zone, faceAxis);

                AddSegment(points, center + faceNormalLocal * half[faceAxis], direction, half);
                return true;
            }

            // 먹매김은 채점 포인트가 곧 정답 선이다. 배선돼 있으면 그 두 점을 그대로 쓴다.
            // 두 점은 존 아래쪽 면(= 목재 윗면)에 찍혀 있으므로 면 법선 반대쪽이 목재다.
            case InkLineZone ink:
            {
                var faceAxis = MostVerticalAxis(zoneTransform, -1);
                faceNormalLocal = SignedFaceAxis(zoneTransform, zone, faceAxis);

                if (ink.pointA != null && ink.pointB != null)
                {
                    points.Add(zoneTransform.InverseTransformPoint(ink.pointA.position));
                    points.Add(zoneTransform.InverseTransformPoint(ink.pointB.position));
                    return true;
                }

                // 미배선 폴백: 목재에 닿는 면의 중앙선. 방향은 남은 두 축 중 긴 쪽 — 먹줄 존은
                // 선 하나를 감싸는 가늘고 긴 띠로 저작되어 있어 긴 축이 곧 선 방향이다.
                var lineAxis = LargestAxisExcept(world, faceAxis);
                AddSegment(points, center - faceNormalLocal * half[faceAxis], LocalAxes[lineAxis], half);
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>기준점에서 <paramref name="direction"/> 양방향으로 박스 안쪽 끝까지 뻗는 선분.</summary>
    static void AddSegment(List<Vector3> points, Vector3 origin, Vector3 direction, Vector3 half)
    {
        var extent = HalfExtentAlong(half, direction) * LineSpanRatio;
        points.Add(origin + direction * extent);
        points.Add(origin - direction * extent);
    }

    /// <summary>박스 로컬 축별 월드 길이. lossyScale의 부호는 무시한다.</summary>
    static Vector3 WorldExtents(Vector3 lossyScale, Vector3 size) =>
        new(size.x * Mathf.Abs(lossyScale.x),
            size.y * Mathf.Abs(lossyScale.y),
            size.z * Mathf.Abs(lossyScale.z));

    /// <summary>
    /// 작업면 바깥을 향하는 부호를 붙인 로컬 축. 1순위는 월드 위쪽이고(수평면 위의 존),
    /// 축이 거의 수평이면(옆면 존) 목재 중심에서 멀어지는 쪽을 고른다. 존이 180° 뒤집혀 저작된
    /// 경우(Play 씬의 ChiselZoneTop 등)에도 로컬 +Y를 그냥 믿지 않기 위한 판정이다.
    /// </summary>
    static Vector3 SignedFaceAxis(Transform zoneTransform, WorkZone zone, int axis)
    {
        var local = LocalAxes[axis];
        var worldDirection = zoneTransform.TransformDirection(local).normalized;

        if (Mathf.Abs(worldDirection.y) >= VerticalFaceThreshold)
            return worldDirection.y >= 0f ? local : -local;

        var reference = ReferenceCenter(zoneTransform, zone);
        return Vector3.Dot(worldDirection, zoneTransform.position - reference) >= 0f ? local : -local;
    }

    /// <summary>존이 붙어 있는 목재의 중심. 존 바깥 방향을 판정하는 기준점이다.</summary>
    static Vector3 ReferenceCenter(Transform zoneTransform, WorkZone zone)
    {
        var modifier = zone != null && zone.woodModifier != null
            ? zone.woodModifier
            : zoneTransform.GetComponentInParent<VisualWoodModifier>(true);

        if (modifier != null) return modifier.transform.position;
        return zoneTransform.parent != null ? zoneTransform.parent.position : zoneTransform.position;
    }

    /// <summary>0에 가까우면 가장 긴 축으로 대체하고, 아니면 정규화해서 돌려준다.</summary>
    static Vector3 NormalizedOrFallback(Vector3 direction, Vector3 world) =>
        direction.sqrMagnitude < 1e-6f ? LocalAxes[LargestAxis(world)] : direction.normalized;

    static int DominantAxis(Vector3 value)
    {
        var x = Mathf.Abs(value.x);
        var y = Mathf.Abs(value.y);
        var z = Mathf.Abs(value.z);
        if (x >= y && x >= z) return 0;
        return y >= z ? 1 : 2;
    }

    static int SmallestAxis(Vector3 world) => SmallestAxisExcept(world, -1);

    static int SmallestAxisExcept(Vector3 world, int excluded)
    {
        var best = -1;
        for (var i = 0; i < 3; i++)
        {
            if (i == excluded) continue;
            if (best < 0 || world[i] < world[best]) best = i;
        }

        return best;
    }

    static int LargestAxis(Vector3 world) => LargestAxisExcept(world, -1);

    static int LargestAxisExcept(Vector3 world, int excluded)
    {
        var best = -1;
        for (var i = 0; i < 3; i++)
        {
            if (i == excluded) continue;
            if (best < 0 || world[i] > world[best]) best = i;
        }

        return best;
    }

    /// <summary>월드 +Y에 가장 가까운 로컬 축.</summary>
    static int MostVerticalAxis(Transform zoneTransform, int excluded)
    {
        var best = -1;
        var bestDot = -1f;
        for (var i = 0; i < 3; i++)
        {
            if (i == excluded) continue;

            var dot = Mathf.Abs(zoneTransform.TransformDirection(LocalAxes[i]).normalized.y);
            if (best >= 0 && dot <= bestDot) continue;
            best = i;
            bestDot = dot;
        }

        return best;
    }

    /// <summary>중심에서 <paramref name="direction"/> 방향으로 박스 면에 닿을 때까지의 거리.</summary>
    static float HalfExtentAlong(Vector3 half, Vector3 direction)
    {
        var extent = float.PositiveInfinity;
        for (var i = 0; i < 3; i++)
        {
            var component = Mathf.Abs(direction[i]);
            if (component <= 1e-4f) continue;
            extent = Mathf.Min(extent, half[i] / component);
        }

        return float.IsInfinity(extent) ? 0f : extent;
    }

    // ---- 작은 존 마커 ----

    /// <summary>
    /// 라인으로 안내하기에는 너무 작은 존인지. 판정 기준은 존 콜라이더의 <b>월드 최장 변</b>이다 —
    /// 최단 변은 얇은 슬래브 존에서 항상 작아 판별이 되지 않는다.
    /// </summary>
    /// <param name="centerLocal">마커를 세울 존 로컬 좌표(콜라이더 중심).</param>
    bool IsSubVisibleZone(GameObject target, out Vector3 centerLocal)
    {
        centerLocal = Vector3.zero;
        if (guideMarkerZoneThreshold <= 0f) return false;

        var lossy = target.transform.lossyScale;

        if (target.TryGetComponent<BoxCollider>(out var box))
        {
            centerLocal = box.center;
            var world = WorldExtents(lossy, box.size);
            return Mathf.Max(world.x, Mathf.Max(world.y, world.z)) < guideMarkerZoneThreshold;
        }

        if (!target.TryGetComponent<Collider>(out var collider)) return false;

        var bounds = collider.bounds;
        centerLocal = target.transform.InverseTransformPoint(bounds.center);
        var size = bounds.size;
        return Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < guideMarkerZoneThreshold;
    }

    /// <summary>
    /// 존 중심에 세우는 최소 가시 크기의 마커. 존이 아무리 작아도 월드
    /// <see cref="guideMarkerWorldSize"/> 크기로 보이도록 부모 배율을 상쇄한다. 알파 펄스는
    /// 프록시 재질 목록에 등록해 <see cref="TickProxyPulse"/>가 함께 돌린다.
    /// </summary>
    GameObject CreateMarker(GameObject target, Vector3 centerLocal, Color color)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "[ProcessGuideMarker]";
        marker.hideFlags = HideFlags.DontSave;
        marker.layer = target.layer;

        var markerTransform = marker.transform;
        markerTransform.SetParent(target.transform, false);
        markerTransform.localPosition = centerLocal;
        markerTransform.localRotation = Quaternion.identity;

        var lossy = target.transform.lossyScale;
        markerTransform.localScale = new Vector3(
            guideMarkerWorldSize / Mathf.Max(1e-4f, Mathf.Abs(lossy.x)),
            guideMarkerWorldSize / Mathf.Max(1e-4f, Mathf.Abs(lossy.y)),
            guideMarkerWorldSize / Mathf.Max(1e-4f, Mathf.Abs(lossy.z)));

        if (marker.TryGetComponent<Collider>(out var markerCollider))
        {
            markerCollider.enabled = false;
            DestroyCreated(markerCollider);
        }

        // QuickOutline이 UV 채널을 기록하므로 공용 내장 메시를 복제해 쓴다(CreateProxy와 같은 이유).
        var filter = marker.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            var mesh = Instantiate(filter.sharedMesh);
            mesh.name = "Process Guide Marker Mesh";
            mesh.hideFlags = HideFlags.DontSave;
            filter.sharedMesh = mesh;
            _createdMeshes.Add(mesh);
        }

        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = CreateProxyMaterial(color, proxyBaseAlpha);
            if (material != null)
            {
                material.name = "Process Guide Marker Material";
                renderer.sharedMaterial = material;
                _createdMaterials.Add(material);
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        marker.SetActive(false);
        _createdProxies.Add(marker);
        return marker;
    }

    // ---- 프록시(존이 아닌 대상·라인 실패 폴백) ----

    GameObject CreateProxy(GameObject target, Color color)
    {
        var proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        proxy.name = "[ProcessGuideProxy]";
        proxy.hideFlags = HideFlags.DontSave;
        proxy.layer = target.layer;
        proxy.transform.SetParent(target.transform, false);

        if (target.TryGetComponent<BoxCollider>(out var box))
        {
            proxy.transform.localPosition = box.center;
            proxy.transform.localScale = box.size;
        }
        else if (target.TryGetComponent<Collider>(out var collider))
        {
            var bounds = collider.bounds;
            proxy.transform.position = bounds.center;
            proxy.transform.rotation = Quaternion.identity;
            proxy.transform.localScale = target.transform.lossyScale.sqrMagnitude > 1e-6f
                ? new Vector3(
                    bounds.size.x / Mathf.Max(1e-4f, target.transform.lossyScale.x),
                    bounds.size.y / Mathf.Max(1e-4f, target.transform.lossyScale.y),
                    bounds.size.z / Mathf.Max(1e-4f, target.transform.lossyScale.z))
                : bounds.size;
        }
        else
        {
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localScale = Vector3.one * 0.25f;
        }

        if (proxy.TryGetComponent<Collider>(out var proxyCollider))
        {
            proxyCollider.enabled = false;
            DestroyCreated(proxyCollider);
        }

        // QuickOutline이 UV 채널을 기록하므로 공용 내장 Cube 메시를 복제해 쓴다.
        var filter = proxy.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            var mesh = Instantiate(filter.sharedMesh);
            mesh.name = "Process Guide Proxy Mesh";
            mesh.hideFlags = HideFlags.DontSave;
            filter.sharedMesh = mesh;
            _createdMeshes.Add(mesh);
        }

        var renderer = proxy.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = CreateProxyMaterial(color, proxyBaseAlpha);
            if (material != null)
            {
                renderer.sharedMaterial = material;
                _createdMaterials.Add(material);
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        proxy.SetActive(false);
        _createdProxies.Add(proxy);
        return proxy;
    }

    /// <summary>
    /// 프록시 면과 안내 라인이 함께 쓰는 URP Unlit 투명 재질. 기준 알파는 호출부가 정한다 —
    /// 면은 옅게(<see cref="proxyBaseAlpha"/>), 라인은 진하게(<see cref="guideLineAlpha"/>) 쓴다.
    /// </summary>
    Material CreateProxyMaterial(Color outlineColorForCraft, float baseAlpha)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("[Guide] 가이드 프록시용 셰이더를 찾지 못했습니다.", this);
            return null;
        }

        var material = new Material(shader)
        {
            name = "Process Guide Proxy Material",
            hideFlags = HideFlags.DontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        var color = outlineColorForCraft;
        color.a = baseAlpha;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    static void DestroyCreated(UnityEngine.Object value)
    {
        if (value == null) return;

        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        AutoShownProcesses.Clear();
    }
}
