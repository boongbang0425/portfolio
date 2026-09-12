using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// 플레이 씬이 필요로 하는 시스템을 프리팹 두 개로 만든다.
///
/// 손으로 조립하지 않고 도구로 만드는 이유는 <see cref="PauseHudBuilder"/>와 같다. UIDocument 세 쌍의
/// uxml·PanelSettings 연결과 <see cref="PauseController"/>·<see cref="AiConversationHud"/>의 설정 값이
/// 인스펙터에만 남으면, 프리팹을 다시 만들 때마다 그 값을 눈으로 복원해야 한다. 여기가 그 배선의
/// 유일한 기록이다.
///
/// 경계는 수명이다. CoreSystems는 <see cref="Singleton{T}"/> 계열과 화면 UI라서 어느 씬에나 놓을 수
/// 있고, PlayLoop는 제작 공정을 도는 Play 씬 전용이다.
///
/// 싱글턴을 자식으로 두어도 된다. <see cref="Singleton{T}.Awake"/>가 스스로 부모를 떼고
/// DontDestroyOnLoad를 걸기 때문에(Singleton.cs:65) 실행 즉시 루트로 빠져나간다.
///
/// 생성물은 AI 자동 생성 자산 규칙에 따라 Assets/Developers/RYU 아래에 만든다.
/// </summary>
static class PlaySystemsBuilder
{
    const string OutputDirectory = "Assets/Developers/RYU/Prefabs";
    const string CoreSystemsPath = OutputDirectory + "/CoreSystems.prefab";
    const string PlayLoopPath = OutputDirectory + "/PlayLoop.prefab";

    const string SubtitleUxmlPath = "Assets/UI/Dialogue/Subtitle.uxml";
    const string SubtitlePanelPath = "Assets/UI/Dialogue/SubtitlePanelSettings.asset";
    const string CutsceneUxmlPath = "Assets/UI/Cutscene/Cutscene.uxml";
    const string CutscenePanelPath = "Assets/UI/Cutscene/CutscenePanelSettings.asset";
    const string PauseUxmlPath = "Assets/UI/Pause/PauseMenu.uxml";
    const string PausePanelPath = "Assets/UI/Pause/PauseMenuPanelSettings.asset";
    const string IeumiUxmlPath = "Assets/UI/Ai/IeumiHud.uxml";
    const string IeumiPanelPath = "Assets/UI/Ai/IeumiHudPanelSettings.asset";

    /// <summary>메인 화면으로 돌아갈 씬. Build Settings에 등록되어 있어야 한다.</summary>
    const string MainSceneName = "StartScene";

