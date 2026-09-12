using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 열려 있는 씬에 시각 공정 가이드를 배치한다. <see cref="ProcessGuideService"/> 루트를 세우고,
/// 공정별로 대상이 될 만한 컴포넌트를 타입으로 찾아 <see cref="ProcessGuideAnchor"/>를 붙인다.
///
/// 손으로 붙이지 않는 이유는 다른 빌더들과 같다 — 어떤 오브젝트가 어느 공정의 안내 대상인지가
/// 인스펙터에만 남으면 씬을 다시 만들 때마다 눈으로 복원해야 한다. 그 판단의 기록이 여기다.
///
/// 공정이 도구 단위에서 <b>부재 단위</b>로 재편되면서 대상 선정 기준도 부재를 따른다.
/// <list type="bullet">
/// <item>ShortPart·MediumPart·LongPart — 그 부재 아래의 모든 <see cref="WorkZone"/>. 존의 작업 종류를
/// 가리지 않는다. 한 부재를 먹매김부터 끌질까지 이어서 하므로 부재 전체가 한 공정의 무대다.</item>
/// <item>도구 — 각 도구는 여러 부재 단계에서 쓰이므로 특정 공정 앵커에 묶지 않는다. 도구 잠금은
/// <see cref="MainPlayProcessBridge"/>가 목표 단위로 하고, 강조 대상은 존이 된다.</item>
/// <item>조립(<see cref="ProcessId.GongpoPuzzle"/>) — 도리 부재와 그 결합 자리, 그리고 서비스 루트에
/// 빈 앵커 하나. 열린 결합 자리는 표시 시점에 동적으로 찾는다.</item>
/// </list>
///
/// 다만 런타임에서는 <see cref="ProcessGuideService"/>가 <b>현재 목표의 신호 키</b>로 대상을 먼저
/// 산출하고, 여기서 붙이는 앵커는 그 경로가 대상을 못 찾았을 때의 폴백이다. 그래서 이 도구를
/// 다시 실행하지 못한 씬(앵커의 process 값이 옛 도구 단위 그대로인 씬)에서도 안내가 어긋나지 않는다.
///
/// 씬은 공용 자산이므로 이 도구를 실행하는 행위 자체가 R의 승인이다. 이미 앵커가 있는
/// 오브젝트는 건드리지 않으므로 여러 번 실행해도 중복되지 않는다.
/// </summary>
static class ProcessGuideBuilder
{
    const string PlayScenePath = "Assets/Scenes/Play.unity";
    const string ServiceRootName = "ProcessGuide";
    const string GongpoAnchorName = "GongpoPuzzleAnchor";
    const string UndoName = "공정 가이드 배치";

    /// <summary>도리 부재의 결합 ID. <see cref="MainPlayProcessBridge.PurlinPartId"/>를 읽기만 한다.</summary>
    const string PurlinSnapId = MainPlayProcessBridge.PurlinPartId;

