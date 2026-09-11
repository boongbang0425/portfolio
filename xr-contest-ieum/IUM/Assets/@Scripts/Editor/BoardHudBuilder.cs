using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BoardRoot 기반 일시정지 HUD를 조립한다. 두 단계다:
///
/// 1) BoardRoot.prefab 인스턴스 + 포인터 두 개 + <see cref="BoardPauseMenu"/>를 묶은
///    BoardHud.prefab을 만든다. 원본 BoardRoot.prefab은 인스턴스로 품을 뿐 수정하지 않는다.
/// 2) Play 씬에서 기존 PauseHud 인스턴스를 제거하고 같은 자리에 BoardHud 인스턴스를 배치한다.
///
/// 손으로 조립하지 않는 이유는 PauseHudBuilder와 같다 — 버튼·슬라이더 아홉 개의 매핑이
/// 인스펙터에만 남으면 프리팹을 다시 만들 때마다 눈으로 복원해야 한다. 여기가 그 배선의 유일한
/// 기록이다. 재실행하면 기존 BoardHud를 지우고 다시 만들므로 멱등이다.
///
/// 생성물은 AI 자동 생성 자산 규칙에 따라 Assets/@Developers/RYU 아래에 만든다.
/// </summary>
static class BoardHudBuilder
{
    const string BoardRootPrefabPath = "Assets/@Prefabs/BoardRoot.prefab";
    const string OutputDirectory = "Assets/@Developers/RYU/UI";
    const string OutputPath = OutputDirectory + "/BoardHud.prefab";
    const string PlayScenePath = "Assets/@Scenes/Play.unity";

    const string HudName = "BoardHud";
    const string LegacyHudName = "PauseHud";

    // BoardRoot 내부 이름. 판 전환은 BoardSwitcher가 담당하고 여기 이름으로 버튼·슬라이더만 찾는다.
    const string ContinueButton = "Btn_Continue";
    const string OptionsButton = "Btn_Options";
    const string RestartButton = "Btn_Restart";
    const string MainMenuButton = "Btn_Main";
    const string BackButton = "Btn_Back";
    const string MusicSlider = "Slider_Music";
    const string VoiceSlider = "Slider_Voice";       // 저장 채널 Dialogue
    const string AmbientSlider = "Slider_Ambient";   // 저장 채널 Environment
    const string VideoSlider = "Slider_Video";

