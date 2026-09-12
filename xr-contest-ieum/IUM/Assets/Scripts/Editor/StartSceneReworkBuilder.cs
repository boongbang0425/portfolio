using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// StartScene(메인 화면) 개편을 씬에 반영한다. 좌측 4버튼 메뉴를 제거하고, 중앙 모니터 앞에 원형
/// 플레이 버튼(▶)과 "처음부터"·"이어하기" 2옵션 메뉴를 세우며, 중앙 모니터 캔버스에 프롤로그 영상
/// 표면(RawImage)과 건너뛰기 게이지를, 화면 위에는 저장 덮어쓰기 확인 모달(ConfirmCanvas)을,
/// 좌·우 패널 캔버스에는 플레이스홀더 이미지를 넣는다. 시각 스타일은 구 UXML(StartMenu.uss)의
/// 유리 팔레트를 따른다.
///
/// 씬은 공용 자산이라 손으로 고치지 않고 빌더로만 반영한다(PlayWorkshopBuilder 선례). 재실행하면
/// <see cref="GeneratedPrefix"/>로 시작하는 이전 생성물을 지우고 다시 만들어 멱등이다 — 단, 좌측
/// 메뉴 제거와 EventSystem 이동은 되돌리지 않는 일방향 정리다.
///
/// 씬의 세 모니터 루트는 전부 개체_1이라 이름으로 못 찾는다. 좌측은 FixedUIStartMenuAdapter가 붙은
/// 캔버스로, 중앙·우측은 빈 월드 캔버스의 크기(909x506 / 495x276)로 판별한다.
///
/// 중앙·우측 캔버스는 앞면(+Z)이 플레이어 쪽을 향해 uGUI가 좌우 반전으로 보인다(좌측만 반대).
/// 그래서 캔버스 자체는 건드리지 않고, 생성한 컨테이너에 Y축 180도 회전을 넣어 보정한다.
///
/// 루트 아래 자식들의 배치는 전부 <b>월드 세터</b>(<c>Transform.position</c>/<c>rotation</c>)로 넣는다.
/// 모니터·플레이어 좌표에서 계산한 목표 지점이므로 루트 포즈와 무관해야 한다 — 루트를 손으로 옮겨도
/// 재실행하면 자식이 의도한 월드 지점으로 되돌아온다. 반대로 루트 포즈 자체와 영상 표면의 로컬 z는
/// 손 조정값이라 <see cref="RemovePrevious"/>가 삭제 직전에 읽어 새 생성물에 승계한다.
/// </summary>
static class StartSceneReworkBuilder
{
    const string ScenePath = "Assets/Scenes/StartScene.unity";
    const string RootName = "StartRework";
    const string GeneratedPrefix = "StartRework";
    const string PlayIconPath =
        "Assets/UI/Pause/FixedUI/Icons/play_arrow_48dp_000000_FILL0_wght400_GRAD0_opsz48.png";
    const string KoreanFontPath = "Assets/Developers/RYU/Quest/UI/Fonts/Giants-Bold.ttf";
    const int UILayer = 5;

    /// <summary>모니터 캔버스 크기 판별 허용 오차. 우측 캔버스 높이가 275.96처럼 미세하게 어긋난다.</summary>
    const float SizeTolerance = 2f;

    static readonly Vector2 CenterCanvasSize = new(909f, 506f);
    static readonly Vector2 RightCanvasSize = new(495f, 276f);

    // ── 손 조정값의 기본치: 씬에 이전 생성물이 없어 승계할 대상이 없을 때만 쓴다.

    /// <summary>StartRework 루트 위치의 기본값. <b>R 확정 포즈</b>다.</summary>
    static readonly Vector3 DefaultRootPosition = new(0f, 0.093f, -0.538f);

    /// <summary>StartRework 루트 회전의 기본값. <b>R 확정 포즈</b>다.</summary>
    static readonly Quaternion DefaultRootRotation = Quaternion.identity;

    /// <summary>StartRework 루트 스케일의 기본값. <b>R 확정 포즈</b>다.</summary>
    static readonly Vector3 DefaultRootScale = Vector3.one;

    /// <summary>
    /// 영상 RawImage(Video)의 로컬 z(px) 기본값. R이 씬에서 넣은 조정값으로, 화면 표면보다 앞으로
    /// 당겨 z-fighting을 피한다(캔버스 스케일 0.001이라 -19px = 월드 19mm).
    /// </summary>
    const float DefaultVideoLocalZ = -19f;

