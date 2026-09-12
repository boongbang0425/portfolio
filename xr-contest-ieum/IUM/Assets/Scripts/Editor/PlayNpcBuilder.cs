using System.Collections.Generic;
using GazeSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GongpoScene의 NPC 두 명(이음이·노장)을 Play 씬으로 이식한다. <see cref="PlayWorkshopBuilder"/>와
/// 같은 임시 복사본 패턴이라 팀원 소유 GongpoScene 원본에는 어떤 변경도 기록하지 않는다.
///
/// 가져올 대상은 NPC 전용 컴포넌트(GazeTracker·애니메이션 트리거)로 고른다. 씬 구성 기준으로
/// 'iumi'(iumi.fbx 인스턴스 + 씬에서 부착한 GazeTracker·EeumAnimationTrigger)와
/// 'legendOldman'(legendOldman.prefab 인스턴스, 시선 스택 내장) 두 루트가 걸린다. 루트째 옮기므로
/// 프리팹 링크는 유지된다.
///
/// 참조 추적은 <see cref="TutorialImportBuilder"/>와 달리 **따라가지 않고 끊는다**. NPC가 작업물
/// 오브젝트를 직렬화로 가리키고 있다면 그 원본은 PlayWorkshopBuilder가 이미 Play 씬에 옮겼으므로,
/// 여기서 또 끌고 오면 같은 목재가 두 벌이 된다. 시선 시스템은 대상(GazeTarget)·플레이어를
/// 런타임에 스스로 다시 찾으므로(FindObjectsOfType 계열) 끊긴 참조는 경고로만 남긴다.
///
/// 플레이어 쪽 요건은 <see cref="PlayerGazeTargetAutoSetup"/> 하나다. 이 컴포넌트가 Start에서
/// 얼굴(메인 카메라)·양손(이름 규칙 탐색) GazeTarget을 스스로 만들어 붙이므로, 빌더가 Play 씬의
/// Player 루트에 없으면 부착해 둔다. GongpoScene은 수동 GazeTarget을 썼지만 라이브러리 설명서가
/// 권하는 이식 경로는 이쪽이다. paaalop 소유 스크립트는 부착만 하고 수정하지 않는다.
///
/// 배치는 스테이징이 전부다. <see cref="ImportRootName"/> 루트에 묶어 다른 이식 묶음과 떨어진
/// 맵 밖 <see cref="StagingPosition"/>에 내려놓고, 실제 위치는 R이 에디터에서 정한다. 이음이는
/// 비행 추적 NPC(isFlyingNPC)라 실행 즉시 플레이어를 따라오므로 초기 위치의 의미가 작고,
/// 노장은 내려놓은 자리에 선다.
/// </summary>
static class PlayNpcBuilder
{
    const string PlayScenePath = "Assets/Scenes/Play.unity";
    const string GongpoScenePath = "Assets/Scenes/GongpoScene.unity";
    const string TempScenePath = "Assets/Developers/RYU/Scenes/__NpcImportTemp.unity";
    const string ImportRootName = "NpcImport";

    /// <summary>
    /// warehouse 동쪽 벽 바깥. WorkshopImport(120, 13.1, -40)·TutorialImport(120, 13.1, -10)에서
    /// 다시 북쪽으로 30유닛 떨어져 세 이식 묶음이 겹치지 않는다.
    /// </summary>
    static readonly Vector3 StagingPosition = new(120f, 13.1f, 20f);

    /// <summary>이 중 하나라도 트리에 있으면 NPC 루트로 본다. 플레이어 쪽 GazeTarget은 넣지 않는다.</summary>
    static readonly System.Type[] NpcComponentTypes =
    {
        typeof(GazeTracker),
        typeof(NPCGazeController),
        typeof(EeumAnimationTrigger),
        typeof(ElderlyAnimationTrigger)
    };

    /// <summary>
    /// NPC 컴포넌트가 트리에 있어도 NPC가 아닌 루트. GongpoScene의 Player는 Head에 GazeTarget만
    /// 달고 있어 위 타입에 안 걸리지만, 구성이 바뀌어도 끌려오지 않도록 명시해 둔다.
    /// </summary>
    static readonly HashSet<string> ExcludedRoots = new()
    {
        "Player",
        "Main Camera",
        "Directional Light",
        "Environment",
        "Canvas",
        "EventSystem",
        "XR Interaction Manager"
    };

