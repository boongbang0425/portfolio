using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 팀원 소유 GongpoScene의 목공·조립 오브젝트를 Play 씬으로 이식한다. 임시 복사본 패턴이라
/// 원본 GongpoScene과 목공 스크립트에는 어떤 변경도 기록하지 않는다.
///
/// 가져올 대상은 이름이 아니라 컴포넌트로 고른다. GongpoScene의 루트 이름은 팀원이 자유롭게 바꾸지만
/// "작업물이려면 WorkZone·도구·조립 컴포넌트를 들고 있어야 한다"는 사실은 바뀌지 않기 때문이다.
/// 선택된 오브젝트가 참조하는 다른 루트(먹줄 LineRenderer, 사영 데칼 등)는 참조를 따라가 함께
/// 가져온다 — 씬 저장 시 남는 씬으로의 참조는 끊기므로, 여기서 빠뜨리면 도구가 조용히 고장난다.
///
/// 배치는 스테이징이 전부다. 가져온 전체를 <see cref="ImportRootName"/> 루트 하나에 원래의 상대
/// 배치 그대로 묶어 warehouse 맵 밖 <see cref="StagingPosition"/>에 내려놓는다. 실제 작업장 위치는
/// R이 에디터에서 직접 정해 옮긴다.
///
/// 이식 후처리는 일곱이다. 재실행 멱등성은 기존 <see cref="ImportRootName"/> 전체 삭제로 보장된다.
/// <list type="number">
/// <item>누락 머티리얼 보정(<see cref="FixMissingMaterials"/>) — 이식 전체에서 null 슬롯을
/// darkwood로 채운다(ISSUE-031). 과거의 도구 비주얼 교체(플레이스홀더 큐브에 프리팹 부착)는
/// 제거했다 — e1cc282부터 GongpoScene의 도구가 완성 모델 프리팹 인스턴스(Saw.fbx·Hammer.fbx·
/// chisel_low (1)·blackline 등)로 교체되어 그대로 이식되고, 옛 교체 대상 프리팹(saw.prefab 등)은
/// 삭제됐다. 이름이 겹치는 "InkBox"에 옛 로직이 이중 비주얼을 붙이는 사고도 이것으로 막는다.</item>
/// <item>Forward+ 비호환 머티리얼 교체(<see cref="FixForwardPlusIncompatibleMaterials"/>) —
/// 부재·공포 블록이 쓰는 Custom/WoodTriplanar 머티리얼을 RYU 소유 호환 셰이더 사본으로
/// 바꿔 끼운다. 원본 셰이더는 Forward+/정점 광원 배리언트가 없어 추가 광원만으로 밝히는
/// Play 씬(주광 intensity 0, 거의 검은 Flat 앰비언트)에서 순수 검정으로 렌더된다.</item>
/// <item>알려진 결손 보정(<see cref="ApplyPartCorrections"/>) — MediumPart·InkWood 먹선 채점
/// 포인트 생성, MediumPart UsablePart 존들의 woodModifier 명시 연결, LongPart SawZone
/// requiredStrokes 통일. 전부 이식된 Play 사본에만 적용하고 원본 GongpoScene은 건드리지 않는다.</item>
/// <item>먹줄 데칼 참조 정리(<see cref="FixInkLineDecals"/>) — inkLineDecalPrefab을 프리팹 자산
/// 참조로 통일하고, 허공에 상시 렌더되던 Line 씬 인스턴스를 제거한다.</item>
/// <item>존 의존성 게이트 주입(<see cref="ApplyZoneGates"/>) — 부재 3종에
/// <see cref="PartZoneGate"/>를 붙이고 R 컨펌 의존 트리를 규칙으로 채운다.</item>
/// <item>공정 가이드 재배치(<see cref="ProcessGuideBuilder.BuildInto"/>) — 이식 루트를 지우면
/// 존에 붙어 있던 가이드 앵커도 사라지므로 이식 직후 다시 붙인다.</item>
/// <item>월드 스케일 적용(<see cref="ApplyWorldScale"/>) — 이식 루트를 <see cref="WorldScale"/>배로
/// 키워 큰 스케일로 저작된 Play 씬(플레이어 CharacterController Height 25 등)과 비율을 맞춘다.
/// 원본에서 이미 월드 크기로 저작된 직계 자식은 배율이 중복되지 않게 정규화한다.</item>
/// </list>
/// </summary>
static class PlayWorkshopBuilder
{
    const string PlayScenePath = "Assets/@Scenes/Play.unity";
    const string WoodworkingScenePath = "Assets/@Scenes/GongpoScene.unity";
    const string TempScenePath = "Assets/@Developers/RYU/Scenes/__PlayWorkshopImportTemp.unity";
    const string ImportRootName = "WorkshopImport";

    /// <summary>
    /// warehouse 동쪽 벽(x≈77) 바깥. 바닥 Plane이 y=13.05에 있으므로 그 위에 얹는다.
    /// GongpoScene 콘텐츠는 원점 주변 10×10 유닛 안에 모여 있어 오프셋 하나로 충분하다.
    /// </summary>
    static readonly Vector3 StagingPosition = new(120f, 13.1f, -40f);

    /// <summary>
    /// 이 중 하나라도 트리에 있으면 작업 오브젝트로 본다. WorkZone이 존 전 종류를 덮는다.
    /// <see cref="LegacyProcessCleanupTool"/>도 같은 목록으로 구식 공정 루트를 판별하므로
    /// 목록을 고치면 두 도구에 동시에 반영된다.
    /// </summary>
    internal static readonly System.Type[] WorkshopComponentTypes =
    {
        typeof(WorkZone),
        typeof(ChiselTool),
        typeof(SawTool),
        typeof(HammerTool),
        typeof(FlatPlaneTool),
        typeof(CurvedPlaneTool),
        typeof(AdzeTool),
        typeof(InkLineTool),
        typeof(VisualWoodModifier),
        typeof(AssemblyTarget),
        typeof(MaleSnapPoint),
        typeof(GongpoAssemblyManager),
        typeof(GongpoInstallationDirector)
    };