    [MenuItem("Tools/PROJECT 이음/공정 가이드 배치")]
    static void BuildFromMenu()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Guide] 열려 있는 씬이 없습니다.");
            return;
        }

        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "공정 가이드 배치",
                $"'{scene.name}' 씬에 시각 공정 가이드를 배치합니다.\n" +
                $"{ServiceRootName} 루트를 만들고 공정별 앵커를 붙인 뒤 씬을 저장합니다.\n" +
                (scene.path == PlayScenePath ? string.Empty : "\n주의: Play 씬이 아닙니다.\n"),
                "배치", "취소"))
            return;

        Build(scene);
    }

    /// <summary>배치모드 진입점: -executeMethod ProcessGuideBuilder.BuildFromBatchMode</summary>
    public static void BuildFromBatchMode() => Build(SceneManager.GetActiveScene());

    static void Build(Scene scene)
    {
        if (!BuildInto(scene)) return;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            Debug.LogError($"[Guide] 씬 저장에 실패했습니다: {scene.path}");
    }

    /// <summary>
    /// 저장 없이 배치만 수행한다. <see cref="PlayWorkshopBuilder"/>가 이식 파이프라인 끝에서
    /// 호출한다 — 이식이 WorkshopImport 루트를 지우고 다시 만들면 존에 붙어 있던 앵커도 함께
    /// 사라지므로, 이식 직후 재부착해야 가이드가 유지된다. 이미 앵커가 있는 오브젝트는 건너뛰어
    /// 여러 번 실행해도 중복되지 않고, 서비스 루트는 이식 루트 밖이라 재이식에도 살아남는다.
    /// </summary>
    internal static bool BuildInto(Scene scene)
    {
        var root = ResolveServiceRoot(scene);
        if (root == null) return false;

        var report = new List<string>
        {
            Report("짧은 부재", AttachPartZones(ProcessId.ShortPart, scene)),
            Report("중간 부재", AttachPartZones(ProcessId.MediumPart, scene)),
            Report("긴 부재", AttachPartZones(ProcessId.LongPart, scene)),
            Report("도리 설치", AttachPurlin(scene)),
            Report("공포 조립", AttachGongpo(root))
        };

        Debug.Log(
            $"[Guide] 공정 가이드 배치 완료 ({scene.name})\n" + string.Join("\n", report) +
            "\n이미 앵커가 있던 오브젝트는 건드리지 않았습니다. " +
            $"강조 시간과 색은 '{ServiceRootName}'의 ProcessGuideService 인스펙터에서 조정합니다.");
        return true;
    }

    static string Report(string label, int created) =>
        $"- {label}: 앵커 {created}개 추가";

    // ---- 서비스 루트 ----

    static GameObject ResolveServiceRoot(Scene scene)
    {
        var existing = FindFirst<ProcessGuideService>(scene);
        if (existing != null) return existing.gameObject;

        // 이식 루트(WorkshopImport·TutorialImport·NpcImport) 밖의 독립 오브젝트로 세운다.
        // 이식 도구가 자기 루트를 통째로 지우고 다시 만들기 때문이다.
        var root = new GameObject(ServiceRootName);
        Undo.RegisterCreatedObjectUndo(root, UndoName);
        if (root.scene != scene) SceneManager.MoveGameObjectToScene(root, scene);

        Undo.AddComponent<ProcessGuideService>(root);
        return root;
    }

    // ---- 공정별 부착 ----

    /// <summary>
    /// 한 부재 아래의 모든 작업 존에 그 부재의 앵커를 붙인다. 소속 판정은 브릿지·서비스와 같은
    /// <see cref="PartProcessSignals.TryResolvePart"/>를 쓰므로 세 곳의 기준이 어긋나지 않는다.
    /// 연습용 목재(InkWood·SawWood)처럼 부재에 속하지 않는 존은 제외된다.
    /// </summary>
    static int AttachPartZones(ProcessId part, Scene scene)
    {
        var count = 0;
        foreach (var zone in FindAll<WorkZone>(scene))
        {
            if (!PartProcessSignals.TryResolvePart(zone, out var owner) || owner != part) continue;
            if (AttachAnchor(zone.gameObject, part, $"{part} 작업 존 ({zone.GetType().Name})."))
                count++;
        }

        return count;
    }

    /// <summary>
    /// 도리 부재와 그 부재가 들어갈 결합 자리. 부재는 결합 ID를 단
    /// <see cref="MaleSnapPoint"/>에서 <see cref="AssemblyPart"/> 루트로 거슬러 올라가 찾는다.
    /// </summary>
    static int AttachPurlin(Scene scene)
    {
        var count = 0;

        foreach (var male in FindAll<MaleSnapPoint>(scene))
        {
            if (!string.Equals(male.mySnapID, PurlinSnapId, StringComparison.Ordinal)) continue;

            var part = male.GetComponentInParent<AssemblyPart>();
            var host = part != null ? part.gameObject : male.gameObject;
            if (AttachAnchor(host, ProcessId.GongpoPuzzle, $"도리 부재 ('{PurlinSnapId}')."))
                count++;
        }

        foreach (var target in FindAll<AssemblyTarget>(scene))
        {
            if (!string.Equals(target.AcceptedPartID, PurlinSnapId, StringComparison.Ordinal)) continue;
            if (AttachAnchor(target.gameObject, ProcessId.GongpoPuzzle,
                    $"도리 부재 '{PurlinSnapId}'를 받는 결합 자리."))
                count++;
        }

        return count;
    }

    /// <summary>
    /// 공포 퍼즐은 다음 자리가 조립 진행에 따라 바뀌므로 정적 대상을 적을 수 없다. 서비스가
    /// 표시 시점에 열린 결합 자리를 찾도록, 대상이 비어 있는 앵커 하나만 둔다.
    /// </summary>
    static int AttachGongpo(GameObject root)
    {
        foreach (var anchor in root.GetComponentsInChildren<ProcessGuideAnchor>(true))
            if (anchor.Process == ProcessId.GongpoPuzzle)
                return 0;

        var holder = new GameObject(GongpoAnchorName);
        Undo.RegisterCreatedObjectUndo(holder, UndoName);
        holder.transform.SetParent(root.transform, false);

        return AttachAnchor(holder, ProcessId.GongpoPuzzle,
            "대상을 비워 두는 동적 앵커. 표시 시점에 열려 있는 AssemblyTarget을 찾아 강조한다.")
            ? 1
            : 0;
    }

    static bool AttachAnchor(GameObject target, ProcessId process, string note)
    {
        if (target == null || target.GetComponent<ProcessGuideAnchor>() != null) return false;

        var anchor = Undo.AddComponent<ProcessGuideAnchor>(target);
        var serialized = new SerializedObject(anchor);

        // ProcessId는 값을 명시하지 않은 연속 enum이라 선언 순서와 정수 값이 같다.
        serialized.FindProperty("process").enumValueIndex = (int)process;
        serialized.FindProperty("objectiveId").stringValue = string.Empty;
        serialized.FindProperty("highlightTargets").ClearArray();
        serialized.FindProperty("note").stringValue = note;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(anchor);
        return true;
    }

    // ---- 탐색 ----

    static IEnumerable<T> FindAll<T>(Scene scene) where T : Component
    {
        var found = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var component in found)
        {
            if (component == null || component.gameObject.scene != scene) continue;
            yield return component;
        }
    }

    static T FindFirst<T>(Scene scene) where T : Component
    {
        foreach (var component in FindAll<T>(scene)) return component;
        return null;
    }
}