    [MenuItem("Tools/PROJECT 이음/Play NPC 가져오기")]
    static void ImportFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "Play NPC",
                $"GongpoScene의 이음이·노장을 Play 씬의 '{ImportRootName}' 아래로 가져옵니다.\n" +
                "기존 " + ImportRootName + "이 있으면 지우고 다시 가져옵니다.",
                "가져오기", "취소"))
            return;

        Build();
    }

    /// <summary>배치모드 진입점: -executeMethod PlayNpcBuilder.ImportFromBatchMode</summary>
    public static void ImportFromBatchMode() => Build();

    static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(GongpoScenePath) == null)
        {
            Debug.LogError("[NpcImport] Play 씬 또는 GongpoScene을 찾지 못했습니다.");
            return;
        }

        // 열려 있는 씬의 미저장 변경을 지키는 쪽은 에디터 사용자 몫이다. 배치모드에는 물을 곳이 없다.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        AssetDatabase.DeleteAsset(TempScenePath);
        if (!AssetDatabase.CopyAsset(GongpoScenePath, TempScenePath))
        {
            Debug.LogError("[NpcImport] GongpoScene 임시 복사에 실패했습니다.");
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
            var selected = SelectNpcRoots(source);
            if (selected.Count == 0)
            {
                Debug.LogWarning("[NpcImport] GongpoScene에서 NPC 루트를 찾지 못했습니다.");
                return;
            }

            ReportOutboundReferences(source, selected);

            var importRoot = new GameObject(ImportRootName);
            SceneManager.MoveGameObjectToScene(importRoot, play);

            foreach (var root in selected)
                root.transform.SetParent(importRoot.transform, true);

            // 원래의 상대 배치(노장과 이음이의 간격)를 유지한 채 전체를 맵 밖으로 옮긴다.
            importRoot.transform.position = StagingPosition;

            EnsurePlayerGazeSetup(play);
            SwapNpcBehaviours(importRoot);

            EditorSceneManager.MarkSceneDirty(play);
            EditorSceneManager.SaveScene(play, PlayScenePath);

            Debug.Log($"[NpcImport] 루트 {selected.Count}개를 '{ImportRootName}'({StagingPosition})로 가져왔습니다: " +
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

    /// <summary>재실행 지원. 이전 가져오기 결과를 지워 NPC가 두 벌이 되지 않게 한다.</summary>
    static void RemoveExistingImport(Scene play)
    {
        foreach (var root in play.GetRootGameObjects())
            if (root.name == ImportRootName)
                Object.DestroyImmediate(root);
    }

    static List<GameObject> SelectNpcRoots(Scene source)
    {
        var selected = new List<GameObject>();
        foreach (var root in source.GetRootGameObjects())
        {
            if (ExcludedRoots.Contains(root.name)) continue;
            if (HasNpcComponent(root)) selected.Add(root);
        }

        return selected;
    }

    static bool HasNpcComponent(GameObject root)
    {
        for (var i = 0; i < NpcComponentTypes.Length; i++)
            if (root.GetComponentInChildren(NpcComponentTypes[i], true) != null)
                return true;

        return false;
    }

    /// <summary>
    /// NPC 트리에서 바깥 루트로 나가는 직렬화 참조를 찾아 경고만 남긴다. 따라가서 끌고 오지 않는
    /// 이유는 클래스 주석에 있다 — 작업물이 두 벌이 되는 쪽이 끊긴 참조보다 나쁘다.
    /// </summary>
    static void ReportOutboundReferences(Scene source, List<GameObject> selected)
    {
        var selectedSet = new HashSet<GameObject>(selected);

        foreach (var root in selected)
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue; // Missing script는 건너뛴다.

            using var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                var referencedRoot = RootOf(property.objectReferenceValue, source);
                if (referencedRoot == null || selectedSet.Contains(referencedRoot)) continue;

                Debug.LogWarning(
                    $"[NpcImport] {component.GetType().Name}.{property.name}이 NPC 밖 루트 " +
                    $"'{referencedRoot.name}'을 참조합니다. 이 참조는 가져온 뒤 끊기며, 시선 시스템은 " +
                    "런타임 탐색으로 대상을 다시 찾습니다.", component);
            }
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

    /// <summary>
    /// 이식된 사본의 paaalop 트리거를 우리 재작성판(<see cref="EeumBehaviour"/>·
    /// <see cref="NojangBehaviour"/>)으로 바꾼다. GongpoScene 원본 인스턴스는 paaalop 것을 그대로
    /// 쓰고, Play 씬 사본만 정돈된 구현으로 돈다 — 두 구현이 병존하므로 비교·롤백이 언제든 된다.
    ///
    /// 절차는 3상이다: ① 새 컴포넌트를 만들고 직렬화 값을 같은 이름 기준으로 이전(신구 클래스의
    /// 필드명이 일부러 동일하다) → ② 트리 전체에서 옛 컴포넌트를 가리키던 직렬화 참조를 새것으로
    /// 재조준(patrolRoutes의 onArrived 퍼시스턴트 바인딩 포함 — 대상 타입명 문자열까지 고친다) →
    /// ③ 옛 컴포넌트 제거. 노장은 프리팹 인스턴스라 제거가 removed-component 오버라이드로 기록되고
    /// 프리팹 링크는 유지된다.
    /// </summary>
    static void SwapNpcBehaviours(GameObject importRoot)
    {
        var replacements = new Dictionary<Component, Component>();

        foreach (var old in importRoot.GetComponentsInChildren<EeumAnimationTrigger>(true))
            replacements.Add(old, CreateReplacement<EeumBehaviour>(old));
        foreach (var old in importRoot.GetComponentsInChildren<ElderlyAnimationTrigger>(true))
            replacements.Add(old, CreateReplacement<NojangBehaviour>(old));

        if (replacements.Count == 0)
        {
            Debug.LogWarning("[NpcImport] 교체할 NPC 트리거 컴포넌트를 찾지 못했습니다.");
            return;
        }

        RetargetSerializedReferences(importRoot, replacements);

        foreach (var pair in replacements)
        {
            Debug.Log($"[NpcImport] {pair.Key.gameObject.name}: {pair.Key.GetType().Name} → " +
                      $"{pair.Value.GetType().Name} 교체 완료.", pair.Value);
            Object.DestroyImmediate(pair.Key);
        }
    }

    /// <summary>새 컴포넌트를 같은 오브젝트에 만들고, 이름·타입이 일치하는 직렬화 값을 이전한다.</summary>
    static Component CreateReplacement<TNew>(Component old) where TNew : Component
    {
        var fresh = old.gameObject.AddComponent<TNew>();

        using var source = new SerializedObject(old);
        using var destination = new SerializedObject(fresh);

        var property = source.GetIterator();
        // 최상위 프로퍼티 단위로 통째로 복사한다. patrolRoutes 같은 배열·중첩 구조는 같은 타입
        // (GazeSystem.WaypointAction)을 공유하므로 서브트리 복사가 그대로 성립한다.
        for (var enter = true; property.NextVisible(enter); enter = false)
        {
            if (property.name == "m_Script") continue;

            var target = destination.FindProperty(property.propertyPath);
            if (target == null || target.propertyType != property.propertyType)
            {
                Debug.LogWarning(
                    $"[NpcImport] {old.GetType().Name}.{property.propertyPath} 값을 이전할 대상이 " +
                    $"{typeof(TNew).Name}에 없습니다. 기본값으로 둡니다.", fresh);
                continue;
            }

            destination.CopyFromSerializedProperty(property);
        }

        destination.ApplyModifiedPropertiesWithoutUndo();
        return fresh;
    }

    /// <summary>
    /// 트리 전체의 직렬화 참조 중 옛 트리거를 가리키는 것을 새 컴포넌트로 재조준한다.
    /// UnityEvent 퍼시스턴트 콜의 m_Target이면 짝이 되는 대상 타입명 문자열도 함께 고치고,
    /// 바인딩된 메서드가 새 클래스에 실제로 있는지 검증한다 — 이름이 어긋나면 조용히 죽기 때문이다.
    /// </summary>
    static void RetargetSerializedReferences(
        GameObject importRoot, Dictionary<Component, Component> replacements)
    {
        foreach (var component in importRoot.GetComponentsInChildren<Component>(true))
        {
            if (component == null || replacements.ContainsKey(component)) continue;

            using var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            var dirty = false;

            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.objectReferenceValue is not Component referenced ||
                    !replacements.TryGetValue(referenced, out var replacement))
                    continue;

                property.objectReferenceValue = replacement;
                dirty = true;

                if (property.name != "m_Target") continue;

                var basePath = property.propertyPath[..^"m_Target".Length];
                var typeName = serialized.FindProperty(basePath + "m_TargetAssemblyTypeName");
                if (typeName != null)
                {
                    var newType = replacement.GetType();
                    typeName.stringValue = $"{newType.FullName}, {newType.Assembly.GetName().Name}";
                }

                var methodName = serialized.FindProperty(basePath + "m_MethodName");
                if (methodName != null && !HasPublicMethod(replacement.GetType(), methodName.stringValue))
                    Debug.LogError(
                        $"[NpcImport] 재바인딩된 이벤트가 부르는 '{methodName.stringValue}'가 " +
                        $"{replacement.GetType().Name}에 없습니다. 해당 연출이 무시됩니다.", replacement);
            }

            if (dirty) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static bool HasPublicMethod(System.Type type, string methodName)
    {
        if (string.IsNullOrEmpty(methodName)) return false;

        foreach (var method in type.GetMethods())
            if (method.Name == methodName)
                return true;

        return false;
    }

    /// <summary>
    /// Play 씬 Player에 <see cref="PlayerGazeTargetAutoSetup"/>을 보장한다. NPC 시선이 바라볼
    /// 얼굴·양손 GazeTarget을 이 컴포넌트가 Start에서 만들어 붙인다. 이미 있으면 그대로 둔다.
    /// </summary>
    static void EnsurePlayerGazeSetup(Scene play)
    {
        foreach (var root in play.GetRootGameObjects())
        {
            var player = root.GetComponentInChildren<Player>(true);
            if (player == null) continue;

            if (player.GetComponent<PlayerGazeTargetAutoSetup>() == null)
            {
                player.gameObject.AddComponent<PlayerGazeTargetAutoSetup>();
                Debug.Log("[NpcImport] Player에 PlayerGazeTargetAutoSetup을 부착했습니다.", player);
            }

            return;
        }

        Debug.LogWarning("[NpcImport] Play 씬에서 Player를 찾지 못해 시선 타겟 자동 설정을 붙이지 못했습니다.");
    }
}
