using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 클립보드 퀘스트보드 프리팹을 만든다. Blender에서 뽑은 Clipboard.fbx에 종이 크기의 월드 캔버스와
/// <see cref="QuestBoardView"/> 배선을 얹는다.
///
/// 손으로 조립하지 않는 이유는 다른 빌더들과 같다 — 캔버스 크기·표면 오프셋·텍스트 계층과 뷰의
/// 다섯 참조가 인스펙터에만 남으면 프리팹을 다시 만들 때마다 눈으로 복원해야 한다. 여기가 그
/// 배선의 유일한 기록이다.
///
/// 캔버스 방향은 하드코딩하지 않는다. FBX 축 변환(Blender Z-up → Unity Y-up)이 내보내기 옵션에
/// 따라 달라지므로, 종이 메시의 가장 얇은 축을 법선으로 삼고 클립 방향을 위로 삼아 계산한다.
/// 내보내기 방식이 바뀌어도 다시 실행하면 맞는 방향이 나온다.
/// </summary>
static class QuestBoardBuilder
{
    const string FbxPath = "Assets/@Developers/RYU/Models/Clipboard.fbx";
    const string MaterialDirectory = "Assets/@Developers/RYU/Models/Materials";
    const string PrefabPath = "Assets/@Developers/RYU/Prefabs/QuestBoard.prefab";
    // FixedUI의 'Giants-Bold SDF.asset'(정적 베이크)은 아틀라스 공간 부족으로 한글 30자('이','먹',
    // '김','업' 등)가 탈락해 있어 쓰지 않는다. TTF에서 동적 에셋을 만들어 두고 그걸 물린다.
    const string SourceFontPath = "Assets/@Developers/RYU/Quest/UI/Fonts/Giants-Bold.ttf";
    const string DynamicFontPath = "Assets/@Developers/RYU/Quest/UI/Fonts/Giants-Bold Dynamic SDF.asset";

    /// <summary>종이 한 변(m). Blender 쪽 paper_export.py의 PW와 같은 값이어야 한다.</summary>
    const float PaperSize = 0.27f;

    /// <summary>캔버스 스케일. 270pt 캔버스가 0.27m 종이가 되도록 1pt = 1mm.</summary>
    const float CanvasScale = 0.001f;

    /// <summary>종이 표면에서 띄우는 거리(m). z-fighting 방지.</summary>
    const float SurfaceOffset = 0.002f;

    static readonly Color Ink = new(0.17f, 0.13f, 0.10f);

    [MenuItem("Tools/PROJECT 이음/퀘스트보드 프리팹 생성")]
    static void BuildFromMenu()
    {
        if (!Application.isBatchMode &&
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog(
                "퀘스트보드 프리팹",
                "기존 QuestBoard.prefab을 다시 만들까요?\n씬 인스턴스의 오버라이드는 유지됩니다.",
                "다시 만들기", "취소"))
            return;

        Build();
    }

    /// <summary>배치모드 진입점: -executeMethod QuestBoardBuilder.BuildFromBatchMode</summary>
    public static void BuildFromBatchMode() => Build();

