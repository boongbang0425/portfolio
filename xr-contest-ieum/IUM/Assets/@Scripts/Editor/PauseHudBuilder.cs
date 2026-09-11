using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assembles the world-space 일시정지 HUD prefab from the two mesh prefabs and wires every mapping.
///
/// Done in an editor tool rather than by hand because the FBX names every plate 개체_N: the mapping
/// below is the only record of which mesh is which button, and it was read off an orthographic render
/// of the geometry. Re-running the tool rebuilds the prefab from scratch, so the mapping never has to
/// be reconstructed by eye in the inspector.
///
/// 생성물은 AI 자동 생성 자산 규칙에 따라 Assets/@Developers/RYU 아래에 만든다. 원본 두 프리팹은
/// 인스턴스로 품고 있을 뿐 수정하지 않는다.
/// </summary>
static class PauseHudBuilder
{
    const string PausePrefabPath = "Assets/@Prefabs/Pause.prefab";
    const string OptionPrefabPath = "Assets/@Prefabs/Option.prefab";
    const string OutputDirectory = "Assets/@Developers/RYU/UI";
    const string OutputPath = OutputDirectory + "/PauseHud.prefab";

    // 일시정지 패널. 개체_51은 제목판, 개체_56은 글자 없는 장식판, 개체_57은 테두리라 제외한다.
    const string ResumePlate = "개체_55";          // 계속하기
    const string OptionsPlate = "개체_53";         // 옵션
    const string RestartPlate = "개체_52";         // 공정 다시 시작
    const string MainMenuPlate = "개체_54";        // 메인 화면으로

    // 옵션 패널. 개체_42는 제목판이다.
    const string BackPlate = "개체_14";            // 뒤로가기

    // 행마다 트랙과 손잡이가 한 쌍이다.
    const string MusicTrack = "개체_5";
    const string MusicHandle = "개체_50";
    const string DialogueTrack = "개체_4";
    const string DialogueHandle = "개체_48";
    const string EnvironmentTrack = "개체_3";
    const string EnvironmentHandle = "개체_49";
    const string VideoTrack = "개체_1";
    const string VideoHandle = "개체_51";

    [MenuItem("Tools/PROJECT 이음/일시정지 HUD 프리팹 생성")]
    static void Build()
    {
        var pauseSource = AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
        var optionSource = AssetDatabase.LoadAssetAtPath<GameObject>(OptionPrefabPath);

        if (pauseSource == null || optionSource == null)
        {
            Debug.LogError($"[PauseHud] 원본 프리팹을 찾지 못했습니다: {PausePrefabPath}, {OptionPrefabPath}");
            return;
        }

        var root = new GameObject("PauseHud");

        try
        {
            var pausePanel = Instantiate(pauseSource, root.transform, "Pause");
            var optionPanel = Instantiate(optionSource, root.transform, "Option");

            // 원본 프리팹은 시작 씬 배치 좌표를 그대로 들고 있다. HUD는 런타임에 배치하므로 원점으로 맞춘다.
            Reset(pausePanel.transform);
            Reset(optionPanel.transform);

            var menu = root.AddComponent<PauseWorldMenu>();
            var serialized = new SerializedObject(menu);

            serialized.FindProperty("pausePanel").objectReferenceValue = pausePanel.transform;
            serialized.FindProperty("optionPanel").objectReferenceValue = optionPanel.transform;

            serialized.FindProperty("resumeButton").objectReferenceValue =
                AddButton(pausePanel, ResumePlate);
            serialized.FindProperty("optionsButton").objectReferenceValue =
                AddButton(pausePanel, OptionsPlate);
            serialized.FindProperty("restartProcessButton").objectReferenceValue =
                AddButton(pausePanel, RestartPlate);
            serialized.FindProperty("mainMenuButton").objectReferenceValue =
                AddButton(pausePanel, MainMenuPlate);
            serialized.FindProperty("optionBackButton").objectReferenceValue =
                AddButton(optionPanel, BackPlate);

            serialized.FindProperty("musicSlider").objectReferenceValue =
                AddSlider(optionPanel, MusicTrack, MusicHandle);
            serialized.FindProperty("dialogueSlider").objectReferenceValue =
                AddSlider(optionPanel, DialogueTrack, DialogueHandle);
            serialized.FindProperty("environmentSlider").objectReferenceValue =
                AddSlider(optionPanel, EnvironmentTrack, EnvironmentHandle);
            serialized.FindProperty("videoSlider").objectReferenceValue =
                AddSlider(optionPanel, VideoTrack, VideoHandle);

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
                Debug.LogError($"[PauseHud] 프리팹 저장에 실패했습니다: {OutputPath}");
                return;
            }

            Debug.Log($"[PauseHud] 생성 완료: {OutputPath}", saved);
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static GameObject Instantiate(GameObject source, Transform parent, string name)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        instance.name = name;
        return instance;
    }

    static void Reset(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
    }

    static WorldMenuButton AddButton(GameObject panel, string plateName)
    {
        var plate = Find(panel, plateName);
        if (plate == null) return null;

        var button = plate.GetComponent<WorldMenuButton>();
        if (button == null) button = plate.gameObject.AddComponent<WorldMenuButton>();

        WorldMenuGeometry.FitBoxCollider(plate, Vector3.zero);
        return button;
    }

    static WorldMenuSlider AddSlider(GameObject panel, string trackName, string handleName)
    {
        var track = Find(panel, trackName);
        var handle = Find(panel, handleName);
        if (track == null || handle == null) return null;

        var slider = track.GetComponent<WorldMenuSlider>();
        if (slider == null) slider = track.gameObject.AddComponent<WorldMenuSlider>();

        var serialized = new SerializedObject(slider);
        serialized.FindProperty("handle").objectReferenceValue = handle;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return slider;
    }

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

    /// <summary>
    /// Both panels contain objects with the same names — 개체_51 is the 일시정지 title plate in one and
    /// the 영상 slider handle in the other — so lookups are always scoped to a single panel.
    /// </summary>
    static Transform Find(GameObject panel, string name)
    {
        foreach (var candidate in panel.GetComponentsInChildren<Transform>(true))
            if (candidate.name == name) return candidate;

        Debug.LogError($"[PauseHud] '{panel.name}' 안에서 '{name}'을 찾지 못했습니다.");
        return null;
    }
}
