using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// StartScene의 플레이어를 GongpoScene의 플레이어와 같은 구성으로 바꾼다.
///
/// 왜 필요한가 — 두 씬의 플레이어는 같은 뿌리(<c>Assets/Prefabs/Player.prefab</c>)에서 나왔지만
/// 지금은 갈라져 있다. StartScene 쪽은 그 프리팹의 인스턴스 그대로이고, GongpoScene 쪽은 씬 로컬
/// 사본으로 풀린 뒤 Head에 <c>GazeSystem.GazeTarget</c>이 붙고 손 레이저에 머티리얼이 배정됐으며
/// 카메라의 URP 옵션(깊이·불투명 텍스처)이 바뀌었다. 프리팹 쪽에는 그 변경이 하나도 반영되지
/// 않았다 — 즉 값 몇 개가 아니라 컴포넌트 구성이 다르므로, 필드만 맞추는 대신 공포씬 구성을
/// 그대로 복제해 갈아 끼운다.
///
/// 승계하는 것은 <b>배치</b>뿐이다. 루트 월드 위치·회전과 Head의 로컬 회전(피치)을 기존
/// "Start Scene Player"에서 그대로 가져온다. 그 셋이 FixedUI 원본 카메라(비활성 Main Camera)의
/// 포즈를 재현하도록 맞춰져 있어서, 하나라도 흘리면 시작 화면이 엉뚱한 곳을 본다.
///
/// 씬 파일을 손으로 고치지 않는다. 메뉴로 실행하면 열려 있는 씬만 바꾸고 저장은 하지 않으며,
/// 배치 실행(<see cref="RunBatch"/>)은 StartScene을 직접 열고 저장까지 한다.
/// </summary>
static class StartScenePlayerSwapTool
{
    const string MenuPath = "Tools/PROJECT 이음/시작씬 플레이어를 공포씬 구성으로 교체";
    const string Tag = "[StartPlayerSwap]";

    const string StartScenePath = "Assets/Scenes/StartScene.unity";
    const string GongpoScenePath = "Assets/Scenes/GongpoScene.unity";

    /// <summary>공포씬 손 레이저에 배정된 것과 같은 셰이더의 내장 머티리얼.</summary>
    const string LaserMaterialPath = "Sprites/Default.mat";

    /// <summary>월드 포즈 승계 허용 오차(미터).</summary>
    const float PoseTolerance = 0.0005f;

    /// <summary>회전 승계 허용 오차(도).</summary>
    const float AngleTolerance = 0.02f;

    // ---- 진입점 ----

    [MenuItem(MenuPath)]
    static void RunFromMenu()
    {
        var scene = SceneManager.GetSceneByPath(StartScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "시작씬 플레이어 교체",
                $"{StartScenePath}를 먼저 열어 주세요.\n이 도구는 열려 있는 씬만 바꾸며 저장하지 않습니다.",
                "확인");
            return;
        }

