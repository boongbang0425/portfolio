using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 튜토리얼을 플레이스홀더 큐브 대신 작업장 실물로 돌리고, WorkshopImport의 x 오프셋을 정규화한다.
///
/// <see cref="TutorialImportBuilder"/>가 TutorialScene에서 가져온 구성은 판정 오브젝트(QuestManager·
/// QuestHud)와 연습용 소품(Tool_Saw·Table·Marker_*)이 섞여 있다. 소품은 작업장 실물이 들어오기 전의
/// 임시 대역이므로, 실물이 있는 지금은 판정 대상만 실물로 옮기고 소품은 지운다.
///
/// 소켓(Socket_Bench)도 함께 지운다. 실물 작업대가 이미 물건으로 차 있어 톱을 내려놓을 자리를 낼 수
/// 없다는 판단으로 quest.json에서 place 목표가 빠졌고, 그와 함께 소켓도 쓸 데가 없어졌다.
///
/// 씬 파일은 직접 건드리지 않는다. 열려 있는 씬에서만 작업하고 저장은 하지 않는다 — 결과를 눈으로
/// 확인한 뒤 R이 Ctrl+S로 저장한다. 모든 변경은 Undo에 실린다.
///
/// 실행 순서를 1→3으로 고정한다. 실물 톱에 ProcessTarget(tool_saw)을 붙이는 1번과 같은 키를 가진
/// 플레이스홀더를 지우는 2번이 뒤집히면 안 되기 때문이다. 다만 <see cref="ProcessTarget"/>의 등록은
/// OnEnable에서 일어나는 런타임 동작이라, 에디터에서 잠시 두 벌이 공존해도 실제 중복 등록으로는
/// 이어지지 않는다.
/// </summary>
static class TutorialRealSwapTool
{
    const string MenuPath = "Tools/PROJECT 이음/튜토리얼 실물 전환·작업장 정규화";
    const string Tag = "[TutorialRealSwap]";

    const string WorkshopRootName = "WorkshopImport";
    const string TutorialRootName = "TutorialImport";
    const string SawTargetKey = "tool_saw";

    /// <summary>루트의 x·z를 0으로 내린다. y는 바닥 높이라 그대로 둔다.</summary>
    static readonly Vector3 WorkshopNormalizedPosition = new(0f, 13.1f, 0f);

    /// <summary>월드 좌표 복원 허용 오차(미터). 이 값을 넘으면 경고로 남긴다.</summary>
    const float PoseTolerance = 0.001f;

    /// <summary>회전 복원 허용 오차(도).</summary>
    const float AngleTolerance = 0.01f;

    /// <summary>지울 오브젝트 하나의 지정. 이름만으로 지우지 않도록 찾는 범위와 조건을 함께 적는다.</summary>
    readonly struct PlaceholderSpec
    {
        public PlaceholderSpec(string name, bool searchWholeScene = false, Type requiredComponent = null,
            Type[] extraAllowedBehaviours = null)
        {
            Name = name;
            SearchWholeScene = searchWholeScene;
            RequiredComponent = requiredComponent;
            ExtraAllowedBehaviours = extraAllowedBehaviours ?? Array.Empty<Type>();
        }

        /// <summary>정확히 일치해야 하는 이름.</summary>
        public string Name { get; }

        /// <summary>true면 씬 전체, false면 <see cref="TutorialRootName"/>의 직계 자식만 본다.</summary>
        public bool SearchWholeScene { get; }

        /// <summary>이 컴포넌트가 없으면 같은 이름이어도 건드리지 않는다. null이면 조건 없음.</summary>
        public Type RequiredComponent { get; }

        /// <summary>이 대상에 한해 추가로 허용하는 MonoBehaviour. 전역 허용 목록을 넓히지 않는다.</summary>
        public Type[] ExtraAllowedBehaviours { get; }
    }