    /// <summary>
    /// 참조를 따라가더라도 절대 끌려오면 안 되는 루트. Play 씬에 이미 자기 몫이 있거나(플레이어,
    /// 라이트), 작업물이 아닌 연출용이다(NPC). 이쪽으로 향하는 참조는 끊기고 경고만 남는다.
    /// </summary>
    static readonly HashSet<string> ExcludedRoots = new()
    {
        "Player",
        "Main Camera",
        "Directional Light",
        "Canvas",
        "EventSystem",
        "XR Interaction Manager",
        "Environment",
        "iumi",
        "legendOldman"
    };

    /// <summary>
    /// 작업 컴포넌트가 없어도 이름이 일치하면 함께 이식하는 루트. 도구·부재가 얹혀 있는 작업대처럼
    /// 순수 배경 소품이라 컴포넌트 기준으로는 걸리지 않지만, 빠지면 작업물이 공중에 뜬다.
    /// </summary>
    static readonly HashSet<string> AdditionalRootNames = new()
    {
        "table",
        "tableSample"
    };

    [MenuItem("Tools/PROJECT 이음/Play 작업장 오브젝트 가져오기")]
    static void ImportFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "Play 작업장 오브젝트",
                $"GongpoScene의 작업 오브젝트를 Play 씬의 '{ImportRootName}' 아래로 가져옵니다.\n" +
                "기존 " + ImportRootName + "이 있으면 지우고 다시 가져옵니다.",
                "가져오기", "취소"))
            return;

        Build();
    }

    /// <summary>배치모드 진입점: -executeMethod PlayWorkshopBuilder.ImportFromBatchMode</summary>
    public static void ImportFromBatchMode() => Build();

    static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(WoodworkingScenePath) == null)
        {
            Debug.LogError("[PlayWorkshop] Play 씬 또는 GongpoScene을 찾지 못했습니다.");
            return;
        }

        // 열려 있는 씬의 미저장 변경을 지키는 쪽은 에디터 사용자 몫이다. 배치모드에는 물을 곳이 없다.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        AssetDatabase.DeleteAsset(TempScenePath);
        if (!AssetDatabase.CopyAsset(WoodworkingScenePath, TempScenePath))
        {
            Debug.LogError("[PlayWorkshop] GongpoScene 임시 복사에 실패했습니다.");
            return;
        }

        // 이미 열려 있던 Play 씬은 작업 후에도 열어 둔다. 에디터에서 메뉴로 실행하는 경우
        // 대개 Play 씬을 보면서 실행하므로, 그 화면을 임의로 닫지 않는다.
        var playWasLoaded = SceneManager.GetSceneByPath(PlayScenePath).isLoaded;

        Scene play = default;
        Scene source = default;
        try
        {
            play = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Additive);
            RemoveExistingImport(play);

            source = EditorSceneManager.OpenScene(TempScenePath, OpenSceneMode.Additive);
            var selected = SelectWorkshopRoots(source);
            if (selected.Count == 0)
            {
                Debug.LogWarning("[PlayWorkshop] GongpoScene에서 작업 오브젝트를 찾지 못했습니다.");
                return;
            }

            FollowReferences(source, selected);

            var importRoot = new GameObject(ImportRootName);
            SceneManager.MoveGameObjectToScene(importRoot, play);

            foreach (var root in selected)
                root.transform.SetParent(importRoot.transform, true);

            // 원래의 상대 배치를 유지한 채 전체를 맵 밖 스테이징 위치로 옮긴다.
            importRoot.transform.position = StagingPosition;

            FixMissingMaterials(importRoot);
            FixForwardPlusIncompatibleMaterials(importRoot);
            ApplyPartCorrections(importRoot);
            FixInkLineDecals(importRoot);
            ApplyZoneGates(importRoot);
            ProcessGuideBuilder.BuildInto(play);
            ApplyWorldScale(importRoot);

            EditorSceneManager.MarkSceneDirty(play);
            EditorSceneManager.SaveScene(play, PlayScenePath);

            Debug.Log($"[PlayWorkshop] 루트 {selected.Count}개를 '{ImportRootName}'({StagingPosition})로 가져왔습니다: " +
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

    static List<GameObject> SelectWorkshopRoots(Scene source)
    {
        var selected = new List<GameObject>();
        foreach (var root in source.GetRootGameObjects())
        {
            if (ExcludedRoots.Contains(root.name)) continue;
            if (HasWorkshopComponent(root) || AdditionalRootNames.Contains(root.name)) selected.Add(root);
        }

        return selected;
    }

    static bool HasWorkshopComponent(GameObject root)
    {
        for (var i = 0; i < WorkshopComponentTypes.Length; i++)
            if (root.GetComponentInChildren(WorkshopComponentTypes[i], true) != null)
                return true;

        return false;
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
                            $"[PlayWorkshop] {component.GetType().Name}.{property.name}이 제외 루트 " +
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

    /// <summary>이식본에 누락(null) 머티리얼 슬롯이 있으면 씬 인스턴스에서 대신 끼우는 재질.</summary>
    const string FallbackMaterialPath = "Assets/@Art/Props/Tools/darkwood.mat";

    /// <summary>
    /// 이식 트리의 렌더러를 순회해 누락 슬롯에 대체 재질을 끼운다. 프리팹이 물고 있던 guid가
    /// 프로젝트에 없으면 sharedMaterials에서 null로 보이므로 null 검사 하나로 누락까지 덮는다
    /// (ISSUE-031의 분홍 렌더 방지). sharedMaterials 배열 재대입은 씬 인스턴스 오버라이드로만
    /// 기록되어 애셋 원본은 그대로다.
    /// </summary>
    static void FixMissingMaterials(GameObject instance)
    {
        var fallback = AssetDatabase.LoadAssetAtPath<Material>(FallbackMaterialPath);
        if (fallback == null)
        {
            Debug.LogWarning($"[PlayWorkshop] 대체 재질 '{FallbackMaterialPath}'을 찾지 못해 누락 머티리얼 보정을 건너뜁니다.");
            return;
        }

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null) continue;

                materials[i] = fallback;
                changed = true;
            }

            if (!changed) continue;

            renderer.sharedMaterials = materials;
            Debug.Log($"[PlayWorkshop] '{renderer.name}'의 누락 머티리얼 슬롯을 '{FallbackMaterialPath}'로 채웠습니다.", renderer);
        }
    }

    // ---- Forward+ 비호환 머티리얼 교체 ----

    /// <summary>부재·공포 블록 머티리얼이 쓰는 팀원 소유 셰이더. Forward+/정점 광원 배리언트가 없다.</summary>
    const string IncompatibleShaderName = "Custom/WoodTriplanar";

    /// <summary>라이팅 배리언트를 보강한 RYU 소유 호환 사본. 원본 셰이더가 바뀌면 함께 갱신한다.</summary>
    const string CompatShaderPath = "Assets/@Developers/RYU/Woodworking/WoodTriplanarCompat.shader";

    /// <summary>호환 머티리얼 사본(*_Compat.mat)을 생성·갱신하는 폴더.</summary>
    const string CompatMaterialFolder = "Assets/@Developers/RYU/Woodworking";

    /// <summary>
    /// 이식 트리에서 <see cref="IncompatibleShaderName"/> 셰이더를 쓰는 머티리얼을 찾아, 같은
    /// 프로퍼티를 복사한 호환 셰이더 사본(<c>{원본 이름}_Compat.mat</c>)으로 슬롯을 바꿔 끼운다.
    ///
    /// 원본 셰이더는 추가 광원을 per-pixel Forward 키워드(_ADDITIONAL_LIGHTS)로만 지원해서,
    /// Forward+ 렌더러(PC_Renderer)에서는 _CLUSTER_LIGHT_LOOP만 켜지고 추가 광원 기여가 전부
    /// 사라진다. Play 씬은 주광 intensity 0에 거의 검은 Flat 앰비언트라, 실내 스팟·포인트 광원을
    /// 못 받는 이 셰이더의 부재들만 순수 검정 실루엣이 된다(GongpoScene은 주광 intensity 2 +
    /// 스카이박스 앰비언트라 정상). 원본 셰이더·머티리얼은 paaalop 소유라 수정하지 않고, 사본은
    /// 실행할 때마다 원본 프로퍼티를 다시 복사해 원본 튜닝 변경을 따라간다. 슬롯 교체는 씬 인스턴스
    /// 오버라이드로만 기록된다.
    /// </summary>
    static void FixForwardPlusIncompatibleMaterials(GameObject instance)
    {
        var compatShader = AssetDatabase.LoadAssetAtPath<Shader>(CompatShaderPath);
        if (compatShader == null)
        {
            Debug.LogWarning($"[PlayWorkshop] 호환 셰이더 '{CompatShaderPath}'를 찾지 못해 Forward+ 머티리얼 교체를 건너뜁니다.");
            return;
        }

        var compatByOriginal = new Dictionary<Material, Material>();
        var replacedSlots = 0;

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var original = materials[i];
                if (original == null || original.shader == null) continue;
                if (original.shader.name != IncompatibleShaderName) continue;

                if (!compatByOriginal.TryGetValue(original, out var compat))
                {
                    compat = GetOrCreateCompatMaterial(original, compatShader);
                    compatByOriginal.Add(original, compat);
                }

                if (compat == null) continue;

                materials[i] = compat;
                changed = true;
                replacedSlots++;
            }

            if (changed) renderer.sharedMaterials = materials;
        }

        if (replacedSlots > 0)
            Debug.Log(
                $"[PlayWorkshop] '{IncompatibleShaderName}' 머티리얼 {compatByOriginal.Count}종, 슬롯 {replacedSlots}개를 " +
                $"호환 사본으로 교체했습니다: {string.Join(", ", compatByOriginal.Keys.Select(m => m.name))}");
    }

    /// <summary>
    /// 원본 머티리얼의 호환 사본 자산을 돌려준다. 없으면 만들고, 있으면 셰이더·프로퍼티를 원본
    /// 기준으로 다시 맞춘다. 사본은 씬 임베드가 아닌 자산이어야 재이식·재저장에도 참조가 안정된다.
    /// </summary>
    static Material GetOrCreateCompatMaterial(Material original, Shader compatShader)
    {
        var originalPath = AssetDatabase.GetAssetPath(original);
        if (string.IsNullOrEmpty(originalPath))
        {
            Debug.LogWarning($"[PlayWorkshop] '{original.name}'은 자산 머티리얼이 아니라 호환 사본을 만들지 않습니다.", original);
            return null;
        }

        var compatPath = $"{CompatMaterialFolder}/{original.name}_Compat.mat";
        var compat = AssetDatabase.LoadAssetAtPath<Material>(compatPath);
        if (compat == null)
        {
            if (!AssetDatabase.IsValidFolder(CompatMaterialFolder))
            {
                Debug.LogWarning($"[PlayWorkshop] 폴더 '{CompatMaterialFolder}'가 없어 호환 사본을 만들지 못합니다.");
                return null;
            }

            compat = new Material(original);
            AssetDatabase.CreateAsset(compat, compatPath);
            Debug.Log($"[PlayWorkshop] 호환 머티리얼 '{compatPath}'를 생성했습니다 (원본: {originalPath}).");
        }

        // 원본이 머티리얼 배리언트면(예: Gongpo_Vertical → 부모 Gongpo_Horizontal) 부모 링크가
        // 셰이더 대입을 상속으로 되덮어써서 사본이 비호환 셰이더로 남는다. 부모를 끊어 평탄화한 뒤
        // 셰이더를 바꾸고, 상속받던 값까지 포함한 원본의 최종 프로퍼티를 로컬로 복사한다.
        compat.parent = null;
        compat.shader = compatShader;
        compat.CopyPropertiesFromMaterial(original);
        EditorUtility.SetDirty(compat);

        return compat;
    }

    // ---- 알려진 결손 보정 ----

    const string ShortPartRootName = "ShortPart";
    const string MediumPartRootName = "MediumPart";
    const string LongPartRootName = "LongPart";

    /// <summary>세 부재의 톱질 스트로크 통일 값 (R 컨펌). Short·Medium은 원본이 이미 10이다.</summary>
    const int RequiredSawStrokes = 10;

    /// <summary>
    /// ShortPart InkZone의 PointA/PointB 로컬 패턴 (GongpoScene e1cc282 실측):
    /// PointA (0.164, -0.5, +0.4537) / PointB (0.164, -0.5, -0.4537).
    /// y = -0.5는 존 로컬 바닥면(= 목재 윗면, InkZone이 목재 상면에 얹힌 판이므로),
    /// z = ±0.4537은 존 z 폭의 약 91%를 가로지르는 스팬이다. x는 절단선(SawZone 중심) 위치로,
    /// MediumPart에서는 InverseTransformPoint로 산출한다.
    /// </summary>
    const float InkPointLocalY = -0.5f;

    const float InkPointLocalZ = 0.45365837f;

    /// <summary>
    /// 원본 GongpoScene에 없는 값들을 이식된 Play 사본에서만 채운다. 대상과 수치는 전부 R 컨펌
    /// 범위다 — 여기서 범위를 넓히지 않는다.
    /// </summary>
    static void ApplyPartCorrections(GameObject importRoot)
    {
        FixMediumInkZonePoints(importRoot);
        FixStripInkZonePoints(importRoot);
        FixMediumZoneModifiers(importRoot);
        FixLongSawStrokes(importRoot);
        FixChiselTipRadius(importRoot);
    }

    /// <summary>
    /// GongpoScene 원본의 ChiselTool.tipCheckRadius(0.015)는 1:1 월드 값이라 ×20 Play 씬에서는
    /// 인체감 1mm가 되어 끌질 판정이 사실상 불가능하다. 코드 기본값(2)과 같은 값으로 보정한다 —
    /// 씬 직렬화 값이 코드 기본값보다 우선하므로 이식 사본에서 명시적으로 덮어야 한다.
    /// </summary>
    static void FixChiselTipRadius(GameObject importRoot)
    {
        const float worldTipRadius = 2f;
        foreach (var chisel in importRoot.GetComponentsInChildren<ChiselTool>(true))
        {
            var serialized = new SerializedObject(chisel);
            var radius = serialized.FindProperty("tipCheckRadius");
            if (radius == null || Mathf.Approximately(radius.floatValue, worldTipRadius)) continue;
            radius.floatValue = worldTipRadius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[PlayWorkshop] '{chisel.name}' tipCheckRadius를 {worldTipRadius}로 보정했습니다 (×20 월드 스케일).");
        }
    }

    /// <summary>
    /// MediumPart InkZone은 채점 포인트(pointA/pointB)가 비어 있어 먹선을 튕겨도
    /// <see cref="InkLineZone.CalculateDistanceError"/>가 float.MaxValue를 돌려 품질이 0으로
    /// 계산된다. ShortPart의 자식 포인트 패턴을 본떠 생성해 연결한다.
    /// </summary>
    static void FixMediumInkZonePoints(GameObject importRoot)
    {
        var part = importRoot.transform.Find(MediumPartRootName);
        if (part == null)
        {
            Debug.LogWarning($"[PlayWorkshop] '{MediumPartRootName}'을 찾지 못해 먹선 포인트 보정을 건너뜁니다.");
            return;
        }

        var ink = part.GetComponentInChildren<InkLineZone>(true);
        if (ink == null)
        {
            Debug.LogWarning($"[PlayWorkshop] '{MediumPartRootName}'에서 InkLineZone을 찾지 못했습니다.");
            return;
        }

        if (ink.pointA != null && ink.pointB != null) return;

        // 기준 절단선: 부재의 SawZone 중 InkZone 중심에 가장 가까운 것. MediumPart는 톱 존이
        // 둘(x≈+0.64 / -0.65)인데 먹선은 한 줄이므로 하나를 골라야 하고, 거리 기준이면 원본
        // 배치가 바뀌어도 결정적이다.
        SawZone nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var saw in part.GetComponentsInChildren<SawZone>(true))
        {
            var distance = Mathf.Abs(ink.transform.InverseTransformPoint(saw.transform.position).x);
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = saw;
        }

        if (nearest == null)
        {
            Debug.LogWarning($"[PlayWorkshop] '{MediumPartRootName}'에 SawZone이 없어 먹선 포인트를 만들 수 없습니다.");
            return;
        }

        var localX = ink.transform.InverseTransformPoint(nearest.transform.position).x;
        ink.pointA = CreateInkPoint(ink.transform, "PointA", new Vector3(localX, InkPointLocalY, InkPointLocalZ));
        ink.pointB = CreateInkPoint(ink.transform, "PointB", new Vector3(localX, InkPointLocalY, -InkPointLocalZ));
        EditorUtility.SetDirty(ink);

        Debug.Log(
            $"[PlayWorkshop] '{MediumPartRootName}' InkZone에 채점 포인트를 생성했습니다 " +
            $"(절단선 '{nearest.name}' x={localX:F3} 기준, ShortPart 패턴 y={InkPointLocalY}, z=±{InkPointLocalZ}).", ink);
    }

    static Transform CreateInkPoint(Transform inkZone, string pointName, Vector3 localPosition)
    {
        var point = new GameObject(pointName);
        point.transform.SetParent(inkZone, false);
        point.transform.localPosition = localPosition;
        return point.transform;
    }

    /// <summary>먹줄 존을 '가늘고 긴 띠'로 볼 최소 종횡비. 이 미만이면 선 방향을 판정하지 않는다.</summary>
    const float InkStripAspect = 3f;

    /// <summary>
    /// 채점 포인트가 존 박스 폭에서 차지하는 비율. ShortPart 실측(단위 박스에서 ±0.4537)을
    /// 임의 크기 박스에 적용할 수 있게 반폭 대비 비율로 환산한 값이다(약 0.907).
    /// </summary>
    const float InkPointSpanRatio = InkPointLocalZ / 0.5f;

    static readonly Vector3[] LocalAxes = { Vector3.right, Vector3.up, Vector3.forward };

    /// <summary>
    /// <see cref="FixMediumInkZonePoints"/>가 다루지 못한 나머지 먹줄 존의 채점 포인트를 채운다.
    /// 대상은 SawZone과 짝을 이루지 않는 '띠 모양' 존이다 — Play 씬에서는 InkWood의 InkZone 둘로,
    /// 부재가 아닌 먹매김 연습용 목재라 절단선 기준이 없다. 대신 존 자체가 그어야 할 선을
    /// 감싸는 가늘고 긴 띠(월드 20 × 2 × 2)로 저작되어 있어 존의 긴 축이 곧 먹줄 방향이다.
    ///
    /// 포인트 배치 규약은 <see cref="FixMediumInkZonePoints"/>와 같다 — 목재에 닿는 면(존의
    /// 아래쪽 면)에서 선 방향으로 존 폭의 약 91%를 가로지른다. 포인트가 비어 있으면
    /// <see cref="InkLineZone.CalculateDistanceError"/>가 float.MaxValue를 돌려 품질이 0이 되고,
    /// <see cref="ProcessGuideService"/>의 안내 라인도 폴백 경로로 떨어진다.
    /// </summary>
    static void FixStripInkZonePoints(GameObject importRoot)
    {
        foreach (var ink in importRoot.GetComponentsInChildren<InkLineZone>(true))
        {
            if (ink.pointA != null && ink.pointB != null) continue;

            if (!ink.TryGetComponent<BoxCollider>(out var box))
            {
                Debug.LogWarning($"[PlayWorkshop] '{ink.name}'에 BoxCollider가 없어 먹선 포인트를 만들 수 없습니다.", ink);
                continue;
            }

            var lossy = ink.transform.lossyScale;
            var world = new Vector3(
                box.size.x * Mathf.Abs(lossy.x),
                box.size.y * Mathf.Abs(lossy.y),
                box.size.z * Mathf.Abs(lossy.z));

            var lineAxis = 0;
            for (var i = 1; i < 3; i++)
                if (world[i] > world[lineAxis])
                    lineAxis = i;

            var second = -1;
            for (var i = 0; i < 3; i++)
                if (i != lineAxis && (second < 0 || world[i] > world[second]))
                    second = i;

            if (world[lineAxis] < world[second] * InkStripAspect)
            {
                Debug.LogWarning(
                    $"[PlayWorkshop] '{ink.name}'은 띠 모양이 아니어서(월드 {Format(world)}) 먹선 방향을 " +
                    "판정할 수 없습니다. 채점 포인트를 비워 둡니다 — GongpoScene에서 직접 배선하세요.", ink);
                continue;
            }

            // 목재에 닿는 면: 선 방향을 뺀 두 축 중 가장 수직인 축의 아래쪽. 존이 목재 윗면에
            // 얹혀 있으므로 '위쪽 바깥'의 반대가 접촉면이다. ProcessGuideService.SignedFaceAxis와
            // 같은 규약이라 안내 라인과 채점 포인트가 같은 면에 놓인다.
            var faceAxis = -1;
            var bestVertical = -1f;
            for (var i = 0; i < 3; i++)
            {
                if (i == lineAxis) continue;

                var vertical = Mathf.Abs(ink.transform.TransformDirection(LocalAxes[i]).normalized.y);
                if (faceAxis >= 0 && vertical <= bestVertical) continue;
                faceAxis = i;
                bestVertical = vertical;
            }

            var outward = LocalAxes[faceAxis];
            if (ink.transform.TransformDirection(outward).y < 0f) outward = -outward;

            var contact = box.center - outward * (box.size[faceAxis] * 0.5f);
            var span = LocalAxes[lineAxis] * (box.size[lineAxis] * 0.5f * InkPointSpanRatio);

            ink.pointA = CreateInkPoint(ink.transform, "PointA", contact + span);
            ink.pointB = CreateInkPoint(ink.transform, "PointB", contact - span);
            EditorUtility.SetDirty(ink);

            Debug.Log(
                $"[PlayWorkshop] '{ink.name}'에 채점 포인트를 생성했습니다 (띠 축 {"XYZ"[lineAxis]}, " +
                $"접촉면 -{"XYZ"[faceAxis]}, 로컬 {Format(contact + span)} / {Format(contact - span)}).", ink);
        }
    }

    // ---- 먹줄 데칼 참조 정리 ----

    /// <summary>먹줄 데칼 템플릿. 팀원(paaalop) 소유 자산이라 읽기만 한다.</summary>
    const string InkLineDecalPrefabPath = "Assets/@Prefabs/Line.prefab";

    /// <summary>
    /// <see cref="VisualWoodModifier.inkLineDecalPrefab"/>을 전부 프리팹 '자산' 참조로 통일하고,
    /// 이식 트리에 남은 <c>Line</c> 씬 인스턴스를 지운다.
    ///
    /// GongpoScene에서는 이 필드 대부분이 씬에 놓인 Line 인스턴스를 가리킨다. 그 인스턴스는
    /// <c>useWorldSpace = 1</c>에 프리팹에 박힌 좌표(약 (0.6, 1.36, -6.6))를 그대로 들고 있어,
    /// 부모가 어디로 가든 원점 근처 허공에 47cm짜리 검은 선으로 상시 렌더된다 — Play 씬에서
    /// 보이던 잔상이 이것이다. 그렇다고 인스턴스를 비활성화하면
    /// <see cref="VisualWoodModifier.CreateInkLine"/>의 <c>Instantiate</c> 사본도 꺼진 채 복제되어
    /// 먹줄이 아예 안 보인다. 그래서 참조를 자산으로 바꾼 뒤 인스턴스를 제거한다 —
    /// <c>Instantiate</c>는 자산 참조로도 동일하게 동작하고, 사본의 <c>useWorldSpace</c>는
    /// <c>CreateLineRendererDecal</c>이 false로 덮어쓴다.
    ///
    /// 미배선(null)이던 modifier도 같은 자산으로 채운다 — 비어 있으면 먹줄을 튕겼을 때
    /// CreateInkLine이 에러만 남기고 끝난다. 자산이지만 Line.prefab이 아닌 다른 것을 가리키는
    /// 경우는 팀원의 의도로 보고 건드리지 않는다.
    /// </summary>
    static void FixInkLineDecals(GameObject importRoot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InkLineDecalPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning(
                $"[PlayWorkshop] 먹줄 데칼 프리팹 '{InkLineDecalPrefabPath}'을 찾지 못해 참조 정리를 건너뜁니다. " +
                "씬 인스턴스도 그대로 둡니다.");
            return;
        }

        var replaced = 0;
        var filled = 0;
        foreach (var modifier in importRoot.GetComponentsInChildren<VisualWoodModifier>(true))
        {
            var current = modifier.inkLineDecalPrefab;
            if (current == prefab) continue;

            if (current == null) filled++;
            else if (current.scene.IsValid()) replaced++;
            else continue;

            modifier.inkLineDecalPrefab = prefab;
            EditorUtility.SetDirty(modifier);
        }

        Debug.Log(
            $"[PlayWorkshop] 먹줄 데칼 참조를 '{InkLineDecalPrefabPath}' 자산으로 통일했습니다 " +
            $"(씬 인스턴스 → 자산 {replaced}곳, 미배선 채움 {filled}곳).");

        RemoveInkLineDecalInstances(importRoot, prefab);
        LogRetainedProjectionVisuals(importRoot);
    }

    /// <summary>
    /// 제거 대상이 아닌 사영 표시를 로그로 남긴다. 이름이 비슷한 <c>InkPointDecal</c>을 잔상으로
    /// 오인해 지우면 먹줄 도구의 조준 표시가 사라지므로, 어떤 오브젝트가 왜 남았는지 남겨 둔다.
    /// </summary>
    static void LogRetainedProjectionVisuals(GameObject importRoot)
    {
        foreach (var tool in importRoot.GetComponentsInChildren<InkLineTool>(true))
        {
            if (tool.projectionVisual == null)
            {
                Debug.LogWarning($"[PlayWorkshop] '{tool.name}'의 projectionVisual이 비어 있습니다.", tool);
                continue;
            }

            Debug.Log(
                $"[PlayWorkshop] '{tool.projectionVisual.name}'은 '{tool.name}'.projectionVisual이 참조하는 " +
                "사영 표시라 제거 대상이 아닙니다 (InkLineTool이 Start에서 스스로 비활성화하므로 잔상도 아닙니다).",
                tool);
        }
    }

    /// <summary>
    /// 이식 트리에서 <see cref="InkLineDecalPrefabPath"/>의 씬 인스턴스를 지운다. 지우기 전에
    /// 이식 트리 전체를 훑어 아직 그 인스턴스를 가리키는 직렬화 참조가 있는지 확인한다 —
    /// 남아 있으면 지우지 않고 경고만 남긴다(끊긴 참조가 조용히 생기는 편이 더 나쁘다).
    ///
    /// <c>InkPointDecal</c>은 대상이 아니다. Line.prefab 인스턴스가 아니고
    /// <see cref="InkLineTool.projectionVisual"/>이 참조하는 사영 표시라, 지우면 먹줄 도구의
    /// 조준 표시가 사라진다. InkLineTool이 Start에서 스스로 비활성화하므로 잔상도 아니다.
    /// </summary>
    static void RemoveInkLineDecalInstances(GameObject importRoot, GameObject prefab)
    {
        var candidates = new List<GameObject>();
        foreach (var line in importRoot.GetComponentsInChildren<LineRenderer>(true))
        {
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(line.gameObject);
            if (instanceRoot == null || instanceRoot != line.gameObject) continue;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            if (source == null || AssetDatabase.GetAssetPath(source) != InkLineDecalPrefabPath) continue;

            if (!candidates.Contains(instanceRoot)) candidates.Add(instanceRoot);
        }

        if (candidates.Count == 0)
        {
            Debug.Log("[PlayWorkshop] 이식 트리에 남은 먹줄 데칼 씬 인스턴스가 없습니다.");
            return;
        }

        foreach (var candidate in candidates)
        {
            if (TryFindReferenceInto(importRoot, candidate, out var holder))
            {
                Debug.LogWarning(
                    $"[PlayWorkshop] '{candidate.name}'을 아직 '{holder.GetType().Name}'({holder.name})이 " +
                    "참조하고 있어 제거하지 않았습니다.", holder);
                continue;
            }

            Debug.Log(
                $"[PlayWorkshop] 허공에 남던 먹줄 데칼 씬 인스턴스 '{candidate.name}'을 제거했습니다 " +
                $"(useWorldSpace 고정 좌표로 상시 렌더되던 잔상). 실제 먹줄은 '{prefab.name}' 자산에서 복제됩니다.");
            Object.DestroyImmediate(candidate);
        }
    }

    /// <summary>
    /// <paramref name="scope"/> 안의 컴포넌트 중 <paramref name="subtree"/>(자신 또는 그 자손)를
    /// 가리키는 직렬화 참조가 있으면 그 컴포넌트를 돌려준다. 자기 자신의 컴포넌트는 제외한다.
    /// </summary>
    static bool TryFindReferenceInto(GameObject scope, GameObject subtree, out Component holder)
    {
        holder = null;

        foreach (var component in scope.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            if (component.transform.IsChildOf(subtree.transform)) continue;

            using var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                var target = property.objectReferenceValue switch
                {
                    GameObject go => go.transform,
                    Component c => c.transform,
                    _ => null
                };

                if (target == null || !target.IsChildOf(subtree.transform)) continue;

                holder = component;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// MediumPart UsablePart의 끌·대패·자귀 존들은 woodModifier가 비어 있다. 런타임에는
    /// <see cref="WorkZone"/>.Start의 GetComponentInParent 폴백이 채워 주지만, 같은 값을 씬에
    /// 명시해 인스펙터에서 결선이 보이게 한다. Short·LongPart는 원본이 이미 연결돼 있다.
    /// 대상을 끌·대패·자귀 존으로 한정하는 이유: 먹줄·톱 존의 빈 woodModifier까지 채우는 것은
    /// 컨펌 범위(UsablePart 존 11개) 밖이다.
    /// </summary>
    static void FixMediumZoneModifiers(GameObject importRoot)
    {
        var part = importRoot.transform.Find(MediumPartRootName);
        if (part == null) return; // FixMediumInkZonePoints에서 이미 경고했다.

        var count = 0;
        foreach (var zone in part.GetComponentsInChildren<WorkZone>(true))
        {
            if (zone.woodModifier != null) continue;
            if (zone is not (ChiselZone or PlaneZone or AdzeZone)) continue;

            // 런타임 폴백과 같은 규칙: 가장 가까운 조상의 VisualWoodModifier. MediumPart에서는
            // UsablePart 루트에 붙은 것이 잡힌다 — Short·LongPart의 기존 결선과 같은 대상이다.
            var modifier = zone.GetComponentInParent<VisualWoodModifier>(true);
            if (modifier == null)
            {
                Debug.LogWarning($"[PlayWorkshop] '{zone.name}'의 조상에서 VisualWoodModifier를 찾지 못했습니다.", zone);
                continue;
            }

            zone.woodModifier = modifier;
            EditorUtility.SetDirty(zone);
            count++;
        }

        if (count > 0)
            Debug.Log($"[PlayWorkshop] '{MediumPartRootName}' 존 {count}개의 woodModifier를 명시 연결했습니다.");
    }

    /// <summary>LongPart SawZone의 requiredStrokes가 원본에서 1로 남아 있어 한 번에 잘린다. 10으로 통일한다.</summary>
    static void FixLongSawStrokes(GameObject importRoot)
    {
        var part = importRoot.transform.Find(LongPartRootName);
        if (part == null)
        {
            Debug.LogWarning($"[PlayWorkshop] '{LongPartRootName}'을 찾지 못해 톱질 스트로크 보정을 건너뜁니다.");
            return;
        }

        foreach (var saw in part.GetComponentsInChildren<SawZone>(true))
        {
            if (saw.requiredStrokes == RequiredSawStrokes) continue;

            Debug.Log(
                $"[PlayWorkshop] '{LongPartRootName}/{saw.name}' requiredStrokes {saw.requiredStrokes} → {RequiredSawStrokes}.", saw);
            saw.requiredStrokes = RequiredSawStrokes;
            EditorUtility.SetDirty(saw);
        }
    }

    // ---- 존 의존성 게이트 ----

    /// <summary>
    /// R 컨펌 의존성 트리. 존 이름은 GongpoScene e1cc282에서 실측한 실명이다.
    /// R의 개념명과 실명이 다른 곳(전부 MediumPart, 개념명은 해당 존의 blendShapeName):
    ///   planeRoundLeft → CurvedPlaneZoneLeft / planeRoundRight → CurvedPlaneZoneRight /
    ///   planeLowLeft → CurvedPlaneZoneLowLeft / planeLowRight → CurvedPlaneZoneLowRight.
    /// 먹매김·톱질 존은 트리에 없다 — 공정 순서는 브릿지의 도구 잠금이 담당하므로 잠그지 않는다.
    /// </summary>
    static readonly (string partName, (string prerequisite, string[] dependents)[] rules)[] GateDefinitions =
    {
        (LongPartRootName, new (string, string[])[]
        {
            ("PlaneZoneTop", new[] { "ChiselZoneTop1", "ChiselZoneTop2", "ChiselZoneTop3", "ChiselZoneTop4" }),
            ("PlaneZoneTailHigh", new[] { "ChiselZoneTailHigh" }),
            ("adzeZoneHeadHigh", new[] { "ChiselZoneHeadHigh" }),
            ("PlaneZoneTailLow", new[] { "ChiselZoneTailLow" }),
            ("adzeZoneHeadLow", new[] { "ChiselZoneHeadLow" }),
            ("adzeZoneBottom", new[] { "ChiselZoneBottom1", "ChiselZoneBottom2" })
        }),
        (MediumPartRootName, new (string, string[])[]
        {
            ("adzeZoneTop", new[] { "PlaneZoneTop", "PlaneZoneLeft", "PlaneZoneRight" }),
            ("PlaneZoneTop", new[] { "ChiselZoneTop" }),
            ("PlaneZoneLeft", new[] { "CurvedPlaneZoneLeft" }),
            ("PlaneZoneRight", new[] { "CurvedPlaneZoneRight" }),
            ("CurvedPlaneZoneLowLeft", new[] { "ChiselZoneLowLeft" }),
            ("CurvedPlaneZoneLowRight", new[] { "ChiselZoneLowRight" })
        }),
        (ShortPartRootName, new (string, string[])[]
        {
            ("PlaneZoneTop", new[] { "ChiselZoneTop" }),
            ("PlaneZoneRight", new[] { "ChiselZoneRight" }),
            ("PlaneZoneLeft", new[] { "ChiselZoneLeft" })
        })
    };

    /// <summary>
    /// 부재 3종에 <see cref="PartZoneGate"/>를 붙이고 확정 의존성 트리를 규칙으로 주입한다.
    /// 이름이 실제 존과 어긋나면 여기서 경고를 남긴다 — 런타임 게이트도 같은 상황에서 경고 후
    /// 해당 규칙만 무시하므로, 빌드 로그에서 먼저 잡는 것이 목적이다.
    /// </summary>
    static void ApplyZoneGates(GameObject importRoot)
    {
        foreach (var (partName, ruleDefinitions) in GateDefinitions)
        {
            var part = importRoot.transform.Find(partName);
            if (part == null)
            {
                Debug.LogWarning($"[PlayWorkshop] '{partName}'을 찾지 못해 존 게이트를 붙이지 못했습니다.");
                continue;
            }

            var zones = part.GetComponentsInChildren<WorkZone>(true);
            var rules = new List<PartZoneGate.GateRule>();
            foreach (var (prerequisite, dependents) in ruleDefinitions)
            {
                WarnIfZoneMissing(zones, prerequisite, partName);
                foreach (var dependent in dependents)
                    WarnIfZoneMissing(zones, dependent, partName);

                rules.Add(new PartZoneGate.GateRule
                {
                    prerequisiteZoneName = prerequisite,
                    dependentZoneNames = dependents
                });
            }

            var gate = part.GetComponent<PartZoneGate>();
            if (gate == null) gate = part.gameObject.AddComponent<PartZoneGate>();
            gate.SetRules(rules);
            EditorUtility.SetDirty(gate);

            Debug.Log($"[PlayWorkshop] '{partName}'에 존 게이트 규칙 {rules.Count}개를 주입했습니다.");
        }
    }

    static void WarnIfZoneMissing(WorkZone[] zones, string zoneName, string partName)
    {
        foreach (var zone in zones)
            if (zone != null && string.Equals(
                    zone.name.Trim(), zoneName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return;

        Debug.LogWarning(
            $"[PlayWorkshop] '{partName}' 아래에 존 '{zoneName}'이 없습니다. " +
            "게이트가 런타임에 이 이름을 무시합니다 — GongpoScene에서 존 이름이 바뀌었는지 확인하세요.");
    }

    // ---- 월드 스케일 적용 ----

    /// <summary>
    /// Play 씬의 월드 배율 (R 컨펌). Play 씬은 플레이어 CharacterController Height가 25 등 월드
    /// 자체가 큰 스케일로 저작되어 있어, 1:1 기준으로 저작된 GongpoScene 이식물은 이 배율로
    /// 키워야 지형·플레이어와 비율이 맞는다. LongPart를 Play 씬에서 수동으로 (20,20,20)으로
    /// 키워 확인한 값이 기준이다.
    /// </summary>
    const float WorldScale = 20f;

    /// <summary>
    /// 직계 자식이 "원본에서 이미 월드 크기로 저작됐다"고 판정할 localScale 성분 하한.
    /// <see cref="WorldScale"/>에 5% 허용오차를 둔 값으로, 세 성분이 전부 이 값 이상이면 루트
    /// 배율과 중복되지 않게 <see cref="WorldScale"/>로 나눠 정규화한다(예: 20 → 1).
    /// </summary>
    const float WorldSizedThreshold = WorldScale * 0.95f;

    /// <summary>
    /// 이식 루트에 월드 배율을 적용한다. 루트 localScale을 <see cref="WorldScale"/>로 설정하면
    /// 자식들의 로컬 배치 간격도 함께 배율되어 원본의 상대 구도가 유지된다. 루트 position은
    /// 스케일과 무관하므로 <see cref="StagingPosition"/>은 변하지 않는다.
    ///
    /// 멱등성: 재이식은 원본 GongpoScene 복사에서 다시 시작하므로 배율이 누적될 일이 없고,
    /// 원본 쪽에서 자식이 1로 저작됐든 20으로 저작됐든(정규화가 흡수) 결과 lossyScale은
    /// 일관되게 <see cref="WorldScale"/>이 된다. 마지막 검증 로그가 이를 실측으로 남긴다.
    /// </summary>
    static void ApplyWorldScale(GameObject importRoot)
    {
        // 1. 이미 월드 크기로 저작된 직계 자식 정규화. 현재 GongpoScene 원본은 부재 3종 전부
        // localScale (1,1,1)이라 해당 없음이 정상이지만, 팀원이 원본을 월드 크기로 다시 저작해도
        // 이 규칙이 이중 배율을 막는다.
        foreach (Transform child in importRoot.transform)
        {
            var scale = child.localScale;
            if (scale.x < WorldSizedThreshold || scale.y < WorldSizedThreshold || scale.z < WorldSizedThreshold)
            {
                Debug.Log(
                    $"[PlayWorkshop] '{child.name}' localScale {Format(scale)} — 1:1 저작으로 판정, 루트 배율만 적용합니다.",
                    child);
                continue;
            }

            child.localScale = scale / WorldScale;
            Debug.Log(
                $"[PlayWorkshop] '{child.name}'은 원본에서 이미 월드 크기(localScale {Format(scale)})로 저작되어 " +
                $"{WorldScale}로 나눠 {Format(child.localScale)}로 정규화했습니다 — 루트 배율과의 중복을 방지합니다.",
                child);
        }

        // 2. 루트 배율 적용.
        importRoot.transform.localScale = Vector3.one * WorldScale;
        Debug.Log(
            $"[PlayWorkshop] '{ImportRootName}' 루트 localScale을 ({WorldScale}, {WorldScale}, {WorldScale})로 " +
            "설정했습니다. 자식 로컬 배치 간격도 함께 배율되어 상대 구도가 유지됩니다.");

        // 3. 검증: 직계 자식의 결과 lossyScale을 실측으로 남긴다. 정규화를 지나고도 성분이
        // WorldScale²에 가까우면 이중 배율이므로 경고한다. 1이 아닌 저작 비율(예: 1.5)은
        // WorldScale배로 남는 것이 의도된 결과라 경고 대상이 아니다.
        var doubleScaledThreshold = WorldScale * WorldSizedThreshold;
        var lines = new List<string>();
        foreach (Transform child in importRoot.transform)
        {
            var lossy = child.lossyScale;
            lines.Add($"  - '{child.name}' lossyScale {Format(lossy)}");

            if (lossy.x >= doubleScaledThreshold && lossy.y >= doubleScaledThreshold && lossy.z >= doubleScaledThreshold)
                Debug.LogWarning(
                    $"[PlayWorkshop] '{child.name}'의 lossyScale {Format(lossy)}이 배율 중복으로 의심됩니다. " +
                    "정규화 판정을 확인하세요.", child);
        }

        Debug.Log($"[PlayWorkshop] 월드 스케일 적용 결과 (기준 {WorldScale}):\n{string.Join("\n", lines)}");
    }

    static string Format(Vector3 value) => $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
}
