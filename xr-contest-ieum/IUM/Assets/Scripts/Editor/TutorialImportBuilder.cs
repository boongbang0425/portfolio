using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TutorialScene의 튜토리얼 구성을 Play 씬으로 이식한다. <see cref="PlayWorkshopBuilder"/>와 같은
/// 임시 복사본 패턴이라 원본 TutorialScene에는 어떤 변경도 기록하지 않는다 — 원본은 단독 실행
/// 테스트 씬으로 계속 쓴다.
///
/// 가져올 대상은 두 갈래다. 판정·실행 오브젝트는 컴포넌트(ProcessTarget·QuestManager·QuestHud·
/// TutorialOutlineGuide)로 고르고, 컴포넌트가 없는 연습용 소품(Table·Marker_*)은 이름으로 고른다.
/// 소품까지 가져오는 이유는 구성을 그대로 옮기기 위해서다 — 마커는 이동 목표의 시각 안내라
/// 빠지면 튜토리얼 동선이 사라진다.
///
/// 가져온 QuestManager에는 <c>requireSavedDefinition</c>과 <c>continueInSameScene</c>을 켠다. 앞의
/// 것은 저장 진행이 가리키는 구간만 시작하게 해 같은 씬의 다른 러너와 겹치지 않게 하고, 뒤의 것은
/// 제작 공정 다섯 개를 씬 재로드 없이 이어서 돌린다. TutorialScene 원본의 인스턴스는 기본값(둘 다
/// 끔)이라 단독 실행 동작이 보존된다.
///
/// 이식 루트에는 <see cref="TutorialFlowDirector"/>를 붙인다. 튜토리얼 동안 자유 행동을 막고 배우는
/// 조작만 목표 순서대로 여는 쪽이며, 이식본에만 필요하므로 원본 씬이 아니라 여기서 얹는다.
///
/// 배치는 스테이징이 전부다. <see cref="ImportRootName"/> 루트 하나에 원래의 상대 배치 그대로
/// 묶어 WorkshopImport와 떨어진 맵 밖 <see cref="StagingPosition"/>에 내려놓는다. 실제 위치는
/// R이 에디터에서 직접 정해 옮긴다.
/// </summary>
static class TutorialImportBuilder
{
    const string PlayScenePath = "Assets/Scenes/Play.unity";
    const string TutorialScenePath = "Assets/Developers/RYU/Scenes/Dev/TutorialScene.unity";
    const string TempScenePath = "Assets/Developers/RYU/Scenes/__TutorialImportTemp.unity";
    const string ImportRootName = "TutorialImport";

    /// <summary>
    /// warehouse 동쪽 벽 바깥, WorkshopImport(120, 13.1, -40)에서 북쪽으로 30유닛. 튜토리얼
    /// 콘텐츠는 원점 주변 ±15유닛 안이라 두 이식 묶음이 겹치지 않는다.
    /// </summary>
    static readonly Vector3 StagingPosition = new(120f, 13.1f, -10f);

    /// <summary>이 중 하나라도 트리에 있으면 튜토리얼 구성으로 본다.</summary>
    static readonly System.Type[] TutorialComponentTypes =
    {
        typeof(QuestManager),
        typeof(QuestHud),
        typeof(TutorialOutlineGuide),
        typeof(ProcessTarget)
    };

    /// <summary>컴포넌트가 없어 이름으로 고르는 연습용 소품 루트.</summary>
    static readonly HashSet<string> PropRoots = new()
    {
        "Table",
        "Marker_North",
        "Marker_West",
        "Marker_East"
    };

    /// <summary>
    /// 참조를 따라가더라도 절대 끌려오면 안 되는 루트. Play 씬에 이미 자기 몫이 있거나(플레이어,
    /// 라이트, CoreSystems 계열 싱글턴), 튜토리얼 구성이 아니다. 이쪽으로 향하는 참조는 끊기고
    /// 경고만 남는다 — QuestManager.player가 그 예인데, Awake가 씬에서 스스로 다시 찾는다.
    /// </summary>
    static readonly HashSet<string> ExcludedRoots = new()
    {
        "Player",
        "Main Camera",
        "Directional Light",
        "Ground",
        "EventSystem",
        "GameFlow",
        "CutsceneDirector",
        "PauseController",
        "Core Services",
        "InGameDialogue",
        "SubtitleView",
        "PauseMenuView",
        "CutsceneView"
    };

