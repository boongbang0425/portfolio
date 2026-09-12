using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 목공 도구 6종의 효과음을 씬 수정 없이 배선한다.
///
/// 씬·프리팹을 건드리지 않는 이유는 빌드 직전이기 때문이다. 도구들은 팀원 소유 프리팹·씬 계층에
/// 있고 클립 하나를 꽂자고 프리팹 인스턴스 오버라이드를 늘리면 병합 충돌 위험만 커진다. 그래서
/// 클립은 <c>Resources</c>에서 로드하고, 씬 로드 직후 <b>필드가 비어 있는 도구에만</b> 주입한다 —
/// 팀원이 나중에 인스펙터로 다른 클립을 넣으면 그쪽이 그대로 우선한다.
///
/// 주입 시점이 <see cref="SceneManager.sceneLoaded"/>인 것은 계약이다. 도구 4종
/// (<c>SawTool</c>·<c>AdzeTool</c>·<c>FlatPlaneTool</c>·<c>CurvedPlaneTool</c>)은 <c>Start</c>에서
/// <c>clip != null</c>일 때만 <c>AudioSource</c>를 만든다. 즉 <b>Start보다 먼저</b> 주입해야 소리가
/// 난다. Unity의 씬 로드 순서는 Awake → OnEnable → sceneLoaded → Start이므로 이 조건을 만족한다.
/// (<c>ChiselTool</c>·<c>InkLineTool</c>은 타격 시점에 <c>PlayClipAtPoint</c>로 필드를 읽으므로
/// 주입 시점에 자유롭다.) 도구는 전부 씬 배치이고 런타임 <c>Instantiate</c> 경로가 없음을 확인했다.
///
/// 톱질 완료음(조각 분리)만 도구가 아니라 존 쪽 이벤트를 탄다. <c>SawZone.CompleteWork</c>는 팀원
/// 소유라 손대지 않고, <see cref="WorkZone.OnWorkCompletedEvent"/>를 구독해 존 위치에서 1회 재생한다
/// — <c>PartZoneGate</c>가 선행 규칙 해제에 쓰는 것과 같은 이벤트다.
///
/// <b>알려진 한계</b>: 도구 자체의 <c>AudioSource</c>와 <c>PlayClipAtPoint</c>는 <c>Core.Audio</c>
/// 버스를 지나지 않으므로 옵션의 환경 볼륨이 적용되지 않는다. 도구 코드를 고쳐야 하는 사안이라
/// 이번 범위에서는 보고만 하고 두었다.
/// </summary>
public static class WoodworkingSfxBootstrap
{
    const string ClipRoot = "Sfx/";

    const string SawLoopClip     = ClipRoot + "saw_loop";
    const string InkSnapClip     = ClipRoot + "ink_snap";
    const string PlaneStrokeClip = ClipRoot + "plane_stroke";
    const string HammerHitClip   = ClipRoot + "hammer_hit";
    const string AdzeHitClip     = ClipRoot + "adze_hit";
    const string WoodCutClip     = ClipRoot + "wood_cut";

    /// <summary>톱질 완료음 볼륨. 원본이 크게 녹음돼 있어 낮춰 재생한다.</summary>
    const float WoodCutVolume = 0.6f;

    /// <summary>
    /// 비어 있는 것과 같이 취급해 덮어쓰는 클립 이름.
    ///
    /// <c>Play.unity</c>의 <c>ChiselTool.hitSound</c>·<c>AdzeTool.hitSound</c>에 XR Interaction
    /// Toolkit 샘플의 <c>Button Pop.wav</c>가 꽂혀 있다. UI 버튼 클릭음이라 끌·자귀 타격음으로
    /// 의도한 선택일 수 없고, 이것을 존중하면 효과음 6종 중 둘이 영영 들리지 않는다. 그래서 이
    /// 목록에 있는 이름만 예외로 덮어쓴다 — 팀원이 넣은 <b>진짜</b> 클립은 여전히 우선한다.
    ///
    /// 씬을 고치지 않는 대신 코드에 예외를 둔 것이므로, 씬에서 필드를 비우면 이 목록도 지워야 한다.
    /// </summary>
    static readonly HashSet<string> PlaceholderClipNames = new() { "Button Pop" };

    /// <summary>주입 대상 판정. 비었거나 알려진 플레이스홀더면 덮어쓴다.</summary>
    static bool ShouldInject(AudioClip current) =>
        current == null || PlaceholderClipNames.Contains(current.name);

    /// <summary>이미 완료 이벤트를 구독한 존. 추가 로드·재진입 시 이중 구독을 막는다.</summary>
    static readonly HashSet<SawZone> SubscribedZones = new();

    static readonly Dictionary<string, AudioClip> ClipCache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// 첫 씬 폴백. 에디터에서 이미 열려 있는 씬으로 Play를 누르는 등 <see cref="OnSceneLoaded"/>가
    /// 오지 않는 경우를 대비한다. AfterSceneLoad도 Start보다는 앞이라 주입 시점 계약을 만족하고,
    /// 주입·구독 모두 멱등이라 중복 실행돼도 무해하다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyToActiveScene() => Apply();

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    static void Apply()
    {
        WireSaws();
        WireInkLines();
        WirePlanes();
        WireChisels();
        WireAdzes();
        WireSawZones();
        WireParticleScale();
    }