    [MenuItem("Tools/PROJECT 이음/플레이 시스템 프리팹 생성")]
    static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CoreSystemsPath) != null &&
            !EditorUtility.DisplayDialog(
                "플레이 시스템 프리팹",
                "기존 CoreSystems.prefab과 PlayLoop.prefab을 다시 만들까요?\n" +
                "씬에 놓인 인스턴스의 오버라이드는 유지되지만, 프리팹 자산의 수동 변경은 사라집니다.",
                "다시 만들기", "취소"))
            return;

        Directory.CreateDirectory(OutputDirectory);
        AssetDatabase.Refresh();

        var core = BuildCoreSystems();
        if (core == null) return;

        var loop = BuildPlayLoop(core);
        if (loop == null) return;

        Debug.Log($"[PlaySystems] 생성 완료: {CoreSystemsPath}, {PlayLoopPath}", loop);
        Selection.activeObject = loop;
        EditorGUIUtility.PingObject(loop);
    }

    /// <summary>
    /// 어느 씬에나 놓을 수 있는 쪽. 싱글턴 일곱과 화면 UI 셋, 그리고 EventSystem이다.
    /// 씬 오브젝트를 하나도 참조하지 않으므로 프리팹 자산만으로 완결된다.
    /// </summary>
    static GameObject BuildCoreSystems()
    {
        var root = new GameObject("CoreSystems");

        try
        {
            // 세 매니저를 한 오브젝트에 둔 것은 기존 씬들의 "Core Services" 구성을 그대로 옮긴 것이다.
            var services = CreateChild(root.transform, "Core Services");
            services.AddComponent<SceneController>();
            services.AddComponent<AudioManager>();
            services.AddComponent<DataManager>();

            CreateChild(root.transform, "GameFlow").AddComponent<GameFlow>();
            CreateChild(root.transform, "CutsceneDirector").AddComponent<CutsceneDirector>();
            CreateChild(root.transform, "InGameDialogue").AddComponent<InGameDialogue>();

            var pause = CreateChild(root.transform, "PauseController").AddComponent<PauseController>();
            var pauseData = new SerializedObject(pause);
            pauseData.FindProperty("mainSceneName").stringValue = MainSceneName;
            pauseData.ApplyModifiedPropertiesWithoutUndo();

            // PanelSettings의 sortingOrder가 자막 0 · 컷씬 6000 · 일시정지 7000으로 이미 잡혀 있어
            // 여기서 순서를 다시 정하지 않는다.
            if (CreateView<SubtitleView>(root.transform, "SubtitleView", SubtitleUxmlPath, SubtitlePanelPath) == null ||
                CreateView<CutsceneView>(root.transform, "CutsceneView", CutsceneUxmlPath, CutscenePanelPath) == null ||
                CreateView<PauseMenuView>(root.transform, "PauseMenuView", PauseUxmlPath, PausePanelPath) == null)
                return null;

            // UI Toolkit 버튼은 EventSystem 없이 클릭을 받지 못한다. 씬과 함께 사라지는 것이 맞으므로
            // 싱글턴이 아닌 이 계층에 남는다.
            var events = CreateChild(root.transform, "EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, CoreSystemsPath, out var success);
            if (success && saved != null) return saved;

            Debug.LogError($"[PlaySystems] 프리팹 저장에 실패했습니다: {CoreSystemsPath}");
            return null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Play 씬 전용. CoreSystems를 중첩으로 품으므로 씬에는 이것 하나만 놓으면 된다.
    /// </summary>
    static GameObject BuildPlayLoop(GameObject core)
    {
        var root = new GameObject("PlayLoop");

        try
        {
            var nested = (GameObject)PrefabUtility.InstantiatePrefab(core, root.transform);
            nested.name = "CoreSystems";

            // 제작 공정 러너는 여기서 만들지 않는다. 실행기가 QuestManager 하나로 통일되면서
            // 공정 정의는 quest.json으로 옮겨 갔고, 그 QuestManager는 TutorialImportBuilder가
            // TutorialScene에서 Play 씬으로 이식한다. 위치 추적기만 프리팹에 남는다 — 씬에서
            // Player를 스스로 찾으므로(PlayerPoseTracker.cs:35) 참조를 비워 둬도 된다.
            CreateChild(root.transform, "PlayerPoseTracker").AddComponent<PlayerPoseTracker>();

            // 브릿지의 씬 참조 네 종은 Awake에서 스스로 찾는다. 목공 오브젝트를 씬에 넣은 뒤에도
            // 프리팹을 다시 배선할 필요가 없다.
            CreateChild(root.transform, "MainPlayProcessBridge").AddComponent<MainPlayProcessBridge>();

            // 이음이 대화 스택 (F-011~F-013). 매니저는 싱글턴이라 첫 로드에서 루트로 빠져나가
            // 유지되고, 씬 재로드가 만드는 중복은 Singleton.Awake가 정리한다. 마이크와 음성
            // 재생기는 매니저가 런타임에 스스로 만들므로 프리팹에는 컴포넌트 하나면 된다.
            // PTT 입력은 Player가 항상 붙이는 VoiceInputModule로 이미 들어온다 (Player.cs:70).
            CreateChild(root.transform, "AiConversationManager").AddComponent<AiConversationManager>();

            var ieumiHud = CreateView<AiConversationHud>(root.transform, "IeumiHud", IeumiUxmlPath, IeumiPanelPath);
            if (ieumiHud == null) return null;

            // 개발 씬(AiVoiceTest)은 서비스 상태 판을 켜 두지만 본편에서는 끈다.
            var hudData = new SerializedObject(ieumiHud.GetComponent<AiConversationHud>());
            hudData.FindProperty("showDebugPanel").boolValue = false;
            hudData.ApplyModifiedPropertiesWithoutUndo();

            // 실행 중 퀘스트의 공정·목표·재안내를 이음이 컨텍스트로 잇는 다리. 러너 참조는 스스로
            // 찾으므로 튜토리얼 이식(TutorialImport) 전에도 프리팹이 완결된다.
            CreateChild(root.transform, "ProcessContextBridge").AddComponent<ProcessContextBridge>();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PlayLoopPath, out var success);
            if (success && saved != null) return saved;

            Debug.LogError($"[PlaySystems] 프리팹 저장에 실패했습니다: {PlayLoopPath}");
            return null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/PROJECT 이음/현재 씬에 PlayLoop 배치")]
    static void PlaceInCurrentScene() => Place(PlayLoopPath, typeof(MainPlayProcessBridge));

    [MenuItem("Tools/PROJECT 이음/현재 씬에 CoreSystems 배치")]
    static void PlaceCoreInCurrentScene() => Place(CoreSystemsPath, typeof(SubtitleView));

    /// <summary>
    /// 열려 있는 씬에 프리팹을 놓는다. <paramref name="guard"/>가 이미 있으면 아무것도 하지 않는다 —
    /// 두 번 놓으면 브릿지와 위치 추적기가 둘이 되어 신호가 두 번 오르고 저장이 겹친다.
    /// </summary>
    static void Place(string prefabPath, System.Type guard)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[PlaySystems] 열려 있는 씬이 없습니다.");
            return;
        }

        if (Object.FindAnyObjectByType(guard, FindObjectsInactive.Include) != null)
        {
            Debug.LogWarning($"[PlaySystems] 이 씬에는 이미 {guard.Name}이(가) 있어 배치를 건너뜁니다.");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[PlaySystems] 프리팹을 찾지 못했습니다: {prefabPath}. 먼저 생성 메뉴를 실행하십시오.");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, $"Place {prefab.name}");
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[PlaySystems] '{scene.name}' 씬에 {prefab.name}을 놓았습니다. 씬 저장이 필요합니다.", instance);
        Selection.activeGameObject = instance;
    }

    static GameObject CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    static GameObject CreateView<T>(Transform parent, string name, string uxmlPath, string panelPath)
        where T : Component
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);

        if (uxml == null || panel == null)
        {
            Debug.LogError($"[PlaySystems] UI 자산을 찾지 못했습니다: {uxmlPath} / {panelPath}");
            return null;
        }

        var host = CreateChild(parent, name);

        // 뷰들이 [RequireComponent(typeof(UIDocument))]라 문서를 먼저 붙이고 값을 채운다.
        var document = host.AddComponent<UIDocument>();
        document.visualTreeAsset = uxml;
        document.panelSettings = panel;

        host.AddComponent<T>();
        return host;
    }
}