    // ── FixedUI 유리 팔레트: 구 UXML 디자인(Assets/UI/Start/StartMenu.uss)의 토큰을 uGUI 상수로
    //    옮긴 것. 출처 셀렉터 — .menu-card(스모키 유리)·.menu-button(흰 유리)·:active(악센트)·
    //    .menu-button--quiet·.modal-layer(모달 암막)·전경 rgb(25,25,25) α0.86.
    static readonly Color PanelGlass = new(72f / 255f, 70f / 255f, 82f / 255f, 0.66f);
    static readonly Color Accent = new(125f / 255f, 199f / 255f, 255f / 255f, 1f);
    static readonly Color InkText = new(25f / 255f, 25f / 255f, 25f / 255f, 0.86f);
    static readonly Color InkTextQuiet = new(25f / 255f, 25f / 255f, 25f / 255f, 0.6f);
    static readonly Color ModalBackdrop = new(4f / 255f, 8f / 255f, 13f / 255f, 0.78f);

    /// <summary>
    /// 흰 유리 버튼(.menu-button): 평소 α0.9 → hover 흰색 → 눌림 악센트 → 비활성 α0.24. 유리 α를
    /// ColorBlock이 쥐므로 대상 Image의 색은 흰색으로 둔다(최종색 = 이미지색 × 블록색).
    /// </summary>
    static ColorBlock GlassButtonColors => new()
    {
        normalColor = new Color(1f, 1f, 1f, 0.9f),
        highlightedColor = Color.white,
        pressedColor = Accent,
        // 크로스헤어 픽킹이 button.Select()를 부르므로 selected가 hover처럼 남지 않게 평소 색과 같게.
        selectedColor = new Color(1f, 1f, 1f, 0.9f),
        disabledColor = new Color(1f, 1f, 1f, 0.24f),
        colorMultiplier = 1f,
        fadeDuration = 0.12f
    };

    /// <summary>차분한 유리 버튼(.menu-button--quiet): 취소처럼 덜 권하는 동작에 쓴다.</summary>
    static ColorBlock QuietButtonColors => new()
    {
        normalColor = new Color(1f, 1f, 1f, 0.55f),
        highlightedColor = new Color(1f, 1f, 1f, 0.8f),
        pressedColor = Accent,
        selectedColor = new Color(1f, 1f, 1f, 0.55f),
        disabledColor = new Color(1f, 1f, 1f, 0.24f),
        colorMultiplier = 1f,
        fadeDuration = 0.12f
    };