    /// <summary>
    /// 지울 대상. 이름 화이트리스트가 아니라 삭제 대상 화이트리스트다 — 여기 없는 것은 무슨 일이
    /// 있어도 지우지 않는다. QuestManager·QuestHud가 같은 부모 아래에 있기 때문이다.
    ///
    /// Socket_Bench만 씬 전체에서 찾는다. 이 도구의 이전 판이 소켓을 실물 작업대 위로 옮겨 놓았을
    /// 수 있어서인데, 그때도 부모는 TutorialImport 그대로였지만 손으로 옮겼을 가능성까지 감안했다.
    /// 대신 <see cref="GrabSocket"/> 보유를 조건으로 걸어 같은 이름의 다른 오브젝트를 지우지 않는다.
    /// </summary>
    static readonly PlaceholderSpec[] Placeholders =
    {
        new("Tool_Saw"),
        new("Table"),
        new("Marker_North"),
        new("Marker_East"),
        new("Marker_West"),
        new("Socket_Bench",
            searchWholeScene: true,
            requiredComponent: typeof(GrabSocket),
            extraAllowedBehaviours: new[] { typeof(GrabSocket) })
    };

    /// <summary>
    /// 플레이스홀더에 붙어 있어도 되는 MonoBehaviour. 대역 노릇에 필요한 것들이라 지워도 잃을 게
    /// 없다. 이 밖의 MonoBehaviour가 하나라도 있으면 누군가 실제 로직을 얹은 것으로 보고 삭제를
    /// 건너뛴다. Transform·Renderer·Collider·MeshFilter·Rigidbody 같은 비-MonoBehaviour는 판정에서
    /// 제외한다. 대상 하나에만 필요한 예외는 <see cref="PlaceholderSpec.ExtraAllowedBehaviours"/>로
    /// 좁게 준다.
    /// </summary>
    static readonly Type[] PlaceholderAllowedBehaviours =
    {
        typeof(ProcessTarget),
        typeof(Grabbable),
        typeof(Outline)
    };