    [MenuItem("Tools/PROJECT 이음/보드 일시정지 HUD 생성·배치")]
    static void BuildFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "보드 일시정지 HUD",
                "BoardHud.prefab을 다시 만들고 Play 씬의 PauseHud를 BoardHud로 교체합니다.\n" +
                "기존 BoardHud가 있으면 지우고 다시 배치합니다.",
                "생성·배치", "취소"))
            return;

        Build();
    }

    /// <summary>배치모드 진입점: -executeMethod BoardHudBuilder.BuildFromBatchMode</summary>
    public static void BuildFromBatchMode() => Build();

    static void Build()
    {
        // 열려 있는 씬의 미저장 변경을 지키는 쪽은 에디터 사용자 몫이다. 배치모드에는 물을 곳이 없다.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var prefab = BuildPrefab();
        if (prefab == null) return;

        PlaceInPlayScene(prefab);
    }

    // ---- 프리팹 ----

    static GameObject BuildPrefab()
    {
        var boardSource = AssetDatabase.LoadAssetAtPath<GameObject>(BoardRootPrefabPath);
        if (boardSource == null)
        {
            Debug.LogError($"[BoardHud] 원본 프리팹을 찾지 못했습니다: {BoardRootPrefabPath}");
            return null;
        }

        var root = new GameObject(HudName);

        try
        {
            var board = (GameObject)PrefabUtility.InstantiatePrefab(boardSource, root.transform);

            // 원본 프리팹은 제작 씬의 배치 좌표(y 1.3, z 0.6)를 그대로 들고 있다. HUD는 런타임에
            // 플레이어 앞으로 배치하므로 위치·회전만 원점으로 맞춘다. 스케일 4.3은 보드 고유
            // 크기의 일부라 유지한다 — 최종 크기는 BoardPauseMenu.autoFitScale이 맞춘다.
            board.transform.localPosition = Vector3.zero;
            board.transform.localRotation = Quaternion.identity;

            var menu = root.AddComponent<BoardPauseMenu>();
            var serialized = new SerializedObject(menu);

            serialized.FindProperty("switcher").objectReferenceValue =
                board.GetComponent<BoardSwitcher>();
            serialized.FindProperty("boardRoot").objectReferenceValue = board.transform;

            serialized.FindProperty("continueButton").objectReferenceValue = FindButton(board, ContinueButton);
            serialized.FindProperty("optionsButton").objectReferenceValue = FindButton(board, OptionsButton);
            serialized.FindProperty("restartButton").objectReferenceValue = FindButton(board, RestartButton);
            serialized.FindProperty("mainMenuButton").objectReferenceValue = FindButton(board, MainMenuButton);
            serialized.FindProperty("backButton").objectReferenceValue = FindButton(board, BackButton);

            serialized.FindProperty("musicSlider").objectReferenceValue = FindSlider(board, MusicSlider);
            serialized.FindProperty("voiceSlider").objectReferenceValue = FindSlider(board, VoiceSlider);
            serialized.FindProperty("ambientSlider").objectReferenceValue = FindSlider(board, AmbientSlider);
            serialized.FindProperty("videoSlider").objectReferenceValue = FindSlider(board, VideoSlider);

            var pointers = serialized.FindProperty("pointers");
            pointers.arraySize = 2;
            pointers.GetArrayElementAtIndex(0).objectReferenceValue =
                AddPointer(root.transform, XRHandSide.Left);
            pointers.GetArrayElementAtIndex(1).objectReferenceValue =
                AddPointer(root.transform, XRHandSide.Right);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(OutputDirectory);
            AssetDatabase.Refresh();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, OutputPath, out var success);
            if (!success || saved == null)
            {
                Debug.LogError($"[BoardHud] 프리팹 저장에 실패했습니다: {OutputPath}");
                return null;
            }

            Debug.Log($"[BoardHud] 프리팹 생성 완료: {OutputPath}", saved);
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static Board3DButton FindButton(GameObject board, string name)
    {
        var target = Find(board, name);
        var button = target != null ? target.GetComponent<Board3DButton>() : null;
        if (button == null)
            Debug.LogError($"[BoardHud] '{name}'에서 Board3DButton을 찾지 못했습니다.");
        return button;
    }

    static Board3DSlider FindSlider(GameObject board, string name)
    {
        var target = Find(board, name);
        var slider = target != null ? target.GetComponent<Board3DSlider>() : null;
        if (slider == null)
            Debug.LogError($"[BoardHud] '{name}'에서 Board3DSlider를 찾지 못했습니다.");
        return slider;
    }

    static Transform Find(GameObject board, string name)
    {
        foreach (var candidate in board.GetComponentsInChildren<Transform>(true))
            if (candidate.name == name) return candidate;

        Debug.LogError($"[BoardHud] '{board.name}' 안에서 '{name}'을 찾지 못했습니다.");
        return null;
    }

    /// <summary>PauseHudBuilder.AddPointer와 같은 구성. 시작은 꺼진 채로 두고 메뉴가 켠다.</summary>
    static WorldMenuPointer AddPointer(Transform parent, XRHandSide hand)
    {
        var label = hand == XRHandSide.Left ? "Left" : "Right";
        var holder = new GameObject($"Pointer_{label}");
        holder.transform.SetParent(parent, false);

        var pointer = holder.AddComponent<WorldMenuPointer>();
        var serialized = new SerializedObject(pointer);
        serialized.FindProperty("hand").enumValueIndex = (int)hand;
        serialized.FindProperty("menuRoot").objectReferenceValue = parent;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        holder.SetActive(false);
        return pointer;
    }

    // ---- 씬 배치 ----

    static void PlaceInPlayScene(GameObject prefab)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayScenePath) == null)
        {
            Debug.LogError($"[BoardHud] Play 씬을 찾지 못했습니다: {PlayScenePath}");
            return;
        }

        // 이미 열려 있던 Play 씬은 작업 후에도 열어 둔다 (PlayNpcBuilder와 같은 사유).
        var wasLoaded = SceneManager.GetSceneByPath(PlayScenePath).isLoaded;

        Scene play = default;
        try
        {
            play = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Additive);

            // 자리는 기존 인스턴스에서 물려받는다. 우선순위: 재실행이면 기존 BoardHud,
            // 첫 실행이면 제거 대상인 PauseHud. 런타임에 Place()가 다시 배치하므로 씬 위치는
            // 에디터에서 찾기 좋게 두는 용도다.
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            var anchored = false;

            var existing = FindRoot(play, HudName);
            if (existing != null)
            {
                position = existing.transform.position;
                rotation = existing.transform.rotation;
                anchored = true;
                Object.DestroyImmediate(existing);
            }

            var legacy = FindRoot(play, LegacyHudName);
            if (legacy != null)
            {
                if (!anchored)
                {
                    position = legacy.transform.position;
                    rotation = legacy.transform.rotation;
                    anchored = true;
                }

                Object.DestroyImmediate(legacy);
            }

            if (!anchored)
                Debug.LogWarning(
                    $"[BoardHud] Play 씬에서 '{LegacyHudName}'를 찾지 못해 BoardHud를 원점에 둡니다. " +
                    "런타임 배치는 BoardPauseMenu.Place()가 수행하므로 동작에는 지장이 없습니다.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, play);
            instance.name = HudName;
            instance.transform.SetPositionAndRotation(position, rotation);

            EditorSceneManager.MarkSceneDirty(play);
            EditorSceneManager.SaveScene(play, PlayScenePath);

            Debug.Log($"[BoardHud] Play 씬 배치 완료: '{LegacyHudName}' 제거, '{HudName}' 배치({position}).");
        }
        finally
        {
            if (!wasLoaded && play.IsValid() && play.isLoaded)
                EditorSceneManager.CloseScene(play, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == name) return root;

        return null;
    }
}