    /// <summary>
    /// 도구에 붙은 파티클(톱밥·나무 부스러기)의 스케일 모드를 Hierarchy로 바꾼다. 원본은 1× 씬
    /// 기준으로 저작돼 기본값(Local)인데, Local은 부모 스케일을 무시하므로 ×20 작업장에서는 눈에
    /// 안 보이는 1/20 크기로 나온다. Hierarchy면 도구의 lossyScale을 따라 크기·속도가 함께 커져
    /// 씬 배율과 무관하게 같은 비율로 보인다. Local이 아닌 값은 저작 의도로 보고 존중한다.
    /// </summary>
    static void WireParticleScale()
    {
        foreach (var tool in EnumerateParticleOwners())
        {
            if (tool == null) continue;
            foreach (var ps in tool.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.scalingMode == ParticleSystemScalingMode.Local)
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
    }

    static IEnumerable<Component> EnumerateParticleOwners()
    {
        foreach (var t in Object.FindObjectsByType<SawTool>(FindObjectsInactive.Include, FindObjectsSortMode.None)) yield return t;
        foreach (var t in Object.FindObjectsByType<FlatPlaneTool>(FindObjectsInactive.Include, FindObjectsSortMode.None)) yield return t;
        foreach (var t in Object.FindObjectsByType<CurvedPlaneTool>(FindObjectsInactive.Include, FindObjectsSortMode.None)) yield return t;
        foreach (var t in Object.FindObjectsByType<ChiselTool>(FindObjectsInactive.Include, FindObjectsSortMode.None)) yield return t;
        foreach (var t in Object.FindObjectsByType<AdzeTool>(FindObjectsInactive.Include, FindObjectsSortMode.None)) yield return t;
    }

    // ── 도구별 필드 주입 ────────────────────────────────────────────────────

    static void WireSaws()
    {
        var tools = Object.FindObjectsByType<SawTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (tools.Length == 0) return;

        var clip = Load(SawLoopClip);
        if (clip == null) return;

        foreach (var tool in tools)
            if (tool != null && ShouldInject(tool.sawSound))
                tool.sawSound = clip;
    }

    static void WireInkLines()
    {
        var tools = Object.FindObjectsByType<InkLineTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (tools.Length == 0) return;

        var clip = Load(InkSnapClip);
        if (clip == null) return;

        foreach (var tool in tools)
            if (tool != null && ShouldInject(tool.snapSound))
                tool.snapSound = clip;
    }

    static void WirePlanes()
    {
        var flats   = Object.FindObjectsByType<FlatPlaneTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var curveds = Object.FindObjectsByType<CurvedPlaneTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (flats.Length == 0 && curveds.Length == 0) return;

        var clip = Load(PlaneStrokeClip);
        if (clip == null) return;

        // 평대패·배대패는 스트로크 동작이 같아 같은 클립을 쓴다.
        foreach (var tool in flats)
            if (tool != null && ShouldInject(tool.planeSound))
                tool.planeSound = clip;

        foreach (var tool in curveds)
            if (tool != null && ShouldInject(tool.planeSound))
                tool.planeSound = clip;
    }

    static void WireChisels()
    {
        var tools = Object.FindObjectsByType<ChiselTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (tools.Length == 0) return;

        // 끌은 망치로 치는 도구다. 소리의 정체는 망치 타격음이고, 재생 주체가 ChiselTool일 뿐이다.
        var clip = Load(HammerHitClip);
        if (clip == null) return;

        foreach (var tool in tools)
            if (tool != null && ShouldInject(tool.hitSound))
                tool.hitSound = clip;
    }

    static void WireAdzes()
    {
        var tools = Object.FindObjectsByType<AdzeTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (tools.Length == 0) return;

        var clip = Load(AdzeHitClip);
        if (clip == null) return;

        foreach (var tool in tools)
            if (tool != null && ShouldInject(tool.hitSound))
                tool.hitSound = clip;
    }

    // ── 톱질 완료음 ─────────────────────────────────────────────────────────

    static void WireSawZones()
    {
        var zones = Object.FindObjectsByType<SawZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (zones.Length == 0) return;

        // 완료 순간에 로드하면 조각이 떨어지는 프레임에 디스크 히치가 생긴다. 미리 채워 둔다.
        Load(WoodCutClip);

        SubscribedZones.RemoveWhere(zone => zone == null);

        foreach (var zone in zones)
        {
            if (zone == null || !SubscribedZones.Add(zone)) continue;

            var captured = zone;
            captured.OnWorkCompletedEvent += _ => PlayWoodCut(captured);
        }
    }

    static void PlayWoodCut(SawZone zone)
    {
        var clip = Load(WoodCutClip);
        if (clip == null) return;

        var position = zone != null ? zone.transform.position : Vector3.zero;
        // 유니티의 PlayClipAtPoint는 min 1 / max 500 / Logarithmic을 박아 두므로 ×12 씬에서
        // 감쇠가 8cm부터 시작한다. 씬 배율을 반영하는 경로로 재생한다.
        SceneWorldScale.PlayClipAtPoint(clip, position, WoodCutVolume);
    }

    // ── 클립 로드 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 없는 클립도 캐시에 null로 적어 둔다. 매 씬·매 타격마다 실패한 <c>Resources.Load</c>를
    /// 반복하지 않기 위해서다.
    /// </summary>
    static AudioClip Load(string path)
    {
        if (ClipCache.TryGetValue(path, out var cached)) return cached;

        var clip = Resources.Load<AudioClip>(path);
        if (clip == null)
            Debug.LogWarning($"[WoodworkingSfx] 클립을 찾지 못했습니다: Resources/{path}");

        ClipCache[path] = clip;
        return clip;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        SubscribedZones.Clear();
        ClipCache.Clear();
    }
}