    [MenuItem(MenuPath)]
    static void RunFromMenu()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"{Tag} 열려 있는 씬이 없습니다.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "튜토리얼 실물 전환·작업장 정규화",
                $"'{scene.name}' 씬에서 다음을 수행합니다.\n\n" +
                "1. 실물 톱(SawTool)에 ProcessTarget(tool_saw) 부여\n" +
                "2. 플레이스홀더 6개 삭제 (Tool_Saw·Table·Marker_*·Socket_Bench)\n" +
                "3. WorkshopImport 루트 오프셋 정규화 (자식 월드 포즈 보존)\n\n" +
                "저장은 하지 않습니다. 결과를 확인한 뒤 직접 저장하십시오.",
                "실행", "취소"))
            return;

        Run(scene);
    }

    static void Run(Scene scene)
    {
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("튜토리얼 실물 전환·작업장 정규화");

        try
        {
            MigrateSawTarget(scene);
            DeletePlaceholders(scene);
            NormalizeWorkshopRoot(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{Tag} 완료했습니다. 씬은 저장하지 않았습니다 — 확인 후 Ctrl+S 하십시오.");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    // ── 1. 실물 톱 타깃 이전 ────────────────────────────────────────────────

    /// <summary>
    /// 씬의 <see cref="SawTool"/>에 <c>tool_saw</c> 키의 <see cref="ProcessTarget"/>을 붙인다.
    /// ProcessTarget은 정적 레지스트리에 OnEnable로 등록하므로, 에디터에서 컴포넌트만 얹고 씬을
    /// 저장하면 런타임 조회가 그대로 성립한다. 별도의 씬 참조 배선은 필요 없다.
    /// </summary>
    static void MigrateSawTarget(Scene scene)
    {
        var tools = Object.FindObjectsByType<SawTool>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t != null && t.gameObject.scene == scene)
            .ToList();

        if (tools.Count == 0)
        {
            Debug.LogError($"{Tag} 1. 씬에서 SawTool을 찾지 못해 타깃 이전을 건너뜁니다.");
            return;
        }

        if (tools.Count > 1)
        {
            Debug.LogError(
                $"{Tag} 1. SawTool이 {tools.Count}개입니다({string.Join(", ", tools.Select(t => PathOf(t.transform)))}). " +
                "어느 쪽이 실물인지 정할 수 없어 타깃 이전을 건너뜁니다.");
            return;
        }

        var saw = tools[0].gameObject;
        var target = saw.GetComponent<ProcessTarget>();

        if (target == null)
        {
            target = Undo.AddComponent<ProcessTarget>(saw);
            SetTargetKey(target, SawTargetKey);
            Debug.Log($"{Tag} 1. 타깃 이전: '{PathOf(saw.transform)}'에 ProcessTarget(key={SawTargetKey})을 추가했습니다.");
        }
        else if (string.IsNullOrWhiteSpace(target.Key))
        {
            SetTargetKey(target, SawTargetKey);
            Debug.Log($"{Tag} 1. 타깃 이전: '{PathOf(saw.transform)}'의 빈 ProcessTarget 키를 '{SawTargetKey}'로 채웠습니다.");
        }
        else if (target.Key != SawTargetKey)
        {
            Debug.LogWarning(
                $"{Tag} 1. '{PathOf(saw.transform)}'에 이미 ProcessTarget(key={target.Key})이 있습니다. " +
                $"'{SawTargetKey}'로 덮어쓰지 않았습니다. 키를 직접 확인하십시오.");
        }
        else
        {
            Debug.Log($"{Tag} 1. 타깃 이전: '{PathOf(saw.transform)}'에 ProcessTarget(key={SawTargetKey})이 이미 있습니다.");
        }

        // point·grab 목표는 ProcessTarget과 같은 오브젝트의 Grabbable을 본다(ProcessTarget.Awake).
        if (saw.GetComponent<Grabbable>() == null)
            Debug.LogError(
                $"{Tag} 1. '{PathOf(saw.transform)}'에 Grabbable이 없습니다. " +
                "point·grab 목표가 대상을 찾지 못합니다.");
    }

    static void SetTargetKey(ProcessTarget target, string key)
    {
        using var serialized = new SerializedObject(target);
        var property = serialized.FindProperty("key");
        if (property == null)
        {
            Debug.LogError($"{Tag} ProcessTarget.key 필드를 찾지 못했습니다. 필드 이름이 바뀌었다면 이 도구도 함께 고쳐야 합니다.");
            return;
        }

        property.stringValue = key;
        serialized.ApplyModifiedProperties();
    }

    // ── 2. 플레이스홀더 삭제 ────────────────────────────────────────────────

    static void DeletePlaceholders(Scene scene)
    {
        var root = FindRoot(scene, TutorialRootName);
        if (root == null)
            Debug.LogWarning($"{Tag} 2. '{TutorialRootName}' 루트를 찾지 못했습니다. 씬 전체에서 찾는 대상만 처리합니다.");

        var deleted = new List<string>();
        var skipped = new List<string>();

        foreach (var spec in Placeholders)
        {
            var found = Find(scene, root, spec);
            if (found.Count == 0)
            {
                skipped.Add($"{spec.Name}(없음)");
                continue;
            }

            foreach (var go in found)
            {
                var blockers = LogicBehaviours(go, spec);
                if (blockers.Count > 0)
                {
                    skipped.Add($"{spec.Name}(로직 컴포넌트: {string.Join(", ", blockers)})");
                    Debug.LogWarning(
                        $"{Tag} 2. '{PathOf(go.transform)}'에 예상 밖의 컴포넌트가 있어 삭제하지 않았습니다: " +
                        string.Join(", ", blockers), go);
                    continue;
                }

                deleted.Add(spec.SearchWholeScene ? PathOf(go.transform) : spec.Name);
                Undo.DestroyObjectImmediate(go);
            }
        }

        Debug.Log($"{Tag} 2. 삭제 목록: {(deleted.Count > 0 ? string.Join(", ", deleted) : "없음")}" +
                  (skipped.Count > 0 ? $" / 건너뜀: {string.Join(", ", skipped)}" : string.Empty));
    }

    /// <summary>
    /// 지정에 맞는 오브젝트를 찾는다. 씬 전체 탐색은 이름만으로는 위험해서
    /// <see cref="PlaceholderSpec.RequiredComponent"/>를 반드시 확인한다.
    /// </summary>
    static List<GameObject> Find(Scene scene, GameObject tutorialRoot, PlaceholderSpec spec)
    {
        IEnumerable<Transform> pool;

        if (spec.SearchWholeScene)
            pool = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true));
        else if (tutorialRoot != null)
            pool = tutorialRoot.transform.Cast<Transform>();
        else
            return new List<GameObject>();

        return pool
            .Where(t => t != null && t.name == spec.Name)
            .Where(t => spec.RequiredComponent == null || t.GetComponent(spec.RequiredComponent) != null)
            .Select(t => t.gameObject)
            .ToList();
    }

    /// <summary>대역용으로 허용된 것 밖의 MonoBehaviour 목록. 자식까지 훑는다.</summary>
    static List<string> LogicBehaviours(GameObject go, PlaceholderSpec spec)
    {
        var found = new List<string>();

        foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
            {
                found.Add("Missing Script");
                continue;
            }

            var type = behaviour.GetType();
            if (PlaceholderAllowedBehaviours.Any(allowed => allowed.IsAssignableFrom(type))) continue;
            if (spec.ExtraAllowedBehaviours.Any(allowed => allowed.IsAssignableFrom(type))) continue;

            found.Add(type.Name);
        }

        return found;
    }

    // ── 3. WorkshopImport 오프셋 정규화 ─────────────────────────────────────

    /// <summary>
    /// 루트를 <see cref="WorkshopNormalizedPosition"/>으로 옮기되 직계 자식의 월드 포즈를 그대로
    /// 둔다. 자식의 로컬 좌표가 그만큼 보정되며, 프리팹 인스턴스는 트랜스폼 오버라이드로 기록된다.
    /// 루트 스케일은 건드리지 않으므로 자식의 lossyScale은 변하지 않는다.
    /// </summary>
    static void NormalizeWorkshopRoot(Scene scene)
    {
        var root = FindRoot(scene, WorkshopRootName);
        if (root == null)
        {
            Debug.LogError($"{Tag} 3. '{WorkshopRootName}' 루트를 찾지 못해 정규화를 건너뜁니다.");
            return;
        }

        var before = root.transform.position;
        var children = root.transform.Cast<Transform>().ToList();
        var cached = children.Select(c => (Transform: c, Pose: new Pose(c.position, c.rotation))).ToList();

        Undo.RegisterCompleteObjectUndo(root.transform, "작업장 오프셋 정규화");
        foreach (var (child, _) in cached)
            Undo.RegisterCompleteObjectUndo(child, "작업장 오프셋 정규화");

        root.transform.position = WorkshopNormalizedPosition;
        MarkPrefabTransform(root.transform);

        foreach (var (child, pose) in cached)
        {
            child.SetPositionAndRotation(pose.position, pose.rotation);
            MarkPrefabTransform(child);
        }

        var maxPositionDelta = 0f;
        var maxAngleDelta = 0f;
        var offenders = new List<string>();

        foreach (var (child, pose) in cached)
        {
            var positionDelta = Vector3.Distance(child.position, pose.position);
            var angleDelta = Quaternion.Angle(child.rotation, pose.rotation);

            maxPositionDelta = Mathf.Max(maxPositionDelta, positionDelta);
            maxAngleDelta = Mathf.Max(maxAngleDelta, angleDelta);

            if (positionDelta > PoseTolerance || angleDelta > AngleTolerance)
                offenders.Add($"{child.name}(Δpos={positionDelta:F5}, Δrot={angleDelta:F5})");
        }

        Debug.Log(
            $"{Tag} 3. 정규화: '{root.name}' {Fmt(before)} → {Fmt(root.transform.position)}. " +
            $"직계 자식 {cached.Count}개 월드 포즈 보존 검증 최대 오차 Δpos={maxPositionDelta:F5} m, " +
            $"Δrot={maxAngleDelta:F5}° (허용 {PoseTolerance} m / {AngleTolerance}°).");

        if (offenders.Count > 0)
            Debug.LogError($"{Tag} 3. 허용 오차를 넘은 자식 {offenders.Count}개: {string.Join(", ", offenders)}");
    }

    // ── 공용 ────────────────────────────────────────────────────────────────

    static GameObject FindRoot(Scene scene, string name) =>
        scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);

    /// <summary>프리팹 인스턴스의 트랜스폼 변경을 오버라이드로 확정한다. 일반 오브젝트에는 무해하다.</summary>
    static void MarkPrefabTransform(Transform transform)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(transform)) return;
        PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
    }

    static string PathOf(Transform transform)
    {
        var path = transform.name;
        for (var parent = transform.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;

        return path;
    }

    static string Fmt(Vector3 value) => $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
}