    [MenuItem("Tools/PROJECT 이음/Play 튜토리얼 오브젝트 가져오기")]
    static void ImportFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "Play 튜토리얼 오브젝트",
                $"TutorialScene의 튜토리얼 구성을 Play 씬의 '{ImportRootName}' 아래로 가져옵니다.\n" +
                "기존 " + ImportRootName + "이 있으면 지우고 다시 가져옵니다.",
                "가져오기", "취소"))
            return;

        Build();
    }

    /// <summary>배치모드 진입점: -executeMethod TutorialImportBuilder.ImportFromBatchMode</summary>
    public static void ImportFromBatchMode() => Build();

    static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) == null)
        {
            Debug.LogError("[TutorialImport] Play 씬 또는 TutorialScene을 찾지 못했습니다.");
            return;
        }

        // 열려 있는 씬의 미저장 변경을 지키는 쪽은 에디터 사용자 몫이다. 배치모드에는 물을 곳이 없다.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        AssetDatabase.DeleteAsset(TempScenePath);
        if (!AssetDatabase.CopyAsset(TutorialScenePath, TempScenePath))
        {
            Debug.LogError("[TutorialImport] TutorialScene 임시 복사에 실패했습니다.");
            return;
        }

        // 이미 열려 있던 Play 씬은 작업 후에도 열어 둔다 (PlayWorkshopBuilder와 같은 사유).
        var playWasLoaded = SceneManager.GetSceneByPath(PlayScenePath).isLoaded;

        Scene play = default;
        Scene source = default;
        try
        {
            play = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Additive);
            RemoveExistingImport(play);

            source = EditorSceneManager.OpenScene(TempScenePath, OpenSceneMode.Additive);
            var selected = SelectTutorialRoots(source);
            if (selected.Count == 0)
            {
                Debug.LogWarning("[TutorialImport] TutorialScene에서 튜토리얼 구성을 찾지 못했습니다.");
                return;
            }

            FollowReferences(source, selected);

            var importRoot = new GameObject(ImportRootName);
            SceneManager.MoveGameObjectToScene(importRoot, play);

            foreach (var root in selected)
                root.transform.SetParent(importRoot.transform, true);

            // 원래의 상대 배치를 유지한 채 전체를 맵 밖 스테이징 위치로 옮긴다.
            importRoot.transform.position = StagingPosition;

            EnableCoexistenceGate(importRoot);
            AttachFlowDirector(importRoot);

            EditorSceneManager.MarkSceneDirty(play);
            EditorSceneManager.SaveScene(play, PlayScenePath);

            Debug.Log($"[TutorialImport] 루트 {selected.Count}개를 '{ImportRootName}'({StagingPosition})로 가져왔습니다: " +
                      string.Join(", ", selected.ConvertAll(r => r.name)));
        }
        finally
        {
            if (source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
            if (!playWasLoaded && play.IsValid() && play.isLoaded) EditorSceneManager.CloseScene(play, true);
            AssetDatabase.DeleteAsset(TempScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    /// <summary>재실행 지원. 이전 가져오기 결과를 지워 두 벌이 겹치지 않게 한다.</summary>
    static void RemoveExistingImport(Scene play)
    {
        foreach (var root in play.GetRootGameObjects())
            if (root.name == ImportRootName)
                Object.DestroyImmediate(root);
    }

    static List<GameObject> SelectTutorialRoots(Scene source)
    {
        var selected = new List<GameObject>();
        foreach (var root in source.GetRootGameObjects())
        {
            if (ExcludedRoots.Contains(root.name)) continue;
            if (PropRoots.Contains(root.name) || HasTutorialComponent(root)) selected.Add(root);
        }

        return selected;
    }

    static bool HasTutorialComponent(GameObject root)
    {
        for (var i = 0; i < TutorialComponentTypes.Length; i++)
            if (root.GetComponentInChildren(TutorialComponentTypes[i], true) != null)
                return true;

        return false;
    }

    /// <summary>
    /// 가져온 QuestManager에 공존 게이트를 켠다. 프리팹이 아니라 씬 인스턴스이므로 이식 시점에
    /// 직렬화 값으로 굳혀야 재로드 후에도 유지된다.
    /// </summary>
    static void EnableCoexistenceGate(GameObject importRoot)
    {
        var questManager = importRoot.GetComponentInChildren<QuestManager>(true);
        if (questManager == null)
        {
            Debug.LogWarning("[TutorialImport] 가져온 트리에 QuestManager가 없어 게이트를 켜지 못했습니다.");
            return;
        }

        using var serialized = new SerializedObject(questManager);
        var gate = serialized.FindProperty("requireSavedDefinition");
        if (gate == null)
        {
            Debug.LogError("[TutorialImport] QuestManager.requireSavedDefinition 필드를 찾지 못했습니다. " +
                           "필드 이름이 바뀌었다면 이 빌더도 함께 고쳐야 합니다.");
            return;
        }

        gate.boolValue = true;

        // 제작 공정 다섯 개가 Play 씬 하나에서 이어지므로 완료할 때마다 씬을 다시 로드하지 않고
        // 같은 씬에서 다음 퀘스트를 시작해야 한다. 이 값이 꺼져 있으면 첫 공정이 끝나는 순간
        // GameFlow가 Play 씬을 다시 열어 작업하던 목재와 도구가 초기화된다.
        var continueHere = serialized.FindProperty("continueInSameScene");
        if (continueHere == null)
            Debug.LogError("[TutorialImport] QuestManager.continueInSameScene 필드를 찾지 못했습니다. " +
                           "필드 이름이 바뀌었다면 이 빌더도 함께 고쳐야 합니다.");
        else
            continueHere.boolValue = true;

        serialized.ApplyModifiedPropertiesWithoutUndo();

        RemoveDuplicateServices(questManager.gameObject);
    }

    /// <summary>
    /// 이식 루트에 <see cref="TutorialFlowDirector"/>를 얹는다. 루트에 두는 이유는 QuestManager를
    /// 자식에서 찾아 구독하기 때문이며, 재실행 시 루트를 통째로 다시 만들므로 중복될 일은 없지만
    /// 수동 배치까지 감안해 존재 여부를 확인한다.
    /// </summary>
    static void AttachFlowDirector(GameObject importRoot)
    {
        if (importRoot.GetComponentInChildren<TutorialFlowDirector>(true) != null) return;

        if (importRoot.GetComponentInChildren<QuestManager>(true) == null)
        {
            Debug.LogWarning("[TutorialImport] 가져온 트리에 QuestManager가 없어 " +
                             "TutorialFlowDirector를 붙이지 않았습니다.");
            return;
        }

        importRoot.AddComponent<TutorialFlowDirector>();
        Debug.Log($"[TutorialImport] '{ImportRootName}'에 TutorialFlowDirector를 붙였습니다.");
    }

    /// <summary>
    /// TutorialScene은 단독 실행을 위해 QuestManager 오브젝트에 PlayerPoseTracker를 함께 실었다.
    /// Play 씬에는 PlayLoop 프리팹이 이미 하나를 세우므로, 그대로 두면 두 트래커가 같은 저장
    /// 데이터를 겹쳐 쓴다. 이식본에서만 떼어낸다 — 원본 씬은 건드리지 않는다.
    /// </summary>
    static void RemoveDuplicateServices(GameObject host)
    {
        var tracker = host.GetComponent<PlayerPoseTracker>();
        if (tracker == null) return;

        Object.DestroyImmediate(tracker);
        Debug.Log("[TutorialImport] Play 씬의 PlayLoop가 이미 제공하는 PlayerPoseTracker를 이식본에서 제거했습니다.");
    }

    /// <summary>
    /// 선택된 트리의 직렬화 필드가 가리키는 다른 루트를 선택에 더한다. 참조가 참조를 부를 수
    /// 있으므로 더 이상 늘지 않을 때까지 반복한다. 제외 루트로 향하는 참조는 함께 가져올 수 없어
    /// 저장 시 끊긴다 — 어디가 끊기는지 경고로 남겨 원인 추적을 가능하게 한다.
    /// </summary>
    static void FollowReferences(Scene source, List<GameObject> selected)
    {
        var selectedSet = new HashSet<GameObject>(selected);
        var scanFrom = 0;

        while (scanFrom < selected.Count)
        {
            var scanTo = selected.Count;
            for (var i = scanFrom; i < scanTo; i++)
            foreach (var component in selected[i].GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue; // Missing script는 건너뛴다.

                using var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                    var referencedRoot = RootOf(property.objectReferenceValue, source);
                    if (referencedRoot == null || selectedSet.Contains(referencedRoot)) continue;

                    if (ExcludedRoots.Contains(referencedRoot.name))
                    {
                        Debug.LogWarning(
                            $"[TutorialImport] {component.GetType().Name}.{property.name}이 제외 루트 " +
                            $"'{referencedRoot.name}'을 참조합니다. 이 참조는 가져온 뒤 끊깁니다.", component);
                        continue;
                    }

                    selectedSet.Add(referencedRoot);
                    selected.Add(referencedRoot);
                }
            }

            scanFrom = scanTo;
        }
    }

    /// <summary>참조가 임시 씬 안의 오브젝트를 가리키면 그 루트를 돌려준다. 자산 참조는 무시한다.</summary>
    static GameObject RootOf(Object reference, Scene source)
    {
        var gameObject = reference switch
        {
            GameObject go => go,
            Component component => component.gameObject,
            _ => null
        };

        if (gameObject == null || gameObject.scene != source) return null;
        return gameObject.transform.root.gameObject;
    }
}