    [MenuItem("Tools/PROJECT 이음/시작 화면 개편 적용")]
    static void ReworkFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "시작 화면 개편",
                "StartScene의 좌측 4버튼 메뉴를 제거하고 원형 플레이 버튼과 모니터 영상 표면을 배치합니다.\n" +
                "재실행하면 이전 생성물을 지우고 다시 만듭니다.",
                "적용", "취소"))
            return;

        Build();
    }

    /// <summary>배치모드 진입점: -executeMethod StartSceneReworkBuilder.ReworkFromBatchMode</summary>
    public static void ReworkFromBatchMode() => Build();

    static void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[StartRework] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"[StartRework] 씬을 찾지 못했습니다: {ScenePath}");
            return;
        }

        // 열려 있는 씬의 미저장 변경을 지키는 쪽은 에디터 사용자 몫이다. 배치모드에는 물을 곳이 없다.
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var adapter = Object.FindFirstObjectByType<FixedUIStartMenuAdapter>(FindObjectsInactive.Include);
        if (adapter == null || adapter.GetComponent<Canvas>() == null)
        {
            Debug.LogError("[StartRework] FixedUIStartMenuAdapter가 붙은 좌측 캔버스를 찾지 못했습니다.");
            return;
        }

        var leftCanvas = adapter.GetComponent<Canvas>();

        var player = Object.FindFirstObjectByType<Player>(FindObjectsInactive.Include);
        if (player == null)
        {
            Debug.LogError("[StartRework] 씬에서 Player를 찾지 못했습니다.");
            return;
        }

        // 멱등성: 이전 생성물을 먼저 지워야 빈 캔버스 판별과 재생성이 어긋나지 않는다.
        // 지우기 전에 손 조정값(루트 포즈·영상 표면 로컬 z)을 읽어 새 생성물에 승계한다.
        var preserved = RemovePrevious();

        if (!FindPanelCanvases(leftCanvas, out var centerCanvas, out var rightCanvas))
            return;

        var playerPos = player.transform.position;
        var font = LoadKoreanFont();

        MoveEventSystemToRoot();
        RemoveLeftMenu(leftCanvas);
        SuppressLegacyUxmlMenu();

        AddPlaceholder(leftCanvas, playerPos, font, 16f);
        AddPlaceholder(rightCanvas, playerPos, font, 12f);

        var video = BuildVideoSurface(centerCanvas, playerPos, font, preserved.VideoLocalZ);
        BuildPlayMenuAndControllers(centerCanvas, playerPos, font, video, preserved);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("[StartRework] 씬 저장에 실패했습니다.");
            return;
        }

        Debug.Log("[StartRework] 시작 화면 개편을 반영했습니다: 좌측 메뉴 제거, 원형 플레이 버튼·" +
                  "모니터 영상 표면·플레이스홀더 배치 완료.");
    }

    // ── 탐색·정리 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 이름이 <see cref="GeneratedPrefix"/>로 시작하는 이전 생성물을 전부 지운다. 지우기 전에 씬에서
    /// 손으로 맞춘 값(루트 포즈·영상 표면 로컬 z)을 읽어 돌려주며, 호출자가 이를 새 생성물에 승계해
    /// 재실행해도 조정이 살아남게 한다. 이전 생성물이 없으면 기본 상수를 돌려준다.
    /// </summary>
    static PreservedPose RemovePrevious()
    {
        var rootPosition = DefaultRootPosition;
        var rootRotation = DefaultRootRotation;
        var rootScale = DefaultRootScale;
        var videoLocalZ = DefaultVideoLocalZ;
        var rootCaptured = false;
        var videoCaptured = false;

        // 씬 루트의 StartRework 루트.
        for (var i = 0; i < 8; i++)
        {
            var previous = GameObject.Find(RootName);
            if (previous == null) break;

            // 루트 포즈는 R이 씬에서 확정한 값이라 재생성 후에도 그대로 복원한다.
            // 중복 루트가 있으면 처음 찾은 것만 승계 대상으로 삼는다.
            if (!rootCaptured)
            {
                var previousTransform = previous.transform;
                rootPosition = previousTransform.position;
                rootRotation = previousTransform.rotation;
                rootScale = previousTransform.localScale;
                rootCaptured = true;
            }

            Object.DestroyImmediate(previous);
        }

        // 각 캔버스 밑에 심었던 컨테이너들.
        foreach (var canvas in Object.FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            for (var i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                var child = canvas.transform.GetChild(i);
                if (!child.name.StartsWith(GeneratedPrefix)) continue;

                // 영상 RawImage의 로컬 z도 손 조정값이라 함께 승계한다.
                if (!videoCaptured)
                {
                    var video = child.Find("Video");
                    if (video != null)
                    {
                        videoLocalZ = video.localPosition.z;
                        videoCaptured = true;
                    }
                }

                Object.DestroyImmediate(child.gameObject);
            }
        }

        return new PreservedPose(rootPosition, rootRotation, rootScale, videoLocalZ);
    }

    /// <summary>
    /// 중앙(909x506)·우측(495x276) 모니터 캔버스를 크기로 판별한다. 자식이 있는 캔버스는 후보에서
    /// 뺀다 — 원본 상태에서 두 캔버스는 비어 있고, 생성물은 <see cref="RemovePrevious"/>가 지웠다.
    /// </summary>
    static bool FindPanelCanvases(Canvas leftCanvas, out Canvas center, out Canvas right)
    {
        center = null;
        right = null;

        foreach (var canvas in Object.FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == leftCanvas || canvas.renderMode != RenderMode.WorldSpace) continue;
            if (canvas.transform.childCount > 0) continue;

            var size = ((RectTransform)canvas.transform).sizeDelta;
            if (Matches(size, CenterCanvasSize)) center = canvas;
            else if (Matches(size, RightCanvasSize)) right = canvas;
        }

        if (center == null || right == null)
        {
            Debug.LogError("[StartRework] 중앙 또는 우측 모니터 캔버스를 찾지 못했습니다. " +
                           $"(중앙 {CenterCanvasSize}, 우측 {RightCanvasSize} 크기의 빈 월드 캔버스가 필요합니다)");
            return false;
        }

        return true;
    }

    static bool Matches(Vector2 size, Vector2 expected) =>
        Mathf.Abs(size.x - expected.x) <= SizeTolerance &&
        Mathf.Abs(size.y - expected.y) <= SizeTolerance;

    /// <summary>
    /// EventSystem이 좌측 메뉴 Backdrop의 자식이라, 메뉴를 지우기 전에 씬 루트로 옮긴다.
    /// 원본은 FBX 좌표계에 딸려 이상한 지역 좌표·스케일을 갖고 있으므로 함께 초기화한다
    /// (EventSystem 동작에 트랜스폼은 무관하다).
    /// </summary>
    static void MoveEventSystemToRoot()
    {
        var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (eventSystem == null)
        {
            Debug.LogWarning("[StartRework] EventSystem을 찾지 못했습니다. UI 입력이 동작하지 않을 수 있습니다.");
            return;
        }

        if (eventSystem.transform.parent == null) return;

        eventSystem.transform.SetParent(null, false);
        eventSystem.transform.localPosition = Vector3.zero;
        eventSystem.transform.localRotation = Quaternion.identity;
        eventSystem.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 구 UXML 시작 메뉴(Start UI Bridge의 StartMenuController)를 외부 메뉴 모드로 강제해 이중
    /// 시작 경로를 막는다. GameObject를 SetActive(false)로 끄지 않는 이유: StartPlayMenu가
    /// FindFirstObjectByType(비활성 제외)으로 이 컨트롤러를 폴링해 처음부터·이어하기·덮어쓰기
    /// 확인(#confirm-panel)·데이터 초기화를 전부 위임하므로, 오브젝트를 끄면 플레이 메뉴가 통째로
    /// 죽는다. externalPrimaryMenu가 기본 버튼 그룹을 숨기고 화면 픽킹을 끄는 정식 억제 수단이며
    /// (FixedUIStartMenuAdapter 선례), 옵션·확인 패널은 폴백으로 계속 동작한다.
    /// </summary>
    static void SuppressLegacyUxmlMenu()
    {
        var startMenu = Object.FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include);
        if (startMenu == null)
        {
            Debug.LogWarning("[StartRework] StartMenuController를 찾지 못해 구 UXML 메뉴 억제를 건너뜁니다.");
            return;
        }

        var serialized = new SerializedObject(startMenu);
        var property = serialized.FindProperty("externalPrimaryMenu");
        if (property == null)
        {
            Debug.LogWarning("[StartRework] externalPrimaryMenu 필드를 찾지 못했습니다. " +
                             "StartMenuController 직렬화 구조가 바뀌었는지 확인이 필요합니다.");
            return;
        }

        if (!property.boolValue)
        {
            property.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>좌측 메뉴 UI(Header·SubTitle·ButtonGroup)를 지운다. Backdrop 배경판은 남긴다.</summary>
    static void RemoveLeftMenu(Canvas leftCanvas)
    {
        var backdrop = leftCanvas.transform.Find("Backdrop");
        if (backdrop == null) return; // 이미 제거된 재실행 상태.

        foreach (var name in new[] { "Header", "SubTitle", "ButtonGroup" })
        {
            var child = backdrop.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }

    // ── 생성 ────────────────────────────────────────────────────────────────

    /// <summary>좌·우 패널 캔버스에 플레이스홀더(이미지+안내 문구)를 넣는다. 기획 이미지가 오면 교체한다.</summary>
    static void AddPlaceholder(Canvas canvas, Vector3 playerPos, Font font, float margin)
    {
        var container = CreateContainer(canvas, GeneratedPrefix + "_Placeholder", playerPos, margin);

        var image = CreateImage(container, "Image",
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            new Color(0.13f, 0.13f, 0.16f, 0.92f));
        Stretch((RectTransform)image.transform);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        var text = CreateText(container, "Label", font, "패널 이미지 준비 중", 34,
            new Color(0.78f, 0.76f, 0.7f, 1f));
        Stretch((RectTransform)text.transform);
    }

    /// <summary>
    /// 중앙 모니터 캔버스에 영상 표면(배경·RawImage·건너뛰기 게이지)을 만든다. 기본은 비활성.
    /// <paramref name="videoLocalZ"/>는 이전 생성물에서 승계한 RawImage 로컬 z(px)다.
    /// </summary>
    static VideoSurfaceParts BuildVideoSurface(
        Canvas centerCanvas, Vector3 playerPos, Font font, float videoLocalZ)
    {
        var container = CreateContainer(centerCanvas, GeneratedPrefix + "_Video", playerPos, 0f);

        // 영상 비율과 캔버스 비율이 다를 때 남는 여백 (CutsceneVideoSurface 선례).
        var background = CreateImage(container, "Background", null, Color.black);
        Stretch((RectTransform)background.transform);
        background.raycastTarget = false;

        var frame = new GameObject("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        frame.layer = UILayer;
        frame.transform.SetParent(container, false);
        var rawImage = frame.GetComponent<RawImage>();
        rawImage.raycastTarget = false;

        var frameRect = (RectTransform)frame.transform;
        Stretch(frameRect);
        // 화면 표면 쪽으로 당겨 둔 손 조정값을 승계한다. Stretch가 x·y를 오프셋으로 몰기 때문에
        // localPosition 대신 anchoredPosition3D로 z만 얹는다(둘의 z는 같은 값이다).
        var framePosition = frameRect.anchoredPosition3D;
        frameRect.anchoredPosition3D = new Vector3(framePosition.x, framePosition.y, videoLocalZ);

        var fitter = frame.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;

        // 건너뛰기 게이지: 영상 하단 중앙의 얇은 가로 바 + 위쪽 힌트 문구.
        //
        // 스프라이트를 쓰지 않는다. 종전 채움은 UI/Skin/UISprite.psd(보더 슬라이스)를 6px 높이로
        // 늘여, 월드 캔버스 축소 렌더(0.001 스케일)에서 보더가 밉맵·필터에 뭉개져 노란 점선처럼
        // 지글거렸다. 순수 Image는 단색 쿼드라 그 앨리어싱이 원천적으로 생기지 않는다.
        //
        // 트랙도 검정 α0.55 → 흰색 α0.16으로 낮춘다. 종전 값은 게이지를 쓰지 않는 동안에도 영상
        // 하단을 가로지르는 회색 띠로 보였다. 표시 자체는 CanvasGroup α로 통제하며(평소 0),
        // StartMonitorPrologue가 건너뛰기 홀드 중에만 띄운다.
        var gaugeRoot = new GameObject("SkipGauge", typeof(RectTransform), typeof(CanvasGroup));
        gaugeRoot.layer = UILayer;
        gaugeRoot.transform.SetParent(container, false);
        var gaugeRect = (RectTransform)gaugeRoot.transform;
        gaugeRect.anchorMin = new Vector2(0.5f, 0f);
        gaugeRect.anchorMax = new Vector2(0.5f, 0f);
        gaugeRect.pivot = new Vector2(0.5f, 0f);
        gaugeRect.anchoredPosition = new Vector2(0f, 40f);
        gaugeRect.sizeDelta = new Vector2(420f, 6f);

        var gaugeGroup = gaugeRoot.GetComponent<CanvasGroup>();
        gaugeGroup.alpha = 0f;
        gaugeGroup.interactable = false;
        gaugeGroup.blocksRaycasts = false;

        var gaugeBackground = CreateImage(gaugeRoot.transform, "Track", null, new Color(1f, 1f, 1f, 0.16f));
        Stretch((RectTransform)gaugeBackground.transform);
        gaugeBackground.raycastTarget = false;

        // 인셋 없이 트랙과 같은 크기다. 6px 바에 2px 인셋을 넣으면 실제 채움이 2px만 남는다.
        var gaugeFill = CreateImage(gaugeRoot.transform, "Fill", null,
            new Color(Accent.r, Accent.g, Accent.b, 0.95f));
        Stretch((RectTransform)gaugeFill.transform);
        gaugeFill.type = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        gaugeFill.fillAmount = 0f;
        gaugeFill.raycastTarget = false;

        // 힌트 문구: 바 위 8px. 밝은 영상 프레임 위에서도 읽히도록 그림자를 깐다.
        var hint = CreateText(gaugeRoot.transform, "Hint", font, "Space 길게 눌러 건너뛰기", 22,
            new Color(1f, 1f, 1f, 0.92f));
        var hintRect = (RectTransform)hint.transform;
        hintRect.anchorMin = new Vector2(0.5f, 1f);
        hintRect.anchorMax = new Vector2(0.5f, 1f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 8f);
        hintRect.sizeDelta = new Vector2(420f, 28f);

        var hintShadow = hint.gameObject.AddComponent<Shadow>();
        hintShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        hintShadow.effectDistance = new Vector2(1f, -1f);

        gaugeRoot.SetActive(false);
        container.gameObject.SetActive(false);

        return new VideoSurfaceParts
        {
            Group = container.gameObject,
            Image = rawImage,
            Fitter = fitter,
            GaugeRoot = gaugeRoot,
            GaugeGroup = gaugeGroup,
            GaugeFill = gaugeFill
        };
    }

    /// <summary>
    /// 원형 플레이 버튼 캔버스, 시청 앵커, 컨트롤러를 StartRework 루트 아래에 만들고 배선한다.
    /// 자식 배치는 전부 월드 세터로 넣어 루트 포즈와 무관하게 의도한 월드 지점에 놓인다.
    /// </summary>
    static void BuildPlayMenuAndControllers(
        Canvas centerCanvas, Vector3 playerPos, Font font, VideoSurfaceParts video,
        PreservedPose preserved)
    {
        var root = new GameObject(RootName);

        // 자식을 붙이기 전에 루트 포즈를 먼저 복원한다. 루트는 씬 최상위라 로컬 = 월드다.
        // 아래 자식들은 월드 세터로 배치하므로 이 포즈가 자식 위치를 끌고 가지 않는다.
        root.transform.SetPositionAndRotation(preserved.RootPosition, preserved.RootRotation);
        root.transform.localScale = preserved.RootScale;

        var monitorPos = centerCanvas.transform.position;
        var toPlayer = playerPos - monitorPos;
        toPlayer.y = 0f;
        toPlayer = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.forward;

        // ── 플레이 버튼 캔버스: 중앙 모니터 앞에 플레이어를 마주 보게 세운다.
        //    월드 캔버스는 앞면(+Z)이 시선 방향과 일치해야 글자가 바로 보인다.
        var canvasObject = new GameObject("PlayMenuCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.layer = UILayer;
        canvasObject.transform.SetParent(root.transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.sizeDelta = new Vector2(360f, 480f);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        // 버튼들이 밝은 모니터 화면(0.909x0.506m)을 배경으로 걸치게 화면 영역 안에 배치한다.
        // monitorPos.y ≈ 화면 중심 1.91m(하단 1.656m·상단 2.162m). down 0.35f였을 때는 버튼 중심이
        // 1.91 - 0.35 - 0.15(anchoredPosition -150px x 스케일 0.001) ≈ 1.41m로 화면 하단보다
        // 0.25m 아래 어두운 벽에 놓여 "검은 방의 검은 버튼"이 됐다. down 0.10f + 버튼 -40px이면
        // 중심이 1.91 - 0.10 - 0.04 ≈ 1.77m로 화면 안에 들어온다(아래 각 요소 주석 참조).
        // 월드 세터(position/rotation)로 넣는다 — 목표가 모니터·플레이어 월드 좌표에서 나온 값이라
        // 루트를 옮겨도 이 캔버스는 같은 월드 지점에 놓여야 한다. RectTransform도 월드 세터가 동작한다.
        // 버튼은 모니터 화면 "위"에 있어야 한다(R 확정). 확인 패널(0.02)보다 1cm 더 앞(0.03)에 두어
        // 영상 표면·화면 면과의 z-fighting을 피한다. 앞으로 띄우던 0.55m는 화면에서 버튼이 떨어져
        // 보여 폐기했다.
        canvasRect.SetPositionAndRotation(
            monitorPos + toPlayer * 0.03f + Vector3.down * 0.10f,
            Quaternion.LookRotation(-toPlayer, Vector3.up));

        // ── 원형 플레이 버튼(▶): Knob 원형 스프라이트 + 재생 아이콘.
        //    원판은 구 UXML의 흰 유리(.menu-button), 아이콘은 잉크색 전경 — 아이콘 png가 전 픽셀
        //    검정(RGB 0,0,0)이라 흰 틴트로는 색이 나오지 않고, 밝은 원판 위에서 짙은 전경이 또렷하다.
        //    유리 α는 GlassButtonColors가 쥐므로 이미지 색은 흰색 고정이다.
        var playButtonImage = CreateImage(canvasObject.transform, "PlayButton",
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            Color.white);
        var playRect = (RectTransform)playButtonImage.transform;
        playRect.sizeDelta = new Vector2(140f, 140f);
        // -40px: 버튼 중심 y = 1.81 - 0.04 = 1.77m, 원판(140px) 하단도 1.70m로 화면 하단(1.656m) 위.
        playRect.anchoredPosition = new Vector2(0f, -40f);
        var playButton = playButtonImage.gameObject.AddComponent<Button>();
        playButton.targetGraphic = playButtonImage;
        playButton.colors = GlassButtonColors;

        var playIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PlayIconPath);
        if (playIcon != null)
        {
            var icon = CreateImage(playButtonImage.transform, "Icon", playIcon, InkText);
            var iconRect = (RectTransform)icon.transform;
            iconRect.sizeDelta = new Vector2(72f, 72f);
            icon.raycastTarget = false;
        }
        else
        {
            Debug.LogWarning($"[StartRework] 재생 아이콘을 찾지 못했습니다: {PlayIconPath}");
        }

        // ── 2옵션 그룹: 원형 버튼 위로 "처음부터"(위)·"이어하기"(아래). 평소에는 꺼져 있다.
        //    버튼이 -40px로 올라와 종전 좌표(+70/-20)로는 원판(-110~+30px)과 겹친다. +80/+170px로
        //    올려 원판 상단과 7px 간격을 두고, "처음부터" 상단(+208px)도 화면 상단
        //    (1.81 + 0.208 = 2.02m < 2.162m) 안에 들어온다.
        var options = new GameObject("Options", typeof(RectTransform));
        options.layer = UILayer;
        options.transform.SetParent(canvasObject.transform, false);
        Stretch((RectTransform)options.transform);

        var newGameButton = CreateMenuButton(options.transform, "NewGame", font, "처음부터",
            new Vector2(0f, 170f));
        var continueButton = CreateMenuButton(options.transform, "Continue", font, "이어하기",
            new Vector2(0f, 80f));

        options.SetActive(false);

        // ── 시청 앵커: 모니터 정면, 플레이어 발 높이. yaw만 플레이어에 적용된다.
        //    화면 확대는 거리 대신 시청 중 FOV 축소로 처리한다 (StartMonitorPrologue.videoFieldOfView).
        //    StartMonitorPrologue가 이 Transform의 월드 position으로 플레이어를 순간이동시키므로
        //    반드시 월드 세터로 넣는다 — 루트를 옮긴 만큼 밀리면 플레이어가 바닥에서 떠오른다.
        var anchor = new GameObject("PrologueViewAnchor");
        anchor.transform.SetParent(root.transform, false);
        var anchorPos = monitorPos + toPlayer * 1.7f;
        anchorPos.y = playerPos.y;
        anchor.transform.SetPositionAndRotation(anchorPos, Quaternion.LookRotation(-toPlayer, Vector3.up));

        // ── 저장 덮어쓰기 확인 패널: 모니터 화면을 덮는 모달.
        var confirm = BuildConfirmPanel(root.transform, monitorPos, toPlayer, font);

        // ── 컨트롤러: 메뉴 루트 밖에 두어 메뉴를 숨겨도 Update가 계속 돈다.
        //    렌더링도 판정도 없는 로직 전용 노드라 루트 포즈를 그대로 물려받아도 무방하다.
        var controllers = new GameObject("Controllers");
        controllers.transform.SetParent(root.transform, false);

        var prologue = controllers.AddComponent<StartMonitorPrologue>();
        var prologueSerialized = new SerializedObject(prologue);
        prologueSerialized.FindProperty("videoGroup").objectReferenceValue = video.Group;
        prologueSerialized.FindProperty("videoImage").objectReferenceValue = video.Image;
        prologueSerialized.FindProperty("videoFitter").objectReferenceValue = video.Fitter;
        prologueSerialized.FindProperty("skipGaugeRoot").objectReferenceValue = video.GaugeRoot;
        prologueSerialized.FindProperty("skipGaugeGroup").objectReferenceValue = video.GaugeGroup;
        prologueSerialized.FindProperty("skipGaugeFill").objectReferenceValue = video.GaugeFill;
        prologueSerialized.FindProperty("viewAnchor").objectReferenceValue = anchor.transform;
        prologueSerialized.FindProperty("lookTarget").objectReferenceValue = centerCanvas.transform;
        prologueSerialized.ApplyModifiedPropertiesWithoutUndo();

        var menu = controllers.AddComponent<StartPlayMenu>();
        var menuSerialized = new SerializedObject(menu);
        menuSerialized.FindProperty("menuRoot").objectReferenceValue = canvasObject;
        menuSerialized.FindProperty("playButton").objectReferenceValue = playButton;
        menuSerialized.FindProperty("optionsRoot").objectReferenceValue = options;
        menuSerialized.FindProperty("newGameButton").objectReferenceValue = newGameButton;
        menuSerialized.FindProperty("continueButton").objectReferenceValue = continueButton;
        menuSerialized.FindProperty("prologue").objectReferenceValue = prologue;
        menuSerialized.FindProperty("confirmRoot").objectReferenceValue = confirm.Root;
        menuSerialized.FindProperty("confirmAcceptButton").objectReferenceValue = confirm.Accept;
        menuSerialized.FindProperty("confirmCancelButton").objectReferenceValue = confirm.Cancel;
        menuSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 저장 덮어쓰기 확인 패널. 중앙 모니터 화면(0.909x0.506m)을 정확히 덮는 별도 월드 캔버스로
    /// 만든다 — 기존 중앙 캔버스는 공용 자산이라 GraphicRaycaster를 붙이는 대신 새 캔버스를 겹치고
    /// (빌더는 캔버스 원본을 건드리지 않는다), 화면 표면·영상 RawImage와의 z-fighting을 피해
    /// 플레이어 쪽으로 2cm 띄운다. 문구는 구 UXML 확인 창(StartMenu.uxml #confirm-panel)을 그대로
    /// 승계했다. 기본은 비활성이며 StartPlayMenu가 열고 닫는다.
    /// </summary>
    static ConfirmPanelParts BuildConfirmPanel(
        Transform root, Vector3 monitorPos, Vector3 toPlayer, Font font)
    {
        var canvasObject = new GameObject("ConfirmCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.layer = UILayer;
        canvasObject.transform.SetParent(root, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.sizeDelta = CenterCanvasSize;
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        // 월드 세터로 넣는다. 로컬로 넣으면 루트를 옮긴 만큼 그대로 밀려 화면 뒤에 파묻히고
        // (루트가 -z로 0.538m 이동했을 때 실제로 모니터 면 뒤로 들어가 확인 패널이 보이지 않았다),
        // 앞으로 2cm 띄운 z-fighting 회피 간격도 무의미해진다.
        canvasRect.SetPositionAndRotation(
            monitorPos + toPlayer * 0.02f,
            Quaternion.LookRotation(-toPlayer, Vector3.up));

        // 모달 암막(.modal-layer). raycastTarget 기본값(true)을 그대로 두어 크로스헤어·마우스가
        // 뒤 요소로 새지 않게 막는다.
        var backdrop = CreateImage(canvasObject.transform, "Backdrop", null, ModalBackdrop);
        Stretch((RectTransform)backdrop.transform);

        // 확인 카드(.options-card 스모키 유리).
        var card = CreateImage(canvasObject.transform, "Card",
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"), PanelGlass);
        card.type = Image.Type.Sliced;
        var cardRect = (RectTransform)card.transform;
        cardRect.sizeDelta = new Vector2(640f, 360f);

        var title = CreateText(card.transform, "Title", font,
            "기존 진행 기록이 있습니다.", 34, Color.white);
        ((RectTransform)title.transform).anchoredPosition = new Vector2(0f, 100f);

        var message = CreateText(card.transform, "Message", font,
            "새 게임을 시작하면 이전 진행 기록이 초기화됩니다.", 24, new Color(1f, 1f, 1f, 0.75f));
        ((RectTransform)message.transform).anchoredPosition = new Vector2(0f, 30f);

        var accept = CreateMenuButton(card.transform, "Accept", font, "새로 시작",
            new Vector2(-160f, -90f));
        var cancel = CreateMenuButton(card.transform, "Cancel", font, "취소",
            new Vector2(160f, -90f), quiet: true);

        canvasObject.SetActive(false);

        return new ConfirmPanelParts { Root = canvasObject, Accept = accept, Cancel = cancel };
    }

    // ── 공통 헬퍼 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 캔버스 전체를 덮는 생성물 컨테이너. 캔버스 앞면(+Z)이 플레이어 쪽을 향하면 uGUI가 좌우
    /// 반전으로 보이므로 컨테이너를 Y축 180도로 뒤집어 보정한다(캔버스 원본은 건드리지 않는다).
    /// </summary>
    static Transform CreateContainer(Canvas canvas, string name, Vector3 playerPos, float margin)
    {
        var container = new GameObject(name, typeof(RectTransform));
        container.layer = UILayer;
        var rect = (RectTransform)container.transform;
        rect.SetParent(canvas.transform, false);
        Stretch(rect);
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);

        var toPlayer = playerPos - canvas.transform.position;
        if (Vector3.Dot(canvas.transform.forward, toPlayer) > 0f)
            rect.localRotation = Quaternion.Euler(0f, 180f, 0f);

        return rect;
    }

    /// <summary>
    /// 흰 유리 + 잉크 텍스트 버튼(.menu-button / quiet는 .menu-button--quiet). 유리 α는 ColorBlock이
    /// 쥐므로 이미지 색은 흰색으로 둔다. 비활성 시 텍스트 α(전경 0.28)까지는 uGUI ColorBlock이
    /// 대상 그래픽 하나만 틴트해 재현하지 못한다 — 판 자체가 α0.24로 죽어 구분은 충분하다.
    /// </summary>
    static Button CreateMenuButton(
        Transform parent, string name, Font font, string label, Vector2 anchoredPosition,
        bool quiet = false)
    {
        var image = CreateImage(parent, name,
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            Color.white);
        image.type = Image.Type.Sliced;

        var rect = (RectTransform)image.transform;
        rect.sizeDelta = new Vector2(280f, 76f);
        rect.anchoredPosition = anchoredPosition;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = quiet ? QuietButtonColors : GlassButtonColors;

        var text = CreateText(image.transform, "Label", font, label, 36,
            quiet ? InkTextQuiet : InkText);
        Stretch((RectTransform)text.transform);

        return button;
    }

    static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = UILayer;
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    static Text CreateText(Transform parent, string name, Font font, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.layer = UILayer;
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 한글 라벨용 폰트. FixedUIStartMenuAdapter가 쓰던 Giants-Bold.ttf를 그대로 빌려 쓰고, 없으면
    /// 내장 폰트로 대체한다(한글 미지원일 수 있어 경고를 남긴다).
    /// </summary>
    static Font LoadKoreanFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(KoreanFontPath);
        if (font != null) return font;

        Debug.LogWarning($"[StartRework] 한글 폰트를 찾지 못해 내장 폰트로 대체합니다: {KoreanFontPath}");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    /// <summary>
    /// 재생성 사이에 승계할 손 조정값. <see cref="RemovePrevious"/>가 이전 생성물에서 읽고, 없으면
    /// 기본 상수로 채운다. 빌더가 계산으로 정하는 값은 여기 담지 않는다 — 계산분은 매번 새로 넣어야
    /// 배치 의도가 코드 한 곳에 남는다.
    /// </summary>
    readonly struct PreservedPose
    {
        public readonly Vector3 RootPosition;
        public readonly Quaternion RootRotation;
        public readonly Vector3 RootScale;

        /// <summary>영상 RawImage의 로컬 z(px). 캔버스 스케일 0.001이라 -19px = 월드 19mm 앞이다.</summary>
        public readonly float VideoLocalZ;

        public PreservedPose(Vector3 rootPosition, Quaternion rootRotation, Vector3 rootScale,
            float videoLocalZ)
        {
            RootPosition = rootPosition;
            RootRotation = rootRotation;
            RootScale = rootScale;
            VideoLocalZ = videoLocalZ;
        }
    }

    struct ConfirmPanelParts
    {
        public GameObject Root;
        public Button Accept;
        public Button Cancel;
    }

    struct VideoSurfaceParts
    {
        public GameObject Group;
        public RawImage Image;
        public AspectRatioFitter Fitter;
        public GameObject GaugeRoot;
        public CanvasGroup GaugeGroup;
        public Image GaugeFill;
    }
}
