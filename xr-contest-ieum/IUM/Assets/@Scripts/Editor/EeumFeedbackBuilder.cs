using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Play 씬의 이음이(<see cref="EeumBehaviour"/> 보유 오브젝트)에 <see cref="EeumStateFeedback"/>을
/// 부착한다. 씬 수정은 컴포넌트 1개 추가뿐이라 다른 작업자의 변경과 겹칠 여지를 최소화하고,
/// 재실행 시 이미 붙어 있으면 아무것도 하지 않는다(멱등).
///
/// 대상 탐색은 이름('iumi')이 아니라 <see cref="EeumBehaviour"/> 컴포넌트로 한다 —
/// <see cref="PlayNpcBuilder"/>가 이식한 NPC 루트 아래 어디에 있어도, 오브젝트 이름이 바뀌어도 걸린다.
/// </summary>
static class EeumFeedbackBuilder
{
    const string PlayScenePath = "Assets/@Scenes/Play.unity";

    [MenuItem("Tools/PROJECT 이음/이음이 상태 피드백 부착")]
    static void AttachFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "이음이 상태 피드백",
                "Play 씬의 이음이에 EeumStateFeedback 컴포넌트를 부착합니다.\n" +
                "이미 붙어 있으면 아무것도 변경하지 않습니다.",
                "부착", "취소"))
            return;

        Build();
    }

    /// <summary>배치모드 진입점: -executeMethod EeumFeedbackBuilder.AttachFromBatchMode</summary>
    public static void AttachFromBatchMode() => Build();

    static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayScenePath) == null)
        {
            Debug.LogError("[EeumFeedback] Play 씬을 찾지 못했습니다: " + PlayScenePath);
            return;
        }

        // 열려 있는 씬의 미저장 변경을 지키는 쪽은 에디터 사용자 몫이다. 배치모드에는 물을 곳이 없다.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 이미 열려 있던 Play 씬은 작업 후에도 열어 둔다 (PlayNpcBuilder와 같은 사유).
        var playWasLoaded = SceneManager.GetSceneByPath(PlayScenePath).isLoaded;

        Scene play = default;
        try
        {
            play = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Additive);

            var behaviour = FindEeum(play);
            if (behaviour == null)
            {
                Debug.LogWarning("[EeumFeedback] Play 씬에서 EeumBehaviour(이음이)를 찾지 못했습니다. " +
                                 "PlayNpcBuilder로 NPC를 먼저 가져왔는지 확인해 주세요.");
                return;
            }

            if (behaviour.GetComponent<EeumStateFeedback>() != null)
            {
                Debug.Log($"[EeumFeedback] '{behaviour.gameObject.name}'에 EeumStateFeedback이 이미 " +
                          "붙어 있어 변경 없이 종료합니다.", behaviour);
                return;
            }

            behaviour.gameObject.AddComponent<EeumStateFeedback>();
            EditorSceneManager.MarkSceneDirty(play);
            EditorSceneManager.SaveScene(play, PlayScenePath);

            Debug.Log($"[EeumFeedback] '{behaviour.gameObject.name}'에 EeumStateFeedback을 부착하고 " +
                      "Play 씬을 저장했습니다.", behaviour);
        }
        finally
        {
            if (!playWasLoaded && play.IsValid() && play.isLoaded) EditorSceneManager.CloseScene(play, true);
            AssetDatabase.SaveAssets();
        }
    }

    static EeumBehaviour FindEeum(Scene play)
    {
        foreach (var root in play.GetRootGameObjects())
        {
            var behaviour = root.GetComponentInChildren<EeumBehaviour>(true);
            if (behaviour != null) return behaviour;
        }

        return null;
    }
}