    static void Build()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogError(
                $"[QuestBoard] '{FbxPath}'를 찾지 못했습니다. Blender에서 내보낸 FBX가 아직 " +
                "임포트되지 않았으면 에디터에 포커스를 주어 임포트한 뒤 다시 실행하십시오.");
            return;
        }

        RemapMaterials();

        var root = new GameObject("QuestBoard");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "Clipboard";

            var canvas = BuildCanvas(root.transform, instance.transform,
                out var header, out var list, out var rowTemplate, out var footer);

            var view = root.AddComponent<QuestBoardView>();
            Wire(view, header, list, rowTemplate, footer);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var success);
            if (!success || saved == null)
            {
                Debug.LogError($"[QuestBoard] 프리팹 저장에 실패했습니다: {PrefabPath}");
                return;
            }

            Debug.Log(
                $"[QuestBoard] 생성 완료: {PrefabPath}\n" +
                "사용법: 씬에 두 개 놓고 한쪽 QuestBoardView.mode를 TaskList로 바꾸면 " +
                "현재 진행/해야 할 일 한 쌍이 된다. " +
                $"캔버스 월드 스케일 {canvas.lossyScale.x:F4} (0.001 근처가 정상).");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---- 머티리얼 ----

    /// <summary>
    /// FBX 내장 머티리얼을 우리 URP 자산으로 리매핑한다. Blender가 심은 Principled 값이 임포터를
    /// 어떻게 통과하는지에 기대지 않고, 프로젝트가 소유한 머티리얼 세 개를 항상 쓴다.
    /// </summary>
    /// <summary>
    /// TTF에서 동적(Dynamic) TMP 폰트 에셋을 만들어 재사용한다. 동적 모드는 표시할 글자를 그때그때
    /// 아틀라스에 추가하므로, 정적 베이크처럼 누락 글자가 생기지 않고 문안이 바뀌어도 재베이크가
    /// 필요 없다.
    /// </summary>
    static TMP_FontAsset LoadOrCreateDynamicFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DynamicFontPath);
        if (existing != null) return existing;

        var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null) return null;

        var font = TMP_FontAsset.CreateFontAsset(
            source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic);
        if (font == null) return null;

        font.name = System.IO.Path.GetFileNameWithoutExtension(DynamicFontPath);
        AssetDatabase.CreateAsset(font, DynamicFontPath);

        // 머티리얼과 아틀라스 텍스처는 별도 오브젝트라 같은 에셋 파일에 붙여야 참조가 살아남는다.
        font.material.name = font.name + " Material";
        font.atlasTexture.name = font.name + " Atlas";
        AssetDatabase.AddObjectToAsset(font.material, font);
        AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
        AssetDatabase.SaveAssets();

        Debug.Log($"[QuestBoard] 동적 폰트 에셋을 생성했습니다: {DynamicFontPath}");
        return font;
    }

    static void RemapMaterials()
    {
        if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer) return;

        if (!AssetDatabase.IsValidFolder(MaterialDirectory))
            AssetDatabase.CreateFolder("Assets/@Developers/RYU/Models", "Materials");

        var changed = false;
        // 텍스처가 있으면 베이스 컬러는 흰색으로 두어 텍스처 색을 그대로 쓴다. 텍스처 자체가
        // 이미 목표 색(황갈 MDF·미색 한지)으로 구워져 있다.
        changed |= Remap(importer, "MDF", new Color(0.55f, 0.36f, 0.20f), 0f, 0.1f,
            "Assets/@Developers/RYU/Models/Textures/ClipboardMDF.png");
        changed |= Remap(importer, "Chrome", new Color(0.90f, 0.90f, 0.92f), 1f, 0.75f);
        changed |= Remap(importer, "Paper", new Color(0.93f, 0.91f, 0.86f), 0f, 0.05f,
            "Assets/@Developers/RYU/Models/Textures/ClipboardPaper.png");

        if (changed) importer.SaveAndReimport();
    }

    static bool Remap(
        ModelImporter importer,
        string name,
        Color color,
        float metallic,
        float smoothness,
        string texturePath = null)
    {
        var path = $"{MaterialDirectory}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[QuestBoard] URP Lit 셰이더를 찾지 못해 머티리얼 리매핑을 건너뜁니다.");
                return false;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        var texture = string.IsNullOrEmpty(texturePath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", texture != null ? Color.white : color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);

        var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
        var remaps = importer.GetExternalObjectMap();
        if (remaps.TryGetValue(identifier, out var existing) && existing == material) return false;

        importer.AddRemap(identifier, material);
        return true;
    }

    // ---- 캔버스 ----

    static Transform BuildCanvas(
        Transform root,
        Transform model,
        out TMP_Text header,
        out RectTransform list,
        out TMP_Text rowTemplate,
        out TMP_Text footer)
    {
        var anchor = FindDeep(model, "PaperAnchor") ?? FindDeep(model, "Paper") ?? model;

        var canvasGo = new GameObject("PaperCanvas", typeof(RectTransform), typeof(Canvas));
        var canvasTransform = (RectTransform)canvasGo.transform;
        canvasTransform.SetParent(anchor, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // GraphicRaycaster는 일부러 뺀다. 읽기 전용 게시판이라 포인터 대상이 되면 안 된다.

        canvasTransform.sizeDelta = new Vector2(PaperSize / CanvasScale, PaperSize / CanvasScale);
        canvasTransform.localScale = Vector3.one * CanvasScale;

        OrientToPaper(canvasTransform, model);

        var font = LoadOrCreateDynamicFont();
        if (font == null)
            Debug.LogWarning($"[QuestBoard] 폰트 '{SourceFontPath}'를 찾지 못해 TMP 기본 폰트를 씁니다.");

        // 헤더 — 상단 고정
        header = MakeText(canvasTransform, "Header", font, 30f, TextAlignmentOptions.Center, Ink);
        var headerRect = header.rectTransform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(14f, -56f);
        headerRect.offsetMax = new Vector2(-14f, -12f);

        // 본문 목록 — 가운데, 세로 레이아웃 컨테이너
        var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list = (RectTransform)listGo.transform;
        list.SetParent(canvasTransform, false);
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.one;
        list.offsetMin = new Vector2(18f, 34f);
        list.offsetMax = new Vector2(-18f, -62f);

        var layout = listGo.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 7f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 줄 원본 — 비활성. 뷰가 필요한 만큼 복제한다.
        rowTemplate = MakeText(list, "RowTemplate", font, 19f, TextAlignmentOptions.Left, Ink);
        rowTemplate.textWrappingMode = TextWrappingModes.Normal;
        rowTemplate.gameObject.SetActive(false);

        // 푸터 — 하단 상태줄
        footer = MakeText(canvasTransform, "Footer", font, 15f, TextAlignmentOptions.Right,
            new Color(Ink.r, Ink.g, Ink.b, 0.6f));
        var footerRect = footer.rectTransform;
        footerRect.anchorMin = Vector2.zero;
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.offsetMin = new Vector2(14f, 10f);
        footerRect.offsetMax = new Vector2(-14f, 32f);

        return canvasTransform;
    }

    static TMP_Text MakeText(
        Transform parent, string name, TMP_FontAsset font, float size,
        TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// 종이 메시의 가장 얇은 로컬 축을 법선으로 삼아 캔버스를 종이 표면에 맞춘다. 위쪽은 보드
    /// 중심에서 클립 판으로 향하는 방향이다 — 클립이 있는 쪽이 게시물의 머리이기 때문이다.
    /// </summary>
    static void OrientToPaper(Transform canvas, Transform model)
    {
        var paper = FindDeep(model, "Paper");
        var board = FindDeep(model, "Board");
        var clip = FindDeep(model, "ClipPlate");

        if (paper == null || board == null)
        {
            Debug.LogWarning("[QuestBoard] Paper 또는 Board를 찾지 못해 캔버스 방향을 계산하지 못했습니다.");
            return;
        }

        var mesh = paper.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null)
        {
            Debug.LogWarning("[QuestBoard] Paper 메시가 없어 캔버스 방향을 계산하지 못했습니다.");
            return;
        }

        var size = mesh.bounds.size;
        var localNormal = size.x <= size.y && size.x <= size.z ? Vector3.right
            : size.y <= size.z ? Vector3.up
            : Vector3.forward;

        var normal = paper.TransformDirection(localNormal).normalized;

        // 종이는 보드 표면에 얹혀 있으므로 보드 중심 → 종이 중심이 대략 바깥 방향이다.
        var outward = paper.position - board.position;
        if (outward.sqrMagnitude > 1e-10f && Vector3.Dot(normal, outward) < 0f)
            normal = -normal;

        var upHint = clip != null ? clip.position - board.position : Vector3.up;
        var up = Vector3.ProjectOnPlane(upHint, normal);
        if (up.sqrMagnitude < 1e-10f) up = Vector3.ProjectOnPlane(Vector3.up, normal);

        // 캔버스 UI는 자기 -Z 쪽에서 읽힌다. 앞면(법선)에서 보이려면 +Z를 법선 반대로 둔다.
        canvas.rotation = Quaternion.LookRotation(-normal, up.normalized);
        canvas.position = paper.position + normal * SurfaceOffset;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (var i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    // ---- 뷰 배선 ----

    static void Wire(
        QuestBoardView view, TMP_Text header, RectTransform list, TMP_Text rowTemplate, TMP_Text footer)
    {
        var serialized = new SerializedObject(view);
        serialized.FindProperty("header").objectReferenceValue = header;
        serialized.FindProperty("listContainer").objectReferenceValue = list;
        serialized.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
        serialized.FindProperty("footer").objectReferenceValue = footer;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