        if (!Run(scene, out var report))
        {
            EditorUtility.DisplayDialog("시작씬 플레이어 교체", report, "확인");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"{Tag} {report}");
        EditorUtility.DisplayDialog("시작씬 플레이어 교체", report + "\n\n확인 후 Ctrl+S로 저장하세요.", "확인");
    }

    /// <summary>
    /// 배치 모드 진입점. <c>-executeMethod StartScenePlayerSwapTool.RunBatch</c>로 부른다.
    /// 씬을 직접 열고 저장까지 하며, 종료 코드로 성패를 알린다.
    /// </summary>
    public static void RunBatch()
    {
        var code = 1;

        try
        {
            var scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);

            if (Run(scene, out var report))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"{Tag} 저장 완료. {report}");
                code = 0;
            }
            else
            {
                Debug.LogError($"{Tag} 실패. {report}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        EditorApplication.Exit(code);
    }

    // ---- 본체 ----

    static bool Run(Scene startScene, out string report)
    {
        report = string.Empty;

        if (!TryFindPlayer(startScene, out var oldPlayer, out report)) return false;

        // 이미 교체된 씬을 다시 돌려도 손해는 없지만, 무엇을 했는지 로그가 흐려지므로 먼저 알린다.
        var alreadySwapped = !PrefabUtility.IsPartOfPrefabInstance(oldPlayer.gameObject)
                             && oldPlayer.Head != null
                             && oldPlayer.Head.GetComponent<GazeSystem.GazeTarget>() != null;

        var oldRoot = oldPlayer.transform;
        var oldName = oldRoot.gameObject.name;
        var oldPosition = oldRoot.position;
        var oldRotation = oldRoot.rotation;
        var oldSiblingIndex = oldRoot.GetSiblingIndex();
        var oldHeadLocalRotation = oldPlayer.Head != null ? oldPlayer.Head.localRotation : Quaternion.identity;
        var oldHeadWorldRotation = oldPlayer.Head != null ? oldPlayer.Head.rotation : oldRotation;

        var gongpoScene = SceneManager.GetSceneByPath(GongpoScenePath);
        var openedHere = !gongpoScene.IsValid() || !gongpoScene.isLoaded;

        if (openedHere) gongpoScene = EditorSceneManager.OpenScene(GongpoScenePath, OpenSceneMode.Additive);

        try
        {
            if (!TryFindPlayer(gongpoScene, out var sourcePlayer, out var sourceError))
            {
                report = $"공포씬 플레이어를 찾지 못해 아무것도 바꾸지 않았습니다. {sourceError}";
                return false;
            }

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("시작씬 플레이어 교체");

            var clone = Object.Instantiate(sourcePlayer.gameObject);
            SceneManager.MoveGameObjectToScene(clone, startScene);
            Undo.RegisterCreatedObjectUndo(clone, "시작씬 플레이어 교체");

            clone.name = oldName;
            clone.transform.SetParent(null, true);
            clone.transform.SetPositionAndRotation(oldPosition, oldRotation);

            var clonePlayer = clone.GetComponent<Player>();
            var cloneHead = clonePlayer != null ? clonePlayer.Head : null;
            if (cloneHead != null && cloneHead != clone.transform) cloneHead.localRotation = oldHeadLocalRotation;

            SyncEulerHint(clone.transform);
            if (cloneHead != null) SyncEulerHint(cloneHead);

            FixLaserMaterials(clone, out var laserNote);

            Undo.DestroyObjectImmediate(oldRoot.gameObject);
            clone.transform.SetSiblingIndex(Mathf.Min(oldSiblingIndex, startScene.rootCount - 1));

            Undo.CollapseUndoOperations(undoGroup);

            report = BuildReport(clonePlayer, oldPosition, oldHeadWorldRotation, laserNote, alreadySwapped);
            return true;
        }
        finally
        {
            // 공포씬은 읽기 전용으로만 썼다. 저장하지 않고 원래대로 닫는다.
            if (openedHere && gongpoScene.IsValid() && gongpoScene.isLoaded)
                EditorSceneManager.CloseScene(gongpoScene, true);
        }
    }

    /// <summary>
    /// 손 레이저 머티리얼을 내장 자산으로 바꾼다. 공포씬의 것은 그 씬 안에 박힌 오브젝트라
    /// 다른 씬에서 참조할 수 없다 — 셰이더가 같은 내장 <c>Sprites/Default</c>로 대신한다.
    /// 비워 두면 <c>GrabHandModule</c>이 이미 있는 LineRenderer에는 폴백을 넣지 않아 레이저가
    /// 통째로 보이지 않는다.
    /// </summary>
    static void FixLaserMaterials(GameObject root, out string note)
    {
        var lines = root.GetComponentsInChildren<LineRenderer>(true);
        var material = ResolveLaserMaterial(out var source);

        if (material == null)
        {
            note = $"LineRenderer {lines.Length}개 — 레이저 머티리얼을 만들지 못해 미배정";
            return;
        }

        var assigned = 0;

        foreach (var line in lines)
        {
            if (line == null) continue;

            line.sharedMaterial = material;
            assigned++;
        }

        note = $"LineRenderer {lines.Length}개 중 {assigned}개에 {source} 배정";
    }

    /// <summary>
    /// 손 레이저용 머티리얼. 내장 자산으로 잡히면 그것이 가장 깨끗하고(씬에 아무것도 박히지
    /// 않는다), 배치 모드처럼 내장 추가 자산이 올라오지 않는 환경에서는 <c>Sprites/Default</c>
    /// 셰이더로 하나 만들어 씬에 박는다 — 공포씬에 들어 있는 것과 정확히 같은 물건이다.
    /// URP의 Unlit 계열은 정점 색을 무시해 <c>LineRenderer.startColor</c>의 붉은 그라데이션이
    /// 죽으므로 셰이더를 바꿔서는 안 된다(<c>GrabHandModule.SharedLaserMaterial</c>과 같은 근거).
    /// </summary>
    static Material ResolveLaserMaterial(out string source)
    {
        var builtin = AssetDatabase.GetBuiltinExtraResource<Material>(LaserMaterialPath);
        if (builtin != null)
        {
            source = $"내장 {LaserMaterialPath}";
            return builtin;
        }

        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            source = "없음";
            return null;
        }

        source = "씬 내장 Sprites/Default 머티리얼(신규 생성)";
        return new Material(shader) { name = "Sprites/Default" };
    }

    /// <summary>
    /// 인스펙터가 보여 주는 오일러 각(<c>m_LocalEulerAnglesHint</c>)을 실제 회전에 맞춘다.
    /// 복제본은 공포씬의 힌트를 그대로 물고 오는데, 우리가 회전을 갈아 끼웠으므로 그 값은 거짓이
    /// 된다. 표시만 어긋나는 것이 아니라, 인스펙터에서 회전을 한 번 건드리면 그 거짓값이 실제
    /// 회전으로 튀어 오른다.
    /// </summary>
    static void SyncEulerHint(Transform target)
    {
        var serialized = new SerializedObject(target);
        var hint = serialized.FindProperty("m_LocalEulerAnglesHint");
        if (hint == null) return;

        hint.vector3Value = target.localRotation.eulerAngles;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static bool TryFindPlayer(Scene scene, out Player player, out string error)
    {
        player = null;
        error = string.Empty;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "씬이 열려 있지 않습니다.";
            return false;
        }

        var found = new List<Player>();
        foreach (var root in scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<Player>(true));

        if (found.Count != 1)
        {
            error = $"'{scene.name}'에서 Player를 {found.Count}개 찾았습니다. 정확히 1개여야 합니다.";
            return false;
        }

        player = found[0];
        return true;
    }

    // ---- 검증 ----

    static string BuildReport(
        Player player, Vector3 expectedPosition, Quaternion expectedHeadRotation, string laserNote, bool alreadySwapped)
    {
        var lines = new List<string>();

        if (alreadySwapped) lines.Add("이미 교체된 구성이었습니다. 공포씬 원본으로 다시 갈아 끼웠습니다.");

        if (player == null) return "교체 후 Player 컴포넌트를 찾지 못했습니다.";

        var head = player.Head;
        var camera = player.GetComponentInChildren<Camera>(true);

        lines.Add($"루트 '{player.name}' 배치 {player.transform.position:F4} " +
                  $"(오차 {Vector3.Distance(player.transform.position, expectedPosition):F5})");

        if (Vector3.Distance(player.transform.position, expectedPosition) > PoseTolerance)
            lines.Add("경고: 루트 위치 승계 오차가 허용치를 넘었습니다.");

        if (head != null)
        {
            var angle = Quaternion.Angle(head.rotation, expectedHeadRotation);
            lines.Add($"Head 월드 회전 오차 {angle:F4}도");
            if (angle > AngleTolerance) lines.Add("경고: Head 회전 승계 오차가 허용치를 넘었습니다.");
        }

        // ① MainCamera 태그 — StartScenePlayerInteraction이 카메라 인수인계에 쓴다.
        var cameraTagged = camera != null && camera.CompareTag("MainCamera");
        lines.Add($"① MainCamera 태그: {(cameraTagged ? "정상" : "없음 — 확인 필요")}" +
                  (camera != null ? $" (카메라 '{camera.name}', near {camera.nearClipPlane})" : " (카메라 없음)"));

        // ② 크로스헤어 캔버스.
        var crosshair = FindChild(player.transform, "CrosshairCanvas");
        var crosshairCanvas = crosshair != null ? crosshair.GetComponent<Canvas>() : null;
        lines.Add($"② CrosshairCanvas: {(crosshairCanvas != null ? $"정상 (renderMode {crosshairCanvas.renderMode})" : "없음 — 확인 필요")}");

        // ③ XR 포인터가 쓰는 손 앵커와 레이저.
        var left = player.GetHandAnchor(XRHandSide.Left);
        var right = player.GetHandAnchor(XRHandSide.Right);
        lines.Add($"③ 손 앵커: 좌 {(left != null ? left.name : "없음")}, 우 {(right != null ? right.name : "없음")} / {laserNote}");

        lines.Add($"worldUnitsPerMetre {player.WorldUnitsPerMetre}, moveSpeed {player.MoveSpeed}, " +
                  $"grabRadius {player.GrabRadius}, distanceGrab {player.DistanceGrabMaxDistance}");

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) lines.Add($"CharacterController height {controller.height}, radius {controller.radius}");

        lines.Add($"프리팹 인스턴스 여부: {(PrefabUtility.IsPartOfPrefabInstance(player.gameObject) ? "예" : "아니오(씬 로컬 — 공포씬과 동일)")}");

        return string.Join("\n", lines);
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(child.name, name, StringComparison.Ordinal))
                return child;

        return null;
    }
}
