using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Play 씬에 남은 구식 목공 공정 잔재를 루트 단위로 일괄 삭제한다. 대상은 세 갈래다.
/// (1) 이전 이식 결과인 <see cref="ImportRootName"/> 루트,
/// (2) Grabbable만 남기고 방치된 루트 레벨 hammer 잔재,
/// (3) 그 외 목공 컴포넌트(<see cref="PlayWorkshopBuilder.WorkshopComponentTypes"/>)를 트리에 가진 루트.
///
/// 판별 타입 목록은 <see cref="PlayWorkshopBuilder"/>의 것을 그대로 재사용해 두 도구가 어긋나지
/// 않게 한다. 단, 정상 콘텐츠도 목공 컴포넌트를 품을 수 있으므로 <see cref="PreservedRoots"/>에
/// 오른 이름은 어떤 조건에도 삭제하지 않는다.
/// </summary>
static class LegacyProcessCleanupTool
{
    const string PlayScenePath = "Assets/@Scenes/Play.unity";
    const string ImportRootName = "WorkshopImport";

    /// <summary>Grabbable만 있고 HammerTool이 없는 방치 잔재. 위치는 대략 (14.9, 21.2, -52.4)다.</summary>
    const string LegacyHammerName = "hammer";

    /// <summary>
    /// 목공 컴포넌트를 품고 있어도 절대 삭제하지 않는 정상 콘텐츠 루트. 이름 기준으로 보호한다.
    /// </summary>
    static readonly HashSet<string> PreservedRoots = new()
    {
        "TutorialImport",
        "SungnyemunImport",
        "Player",
        "CoreSystems",
        "PlayLoop",
        "PauseHud",
        "iumi",
        "legendOldman"
    };

    [MenuItem("Tools/PROJECT 이음/Play 구식 공정 일괄 삭제")]
    static void CleanupFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "Play 구식 공정 일괄 삭제",
                $"Play 씬에서 '{ImportRootName}', 방치된 '{LegacyHammerName}', 목공 컴포넌트를 가진 " +
                "루트를 삭제하고 씬을 저장합니다.\n보존 목록에 오른 루트는 건드리지 않습니다.",
                "삭제", "취소"))
            return;

        Cleanup();
    }

    /// <summary>배치모드 진입점: -executeMethod LegacyProcessCleanupTool.CleanupFromBatchMode</summary>
    public static void CleanupFromBatchMode() => Cleanup();

    static void Cleanup()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayScenePath) == null)
        {
            Debug.LogError("[LegacyCleanup] Play 씬을 찾지 못했습니다.");
            return;
        }

        // 열려 있는 씬의 미저장 변경을 지키는 쪽은 에디터 사용자 몫이다. 배치모드에는 물을 곳이 없다.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 이미 열려 있던 Play 씬은 작업 후에도 열어 둔다. 아니면 임시로 열었다가 닫는다.
        var playWasLoaded = SceneManager.GetSceneByPath(PlayScenePath).isLoaded;

        Scene play = default;
        try
        {
            play = playWasLoaded
                ? SceneManager.GetSceneByPath(PlayScenePath)
                : EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Additive);

            var targets = CollectTargets(play);
            if (targets.Count == 0)
            {
                Debug.Log("[LegacyCleanup] 삭제할 구식 공정 루트가 없습니다.");
                return;
            }

            foreach (var (root, reason) in targets)
                Debug.Log($"[LegacyCleanup] 삭제 대상: '{root.name}' — {reason}");

            foreach (var (root, _) in targets)
                Object.DestroyImmediate(root);

            EditorSceneManager.MarkSceneDirty(play);
            EditorSceneManager.SaveScene(play, PlayScenePath);

            Debug.Log($"[LegacyCleanup] 루트 {targets.Count}개를 삭제하고 Play 씬을 저장했습니다.");
        }
        finally
        {
            if (!playWasLoaded && play.IsValid() && play.isLoaded) EditorSceneManager.CloseScene(play, true);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>삭제 대상 루트를 사유와 함께 모은다. 보존 목록 검사가 모든 조건에 앞선다.</summary>
    static List<(GameObject root, string reason)> CollectTargets(Scene play)
    {
        var targets = new List<(GameObject root, string reason)>();
        foreach (var root in play.GetRootGameObjects())
        {
            if (PreservedRoots.Contains(root.name)) continue;

            if (root.name == ImportRootName)
            {
                targets.Add((root, "이전 이식 결과 " + ImportRootName));
                continue;
            }

            if (root.name == LegacyHammerName)
            {
                targets.Add((root, "방치된 hammer 잔재"));
                continue;
            }

            var foundType = FindWorkshopComponentType(root);
            if (foundType != null)
                targets.Add((root, $"목공 컴포넌트 {foundType.Name} 보유"));
        }

        return targets;
    }

    /// <summary>트리에서 처음 발견한 목공 컴포넌트 타입을 돌려준다. 없으면 null.</summary>
    static System.Type FindWorkshopComponentType(GameObject root)
    {
        foreach (var type in PlayWorkshopBuilder.WorkshopComponentTypes)
            if (root.GetComponentInChildren(type, true) != null)
                return type;

        return null;
    }
}
