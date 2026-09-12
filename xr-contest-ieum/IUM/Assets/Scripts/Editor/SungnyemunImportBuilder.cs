using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 팀원 unitypackage에서 선별 추출한 숭례문 자산을 Play 씬에 배치한다. 다른 Import 빌더들과 달리
/// 팀 씬을 복사할 필요가 없다 — 자산이 프리팹이므로 인스턴스만 만들면 된다.
///
/// 원본 씬에서 프리팹으로 저장되며 <see cref="DirectorPrefabPath"/>의 스테이지 참조(씬 오브젝트)가
/// 전부 끊겨 있다(groupParents가 fileID 0). 건물 프리팹 안에 같은 이름의 그룹(1층·2층·지붕)이
/// 있으므로, 배치 시 이름으로 다시 묶는다. 이 재바인딩이 이 빌더의 존재 이유다 — 프리팹 두 개를
/// 손으로 놓기만 해서는 조립 연출이 돌지 않는다.
///
/// 건물 프리팹은 원본 씬의 배치 좌표(수백 유닛 오프셋)를 그대로 물고 있어, 인스턴스의 렌더러
/// 바운드를 실측해 바닥 기준으로 스테이징 위치에 앉힌다. 팀원 소유 스크립트
/// (BuildingStageDirector)는 부착된 그대로 쓰고 수정하지 않는다.
///
/// 배치와 함께 <see cref="SungnyemunBuildTrigger"/>를 루트에 붙여 게임 흐름과 연결하고, 그 트리거가
/// 재생할 건설 컷씬 씬은 <see cref="CreateBuildCutsceneScene"/>이 따로 만든다. 컷씬 씬에는 카메라와
/// 스테이지 스크립트만 들어가며 건물은 이 씬에 그대로 남는다.
/// </summary>
static class SungnyemunImportBuilder
{
    const string PlayScenePath = "Assets/Scenes/Play.unity";
    const string BuildingPrefabPath = "Assets/Art/Environment/Sungnyemun/texture/modedprefab 1.prefab";
    const string DirectorPrefabPath = "Assets/Art/Environment/Sungnyemun/texture/SungnyemunDirector.prefab";
    const string MaterialRoot = "Assets/Art/Environment/Sungnyemun";
    const string ImportRootName = "SungnyemunImport";

    const string CutsceneSceneDirectory = "Assets/Developers/RYU/Scenes";
    const string CutsceneScenePath = CutsceneSceneDirectory + "/Cutscene_SungnyemunBuild.unity";

    /// <summary>
    /// warehouse 동쪽 바깥, 다른 이식 묶음(WorkshopImport -40 · TutorialImport -10 · NpcImport +20,
    /// 모두 x=120)보다 더 동쪽. 숭례문은 성문 실물 스케일일 수 있어 바운드 실측으로 다시 앉히므로
    /// 이 값은 발밑 중심의 목표점이다.
    /// </summary>
    static readonly Vector3 StagingPosition = new(200f, 13.05f, -40f);

    /// <summary>디렉터 스테이지 순서(1층·2층·3)와 건물 그룹 이름의 대응.</summary>
    static readonly string[] StageGroupNames = { "1층", "2층", "지붕" };

    [MenuItem("Tools/PROJECT 이음/Play 숭례문 가져오기")]
    static void ImportFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "Play 숭례문",
                $"숭례문 건물과 조립 연출 디렉터를 Play 씬의 '{ImportRootName}' 아래로 배치합니다.\n" +
                "기존 " + ImportRootName + "이 있으면 지우고 다시 배치합니다.",
                "배치", "취소"))
            return;

        Build();
    }

    /// <summary>배치모드 진입점: -executeMethod SungnyemunImportBuilder.ImportFromBatchMode</summary>
    public static void ImportFromBatchMode() => Build();

    static void Build()
    {
        var buildingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPrefabPath);
        var directorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DirectorPrefabPath);
        if (buildingPrefab == null || directorPrefab == null)
        {
            Debug.LogError(
                "[Sungnyemun] 숭례문 프리팹을 찾지 못했습니다. Assets/Art/Environment/Sungnyemun이 아직 " +
                "임포트되지 않았다면 에디터에 포커스를 주어 임포트를 마친 뒤 다시 실행하십시오.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayScenePath) == null)
        {
            Debug.LogError("[Sungnyemun] Play 씬을 찾지 못했습니다.");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ConvertMaterials();

        var playWasLoaded = SceneManager.GetSceneByPath(PlayScenePath).isLoaded;
        var play = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Additive);

        RemoveExistingImport(play);

        var root = new GameObject(ImportRootName);
        SceneManager.MoveGameObjectToScene(root, play);
        root.transform.position = StagingPosition;

        // 건물: 원본 씬 좌표를 버리고 바운드 실측으로 발밑을 스테이징 지점에 맞춘다.
        var building = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab, play);
        building.transform.SetParent(root.transform, false);
        building.transform.localPosition = Vector3.zero;
        building.transform.localRotation = Quaternion.identity;
        GroundByBounds(building.transform, StagingPosition);

        // 디렉터: 건물 옆에 두고 스테이지를 건물 그룹으로 재바인딩한다.
        var director = (GameObject)PrefabUtility.InstantiatePrefab(directorPrefab, play);
        director.transform.SetParent(root.transform, false);
        director.transform.localPosition = Vector3.zero;
        var boundCount = BindStages(director, building.transform);

        // 게임 흐름 연결. 퀘스트 완료 시 건설 컷씬을 걸고, 이미 지난 공정이면 조용히 세워 둔다.
        // 디렉터가 이 루트의 자식이라 트리거가 스스로 찾아낸다.
        if (root.GetComponent<SungnyemunBuildTrigger>() == null)
            root.AddComponent<SungnyemunBuildTrigger>();

        EditorSceneManager.SaveScene(play);
        if (!playWasLoaded) EditorSceneManager.CloseScene(play, true);

        Debug.Log(
            $"[Sungnyemun] 배치 완료: '{ImportRootName}' @ {StagingPosition}, 스테이지 재바인딩 {boundCount}건.\n" +
            "SungnyemunBuildTrigger를 함께 붙였습니다 — 퀘스트가 완료 상태에 들어가면 " +
            $"cutscene.json의 'sungnyemun_build'를 재생합니다. 그 컷씬 씬은 " +
            "'Tools/PROJECT 이음/숭례문 건설 컷씬 씬 생성'으로 만듭니다.\n" +
            "연출만 따로 확인하려면 플레이 중 SungnyemunDirector의 BuildingStageDirector 컴포넌트 우클릭 → " +
            "'Start Auto Build All' (또는 'Build Next Stage'로 한 단계씩). autoBuildOnStart는 꺼 두었으므로 " +
            "씬 로드만으로는 돌지 않습니다.");
    }

    static void RemoveExistingImport(Scene play)
    {
        foreach (var rootObject in play.GetRootGameObjects())
            if (rootObject.name == ImportRootName)
                Object.DestroyImmediate(rootObject);
    }

    /// <summary>
    /// 렌더러 바운드를 합쳐 발밑 중심(바운드 밑면의 중앙)을 target으로 옮긴다. 프리팹 내부의
    /// 수백 유닛짜리 원본 배치 오프셋이 어디를 향하든 결과가 같아지는 유일한 방법이다.
    /// </summary>
    static void GroundByBounds(Transform building, Vector3 target)
    {
        var renderers = building.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[Sungnyemun] 건물에서 렌더러를 찾지 못해 바운드 보정을 건너뜁니다.", building);
            return;
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        var foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        building.position += target - foot;

        Debug.Log(
            $"[Sungnyemun] 건물 크기 실측: {bounds.size.x:F1} x {bounds.size.y:F1} x {bounds.size.z:F1} " +
            $"(렌더러 {renderers.Length}개). 너무 크거나 작으면 '{ImportRootName}' 루트 스케일로 조절하십시오.");
    }

    /// <summary>
    /// 디렉터 스테이지의 끊긴 groupParents를 건물의 동명 그룹으로 다시 묶는다. 스테이지 순서는
    /// 원본 프리팹 그대로(1층·2층·3)이고 <see cref="StageGroupNames"/>가 그 순서에 대응한다.
    /// </summary>
    static int BindStages(GameObject director, Transform building)
    {
        var component = director.GetComponent<BuildingStageDirector>();
        if (component == null)
        {
            Debug.LogError("[Sungnyemun] 디렉터 프리팹에 BuildingStageDirector가 없습니다.", director);
            return 0;
        }

        var serialized = new SerializedObject(component);
        var stages = serialized.FindProperty("stages");
        var bound = 0;

        var count = Mathf.Min(stages.arraySize, StageGroupNames.Length);
        if (stages.arraySize != StageGroupNames.Length)
            Debug.LogWarning(
                $"[Sungnyemun] 스테이지 수({stages.arraySize})와 그룹 이름 수({StageGroupNames.Length})가 " +
                "다릅니다. 앞에서부터 맞는 만큼만 바인딩합니다.", component);

        for (var i = 0; i < count; i++)
        {
            var group = FindDirectChild(building, StageGroupNames[i]);
            if (group == null)
            {
                Debug.LogWarning($"[Sungnyemun] 건물에서 그룹 '{StageGroupNames[i]}'을 찾지 못했습니다.", building);
                continue;
            }

            var parents = stages.GetArrayElementAtIndex(i).FindPropertyRelative("groupParents");
            if (parents.arraySize == 0) parents.arraySize = 1;
            parents.GetArrayElementAtIndex(0).objectReferenceValue = group;
            bound++;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return bound;
    }

    /// <summary>
    /// 건설 컷씬 씬을 만든다. 연출 대상인 숭례문은 Play 씬에 그대로 두므로 이 씬에 들어가는 것은
    /// 카메라와 <see cref="SungnyemunBuildStage"/>뿐이다 — 겹쳐 올린 씬이 주 씬의 디렉터를 몰아간다.
    ///
    /// 기본 오브젝트(카메라·태양광)는 지운다. MainCamera 태그가 붙은 카메라는 주 씬 카메라와
    /// Camera.main을 두고 다투고, 광원은 주 씬 위에 두 번째 태양을 얹는 셈이 된다.
    /// </summary>
    [MenuItem("Tools/PROJECT 이음/숭례문 건설 컷씬 씬 생성")]
    static void CreateBuildCutsceneScene()
    {
        if (File.Exists(CutsceneScenePath) &&
            !EditorUtility.DisplayDialog(
                "숭례문 건설 컷씬 씬",
                $"{CutsceneScenePath}이 이미 있습니다. 덮어쓸까요?",
                "덮어쓰기", "취소"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var defaultCamera = Camera.main;
        if (defaultCamera != null) Object.DestroyImmediate(defaultCamera.gameObject);

        var defaultLight = Object.FindAnyObjectByType<Light>();
        if (defaultLight != null) Object.DestroyImmediate(defaultLight.gameObject);

        var stageObject = new GameObject("SungnyemunBuildStage");
        var stage = stageObject.AddComponent<SungnyemunBuildStage>();

        var cameraObject = new GameObject("CutsceneCamera", typeof(Camera));
        cameraObject.transform.SetParent(stageObject.transform, false);

        var stageCamera = cameraObject.GetComponent<Camera>();
        stageCamera.clearFlags = CameraClearFlags.Skybox;
        stageCamera.depth = 10f;

        // 꺼 둔 채로 저장한다. CutsceneDirector.TakeOverCamera가 암전 뒤에 켜면서 주 씬 카메라를
        // 내리므로, 켠 채로 두면 씬이 올라온 순간부터 교체 전까지 두 카메라가 함께 그려진다.
        stageCamera.enabled = false;

        // AudioListener는 붙이지 않는다. 주 씬에 이미 하나 있고 둘이 되면 Unity가 경고를 낸다.

        var serialized = new SerializedObject(stage);
        serialized.FindProperty("stageCamera").objectReferenceValue = stageCamera;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.ClearAll();
        Directory.CreateDirectory(CutsceneSceneDirectory);
        EditorSceneManager.SaveScene(scene, CutsceneScenePath);
        AssetDatabase.Refresh();

        RegisterInBuildSettings(CutsceneScenePath);

        Debug.Log(
            $"[Sungnyemun] 컷씬 씬 생성 완료: {CutsceneScenePath}\n" +
            "카메라 궤도와 연출 길이는 SungnyemunBuildStage 인스펙터에서 조절합니다. " +
            "재생은 Play 씬의 SungnyemunBuildTrigger가 퀘스트 완료 시점에 겁니다.",
            stage);
    }

    /// <summary>
    /// Build Settings에 씬을 넣는다. additive 로드는 등록되지 않은 씬을 열지 못한다. 공용 설정이므로
    /// 먼저 묻는다 — <see cref="DevSceneBuilder"/>의 동명 헬퍼와 같은 절차이고, 그쪽이 private이라
    /// 같은 로직을 여기에 둔다.
    /// </summary>
    static void RegisterInBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes;
        for (var i = 0; i < scenes.Length; i++)
            if (scenes[i].path == scenePath)
                return;

        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "Build Settings",
                $"'{scenePath}'이 Build Settings에 없습니다.\n\n" +
                "등록하지 않으면 컷씬을 겹쳐 올릴 수 없습니다. 지금 추가할까요?",
                "추가", "건너뛰기"))
        {
            Debug.LogWarning(
                $"[Sungnyemun] {scenePath}이 Build Settings에 없습니다. 추가하기 전까지 컷씬 로드가 실패합니다.");
            return;
        }

        var updated = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(updated, 0);
        updated[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = updated;
    }

    static Transform FindDirectChild(Transform parent, string name)
    {
        for (var i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name)
                return parent.GetChild(i);
        return null;
    }

    /// <summary>
    /// URP가 아닌 셰이더를 URP/Lit으로 바꾼다. 추출 자산 전수 조사에서는 133개 전부 이미
    /// URP/Lit이라 실제로는 안전판이다 — 팀원이 내장 셰이더 자산을 추가로 보내오는 경우를 대비한다.
    /// </summary>
    static void ConvertMaterials()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogWarning("[Sungnyemun] URP/Lit 셰이더를 찾지 못해 머티리얼 검사를 건너뜁니다.");
            return;
        }

        var converted = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null) continue;
            if (material.shader.name.StartsWith("Universal Render Pipeline/")) continue;

            var mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

            material.shader = urpLit;
            if (mainTexture != null) material.SetTexture("_BaseMap", mainTexture);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            converted++;
        }

        if (converted > 0) AssetDatabase.SaveAssets();
        Debug.Log($"[Sungnyemun] 머티리얼 검사 완료: URP 변환 {converted}건.");
    }
}
