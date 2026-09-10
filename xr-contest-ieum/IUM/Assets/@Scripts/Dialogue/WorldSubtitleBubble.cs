using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화자 머리 위에 뜨는 월드 공간 말풍선. 계층 전체를 코드로 세운다 — 씬·프리팹을 건드리지 않는
/// 것이 이 시스템의 전제이고, VR에서 화면 공간 UI가 아예 렌더되지 않는 것이 만들어진 이유다.
///
/// 스케일 무관 설계가 핵심이다. Play 씬은 월드가 ×20이라 "월드 1유닛"이 씬마다 다른 크기를
/// 뜻한다. 그래서 크기를 월드 단위로 못 박지 않고 <b>시야각</b>으로 정한다 — 캔버스는 고정된
/// 픽셀 크기로 만들고, 루트 스케일을 카메라 거리에 비례시켜 화면에서 차지하는 각도를 일정하게
/// 유지한다. 비례식이라 월드 배율이 얼마든 결과가 같다.
///
/// 화자에 <b>부모로 붙이지 않는다</b>. 붙이면 화자의 lossyScale을 그대로 물려받아 위의 계산이
/// 무너진다. 대신 매 <see cref="LateUpdate"/>마다 앵커 위치를 읽어 따라간다.
///
/// 시간은 전부 <see cref="Time.unscaledDeltaTime"/>으로 흐른다. 일시정지 중에도 이미 떠 있는
/// 자막은 그대로 있어야 하고(<c>SubtitleView</c>와 같은 계약), timeScale이 0이면 페이드가 멈춰
/// 반투명 상태로 굳는다.
/// </summary>
public sealed class WorldSubtitleBubble : MonoBehaviour
{
    /// <summary>화자별 글자색. <c>SubtitleView</c>의 값을 그대로 쓴다 — 두 뷰가 달라지면 안 된다.</summary>
    static readonly Color NojangColor = new(1f, 0.804f, 0.376f);   // #FFCD60
    static readonly Color IeumiColor = new(0.31f, 0.796f, 1f);     // #4FCBFF
    static readonly Color DefaultColor = Color.white;              // #FFFFFF

    /// <summary>캔버스 기준 픽셀 폭. 실제 크기는 루트 스케일이 정하므로 이 값은 해상도일 뿐이다.</summary>
    const float ReferenceWidth = 900f;
    const float ReferenceHeight = 520f;

    /// <summary>본문 최대 폭(px). 이 폭에서 줄바꿈이 일어난다.</summary>
    const float MaxTextWidth = 760f;

    const float PaddingX = 40f;
    const float PaddingY = 28f;
    const float FontSize = 52f;
    const float TailHeight = 26f;
    const float TailWidth = 42f;

    /// <summary>
    /// 말풍선이 화면에서 차지할 가로 시야각. 거리 스케일의 기준이다.
    ///
    /// 글자 크기는 여기서 따라 나온다 — 한 줄의 시야각 높이 =
    /// <see cref="AngularWidthDegrees"/> × <see cref="FontSize"/> ÷ <see cref="ReferenceWidth"/>
    /// = 약 1.5°다. VR에서 편하게 읽히는 자막 높이(1.5~2°) 구간이며, 세 값 중 하나를 바꾸면
    /// 나머지도 이 식으로 다시 맞춰야 한다.
    /// </summary>
    const float AngularWidthDegrees = 26f;

    /// <summary>스케일 클램프. 화자 크기로 추정한 월드 배율의 배수로 표현해 ×20 씬에서도 통한다.</summary>
    const float MinDistanceFactor = 0.3f;
    const float MaxDistanceFactor = 30f;

    const float FadeSeconds = 0.18f;

    /// <summary>사람 키(m). 화자 렌더러 높이를 이 값으로 나눠 씬의 월드 배율을 추정한다.</summary>
    const float HumanHeightMeters = 1.7f;

    // ---- 폴백 패널(화자 없음) ----

    /// <summary>카메라 앞 고정 거리. 월드 배율 배수다.</summary>
    const float FallbackDistanceFactor = 2.2f;

    /// <summary>시선 중심에서 내려 앉는 높이. 정면을 가리지 않도록.</summary>
    const float FallbackDropFactor = 0.45f;

    /// <summary>이 각도 안쪽이면 따라가지 않는다. VR에서 고개를 조금 움직일 때마다 UI가 흔들리면 멀미가 온다.</summary>
    const float FallbackDeadZoneDegrees = 18f;

    /// <summary>데드존을 넘었을 때 따라붙는 속도(초당 비율). 낮을수록 느긋하다.</summary>
    const float FallbackFollowSharpness = 2.2f;

    RectTransform _root;
    RectTransform _panel;
    RectTransform _tail;
    Canvas _canvas;
    CanvasGroup _group;
    Image _panelImage;
    Image _tailImage;

    /// <summary>깊이 판정을 끄려고 만든 머티리얼 인스턴스. 파괴 대상은 이 둘뿐이다.</summary>
    Material _panelMaterial;
    Material _tailMaterial;
    TextMeshProUGUI _text;

    Transform _boundsRoot;
    Transform _headFallback;

    /// <summary>화자의 렌더러. 앵커가 바뀔 때만 다시 모은다.</summary>
    readonly List<Renderer> _renderers = new();

    /// <summary>TMP가 만든 폴백·넘침 서브메시. 재사용해 매 줄 배열 할당을 피한다.</summary>
    readonly List<TMP_SubMeshUI> _subMeshes = new();

    /// <summary>화자 크기에서 추정한 씬의 월드 배율. 화자가 없으면 카메라 리그 스케일로 대신한다.</summary>
    float _worldScale = 1f;

    /// <summary>폴백 패널이 향하는 수평 방향. 카메라 정면을 느리게 쫓는다.</summary>
    Vector3 _fallbackDirection = Vector3.forward;
    bool _fallbackInitialized;

    float _alpha;
    float _targetAlpha;

    public bool IsVisible => _targetAlpha > 0.001f;

    /// <summary>현재 잡고 있는 앵커. 화자가 사라졌는지 확인할 때 쓴다.</summary>
    public Transform Anchor => _boundsRoot;

    /// <summary>
    /// 말풍선을 만든다. <paramref name="parent"/>는 스케일이 1인 루트여야 한다 — 매니저의
    /// DontDestroyOnLoad 오브젝트가 그 조건을 만족한다.
    /// </summary>
    public static WorldSubtitleBubble Create(Transform parent, string name)
    {
        var host = new GameObject(name, typeof(RectTransform));
        host.transform.SetParent(parent, false);

        var bubble = host.AddComponent<WorldSubtitleBubble>();
        bubble.Build();
        return bubble;
    }

    void Build()
    {
        _root = (RectTransform)transform;
        _root.localPosition = Vector3.zero;
        _root.localRotation = Quaternion.identity;
        _root.localScale = Vector3.one;
        _root.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

        // 피벗이 아래 가운데다. 루트 위치 = 꼬리 끝 = 머리 위 지점이 되어 말풍선이 위로 자란다.
        _root.pivot = new Vector2(0.5f, 0f);

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        // 월드 지오메트리와 다투지 않도록 UI 레이어 안에서 가장 위에 둔다. 실제 가림 방지는
        // ZTest 쪽이 맡는다.
        _canvas.sortingOrder = 100;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // GraphicRaycaster를 붙이지 않는다. 자막은 조작 대상이 아니고, 레이캐스터가 있으면
        // 월드 메뉴 포인터가 자막을 집는다.

        var background = new Color(0.04f, 0.05f, 0.07f, 0.72f);

        _panel = CreateChild("Panel", out _panelImage);
        _panelImage.sprite = BubbleSprites.RoundedRect();
        _panelImage.type = Image.Type.Sliced;
        _panelImage.color = background;
        _panel.pivot = new Vector2(0.5f, 0f);
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0f);
        _panel.anchoredPosition = new Vector2(0f, TailHeight - 2f);

        _tail = CreateChild("Tail", out _tailImage);
        _tailImage.sprite = BubbleSprites.DownTriangle();
        _tailImage.type = Image.Type.Simple;
        _tailImage.color = background;
        _tail.pivot = new Vector2(0.5f, 0f);
        _tail.anchorMin = _tail.anchorMax = new Vector2(0.5f, 0f);
        _tail.sizeDelta = new Vector2(TailWidth, TailHeight);
        _tail.anchoredPosition = Vector2.zero;

        BuildText();
        ApplyAlwaysOnTop();

        gameObject.SetActive(false);
    }

    RectTransform CreateChild(string name, out Image image)
    {
        var host = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)host.transform;
        rect.SetParent(_root, false);
        rect.localScale = Vector3.one;

        image = host.AddComponent<Image>();
        image.raycastTarget = false;
        return rect;
    }

    void BuildText()
    {
        var host = new GameObject("Text", typeof(RectTransform));
        var rect = (RectTransform)host.transform;
        rect.SetParent(_panel, false);

        // 패널 안쪽에 패딩만큼 물린다. 패널 크기가 바뀌면 같이 늘어난다.
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(PaddingX, PaddingY);
        rect.offsetMax = new Vector2(-PaddingX, -PaddingY);

        _text = host.AddComponent<TextMeshProUGUI>();
        _text.raycastTarget = false;
        _text.fontSize = FontSize;
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.richText = false;

        // 여기서는 폰트를 고르지 않는다. 말풍선은 첫 씬이 로드되기 전에 만들어지므로 이 시점에는
        // 프로젝트 서체가 아직 메모리에 없고, 그 상태로 고르면 OS 폰트가 캐시돼 눌러앉는다.
        // 실제 선택은 첫 Show에서 한다.
        ApplyFont(null);
    }

    /// <summary>
    /// 폰트를 갈아 끼우고 글자 머티리얼에 의존하는 설정을 다시 얹는다. 폰트를 바꾸면 TMP가
    /// 공유 머티리얼을 새 폰트의 것으로 갈아 끼우므로 외곽선과 깊이 설정이 함께 날아간다 —
    /// 두 작업은 떼어 놓을 수 없다.
    ///
    /// <c>TMP_Text.outlineWidth</c>·<c>outlineColor</c> 프로퍼티를 쓰지 않는다. 그 setter들은
    /// 머티리얼이 아니라 <b>캐시된 필드</b>와 값을 비교해 같으면 그냥 빠져나가는데, 폰트 교체는
    /// 머티리얼만 바꾸고 그 필드는 그대로 두기 때문에 두 번째 호출부터 조용히 무시된다. 결과는
    /// 외곽선이 사라진 자막이다. 머티리얼에 직접 쓰면 그 함정이 없다.
    /// </summary>
    void ApplyFont(TMP_FontAsset font)
    {
        if (font != null && _text.font != font) _text.font = font;

        // ID_* 는 지연 초기화라 0으로 남아 있을 수 있다. 그러면 SetColor가 엉뚱한 프로퍼티를
        // 건드리고 아무 경고도 나지 않는다. 두 번째 호출부터는 플래그 검사 한 번으로 끝난다.
        ShaderUtilities.GetShaderPropertyIDs();

        var material = _text.fontMaterial;
        if (material == null) return;

        // 반투명 판 위이긴 해도 밝은 하늘이 비쳐 들 수 있다. SubtitleView가 배경 판 없이 버티는
        // 근거가 검은 외곽선이었으므로 여기서도 같은 보험을 든다.
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.78f));
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
        material.SetFloat(ShaderUtilities.ShaderTag_ZTestMode, AlwaysZTest);

        // 외곽선은 글자 사각형 밖으로 번지므로 TMP가 여백을 다시 계산해야 한다. 프로퍼티 setter가
        // 대신 해 주던 일이고, 빼먹으면 외곽선 가장자리가 잘려 나간다.
        _text.UpdateMeshPadding();
    }

    /// <summary>
    /// 폴백 글리프와 넘침 아틀라스는 TMP가 <see cref="TMP_SubMeshUI"/> 자식으로 따로 그린다.
    /// 그 머티리얼들은 본체와 별개라 깊이 설정을 여기서 한 번 더 얹어야 한다 — 빼먹으면 한글 일부만
    /// 기둥 뒤로 사라지는, 이 시스템이 없애려던 바로 그 증상이 남는다.
    /// </summary>
    void ApplySubMeshZTest()
    {
        // 서브메시는 메시를 만들 때 생긴다. 글자를 바꾼 직후에는 아직 없을 수 있다.
        _text.ForceMeshUpdate();

        _subMeshes.Clear();
        _text.GetComponentsInChildren(true, _subMeshes);

        foreach (var subMesh in _subMeshes)
        {
            if (subMesh == null) continue;

            var material = subMesh.material;
            if (material == null) continue;

            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.78f));
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            material.SetFloat(ShaderUtilities.ShaderTag_ZTestMode, AlwaysZTest);
        }
    }

    const float OutlineWidth = 0.12f;

    /// <summary>CompareFunction.Always. UnityEngine.Rendering을 끌어오지 않으려고 값으로 둔다.</summary>
    const float AlwaysZTest = 8f;

    /// <summary>
    /// 기둥이나 부재 뒤로 들어가도 자막이 사라지지 않게 깊이 판정을 끈다. 셰이더가 이 프로퍼티를
    /// 갖고 있지 않으면 조용히 무시되고 가림만 남는다 — 기능이 죽지는 않는다.
    /// </summary>
    void ApplyAlwaysOnTop()
    {
        _panelMaterial = SetZTest(_panelImage);
        _tailMaterial = SetZTest(_tailImage);

        static Material SetZTest(Image image)
        {
            if (image == null) return null;

            // 공용 머티리얼을 건드리면 다른 UI까지 깊이 판정이 꺼진다. 인스턴스를 만들어 쓴다.
            // Graphic.material은 지정이 없으면 기본 UI 머티리얼을 돌려주므로 보통 null이 아니다.
            var source = image.material;
            if (source == null) return null;

            var instance = new Material(source) { name = source.name + " (Bubble)" };
            instance.SetFloat(ShaderUtilities.ShaderTag_ZTestMode, AlwaysZTest);
            image.material = instance;
            return instance;
        }
    }

    /// <summary>
    /// 앵커를 갈아 끼운다. <paramref name="boundsRoot"/>가 null이면 카메라 앞 폴백 패널로 돈다.
    /// 화자가 바뀌면 위치를 보간하지 않고 즉시 옮긴다 — 씬을 가로지르는 미끄러짐이 더 어색하다.
    /// </summary>
    public void SetAnchor(Transform boundsRoot, Transform headFallback)
    {
        var changed = _boundsRoot != boundsRoot || _headFallback != headFallback;

        _boundsRoot = boundsRoot;
        _headFallback = headFallback;

        if (!changed) return;

        _fallbackInitialized = false;
        CacheRenderers();
        if (boundsRoot != null) _worldScale = EstimateWorldScale();
    }

    /// <summary>
    /// 화자의 렌더러를 한 번만 모은다. 바운즈는 매 프레임 다시 재야 하지만(노장은 걸어 다니고
    /// 애니메이션에 따라 바운즈가 바뀐다) 컴포넌트 조회까지 매 프레임 할 이유는 없다 —
    /// <c>GetComponentsInChildren</c>은 호출마다 배열을 새로 만들어 Quest에서 GC를 부른다.
    /// </summary>
    void CacheRenderers()
    {
        _renderers.Clear();

        var root = _boundsRoot != null ? _boundsRoot : _headFallback;
        if (root == null) return;

        root.GetComponentsInChildren(true, _renderers);

        // 파티클·트레일은 순간적으로 바운즈가 크게 튀어 자막을 하늘로 날린다.
        for (var i = _renderers.Count - 1; i >= 0; i--)
            if (_renderers[i] is ParticleSystemRenderer or TrailRenderer or LineRenderer)
                _renderers.RemoveAt(i);
    }

    public void Show(DialogueSpeaker speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Hide();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // 폰트를 지금 다시 확인한다. 씬이 바뀌며 더 나은 폰트가 올라왔을 수 있고, 첫 자막이
        // 뜨기 전에는 어떤 씬이 로드될지 알 수 없다.
        ApplyFont(DialogueBubbleFont.Resolve());

        _text.color = ColorFor(speaker);
        _text.text = text;

        Layout(text);
        ApplySubMeshZTest();

        _targetAlpha = 1f;

        // 첫 프레임을 기다리지 않고 지금 배치한다. 한 프레임이라도 이전 화자 자리에 뜨면 눈에 띈다.
        Place();
    }

    public void Hide()
    {
        _targetAlpha = 0f;
        if (_alpha > 0.001f || !gameObject.activeSelf) return;

        gameObject.SetActive(false);
        _fallbackInitialized = false;
    }

    static Color ColorFor(DialogueSpeaker speaker) => speaker switch
    {
        DialogueSpeaker.Nojang => NojangColor,
        DialogueSpeaker.Ieumi => IeumiColor,
        _ => DefaultColor
    };

    /// <summary>글자 길이에 맞춰 판 크기를 잡는다. 폭은 <see cref="MaxTextWidth"/>에서 잘리고 줄이 넘어간다.</summary>
    void Layout(string text)
    {
        var preferred = _text.GetPreferredValues(text, MaxTextWidth, 0f);
        var width = Mathf.Clamp(preferred.x, 160f, MaxTextWidth);
        var height = Mathf.Max(preferred.y, FontSize * 1.2f);

        // 여유 2픽셀. 측정한 폭을 글자 영역에 그대로 주면 반올림 차이로 마지막 단어가 다음 줄로
        // 밀려나면서, 방금 그 글자에 맞춰 잡은 판을 넘쳐 버리는 일이 생긴다.
        _panel.sizeDelta = new Vector2(width + PaddingX * 2f + 2f, height + PaddingY * 2f);
    }

    void LateUpdate()
    {
        StepFade();
        if (_alpha <= 0.001f && _targetAlpha <= 0.001f) return;

        Place();
    }

    void StepFade()
    {
        // unscaledDeltaTime이라 일시정지 중에도 페이드가 끝까지 간다. 그것이 의도다 — 자막이
        // 반투명하게 얼어붙는 것보다 낫고, 이미 떠 있는 자막은 유지된다는 계약과도 어긋나지 않는다.
        var step = Time.unscaledDeltaTime / Mathf.Max(0.01f, FadeSeconds);
        _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, step);
        _group.alpha = _alpha;

        if (_alpha > 0.001f || _targetAlpha > 0.001f || !gameObject.activeSelf) return;

        gameObject.SetActive(false);

        // 다시 뜰 때는 폴백 패널을 시야 정면에 새로 잡는다. 그러지 않으면 자막이 없는 동안
        // 플레이어가 돌아본 만큼 패널이 등 뒤에 나타나 천천히 돌아 들어온다.
        _fallbackInitialized = false;
    }

    /// <summary>
    /// 위치·회전·크기를 한 프레임분 갱신한다. 화자 앵커는 매번 새로 읽으므로 화자가 바뀌면
    /// 그 프레임에 바로 옮겨 간다 — 씬을 가로지르는 보간이 없다.
    /// </summary>
    void Place()
    {
        var camera = ResolveCamera();
        if (camera == null) return;

        var cameraPosition = camera.transform.position;

        if (_boundsRoot != null || _headFallback != null) PlaceOverSpeaker();
        else PlaceInFront(camera);

        // 빌보드. 캔버스의 +Z는 보는 쪽의 반대를 향해야 글자가 바로 보인다. 롤은 월드 up으로
        // 고정한다 — 카메라 up을 따라가면 HMD를 기울일 때 자막이 같이 기울어 읽기 나쁘다.
        var away = transform.position - cameraPosition;
        if (away.sqrMagnitude > 0.0000001f)
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);

        ApplyDistanceScale(Vector3.Distance(transform.position, cameraPosition));
    }

    void PlaceOverSpeaker()
    {
        var bounds = ResolveBounds();
        var top = bounds.center + Vector3.up * bounds.extents.y;

        // 머리 위 여유. 화자 크기에 비례시켜 ×20 씬에서도 같은 비율로 뜬다.
        top += Vector3.up * (bounds.size.y * 0.10f + 0.05f * _worldScale);
        transform.position = top;
    }

    /// <summary>
    /// 캐시한 렌더러로 월드 바운즈를 만든다. 매 프레임 다시 재는 이유는 노장이 순찰·추종으로
    /// 움직이고 애니메이션에 따라 바운즈가 바뀌기 때문이다. 조회는 이미 끝나 있어 할당이 없다.
    /// </summary>
    Bounds ResolveBounds()
    {
        var root = _boundsRoot != null ? _boundsRoot : _headFallback;
        if (root == null) return new Bounds(transform.position, Vector3.zero);

        var found = false;
        var bounds = new Bounds(root.position, Vector3.zero);

        foreach (var renderer in _renderers)
        {
            if (!IsDrawn(renderer)) continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        if (found) return bounds;

        // 모델이 없거나 전부 꺼져 있으면 머리 트랜스폼 위 고정 오프셋으로 대신한다.
        var anchor = _headFallback != null ? _headFallback : root;
        return new Bounds(anchor.position + Vector3.up * (0.15f * _worldScale), Vector3.zero);
    }

    /// <summary>
    /// 화자 모델이 없을 때(내레이션·컷씬) 쓰는 패널. 카메라 정면에 고정 거리로 띄우되 고개를
    /// 조금 돌린 정도로는 따라오지 않는다 — 시야에 붙어 다니는 UI는 VR 멀미의 대표적 원인이다.
    /// </summary>
    void PlaceInFront(Camera camera)
    {
        var scale = ResolveWorldScale(camera);
        var cameraTransform = camera.transform;

        var desired = cameraTransform.forward;
        desired.y = 0f;

        // 정확히 수직으로 올려다보거나 내려다보면 수평 성분이 사라진다. 그때는 카메라 up이
        // 몸이 향한 쪽을 가리키므로 그것을 쓴다 — 아래를 볼 때는 up이 정면, 위를 볼 때는 뒤쪽이라
        // 피치 부호로 뒤집는다.
        if (desired.sqrMagnitude < 0.0001f)
        {
            desired = -cameraTransform.up * Mathf.Sign(cameraTransform.forward.y);
            desired.y = 0f;
        }

        if (desired.sqrMagnitude < 0.0001f) desired = Vector3.forward;
        desired.Normalize();

        if (!_fallbackInitialized)
        {
            _fallbackDirection = desired;
            _fallbackInitialized = true;
        }
        else
        {
            var offAngle = Vector3.Angle(_fallbackDirection, desired);
            if (offAngle > FallbackDeadZoneDegrees)
            {
                // 데드존을 넘은 만큼만 좁힌다. 데드존 경계에서 속도가 0이라 튀지 않는다.
                var blend = 1f - Mathf.Exp(-FallbackFollowSharpness * Time.unscaledDeltaTime);
                var target = Vector3.Slerp(_fallbackDirection, desired, blend);
                _fallbackDirection = target.sqrMagnitude > 0.0001f ? target.normalized : desired;
            }
        }

        var distance = FallbackDistanceFactor * scale;
        transform.position = cameraTransform.position
                             + _fallbackDirection * distance
                             - Vector3.up * (FallbackDropFactor * scale);
    }

    /// <summary>
    /// 거리에 비례해 스케일을 잡아 화면에서 차지하는 각도를 일정하게 만든다. 월드 배율이 얼마든
    /// 결과가 같은 이유가 여기 있다 — 거리와 크기가 같은 배율로 함께 커지므로 비율이 불변이다.
    /// </summary>
    void ApplyDistanceScale(float distance)
    {
        var scale = _worldScale;
        var clamped = Mathf.Clamp(distance, MinDistanceFactor * scale, MaxDistanceFactor * scale);

        // 폭 ReferenceWidth 픽셀이 AngularWidthDegrees를 차지하도록 픽셀당 월드 크기를 정한다.
        var widthAtUnitDistance = 2f * Mathf.Tan(AngularWidthDegrees * 0.5f * Mathf.Deg2Rad);
        var unitsPerPixel = widthAtUnitDistance / ReferenceWidth;

        var value = clamped * unitsPerPixel;
        transform.localScale = new Vector3(value, value, value);
    }

    /// <summary>
    /// 씬의 월드 배율. 화자를 잡고 있으면 그 크기에서, 아니면 카메라 리그의 스케일에서 얻는다.
    /// Play 씬처럼 리그째로 ×20 된 월드에서 폴백 패널이 코앞이나 지평선에 뜨는 것을 막는다.
    /// </summary>
    float ResolveWorldScale(Camera camera)
    {
        if (_boundsRoot != null) return _worldScale;

        var rigScale = camera != null ? camera.transform.lossyScale.y : 1f;
        _worldScale = rigScale > 0.0001f ? rigScale : 1f;
        return _worldScale;
    }

    /// <summary>
    /// 지금 실제로 그려지는 렌더러인지. <c>Renderer</c>는 <c>Behaviour</c>가 아니라
    /// <c>isActiveAndEnabled</c>가 없고, 꺼진 오브젝트 위의 렌더러도 <c>enabled</c>는 참으로
    /// 남는다. 그것까지 세면 숨겨 둔 변형 모델이나 치워 둔 소품이 바운즈에 들어가 말풍선이
    /// 보이지 않는 무언가 위에 뜬다.
    /// </summary>
    static bool IsDrawn(Renderer renderer) =>
        renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;

    /// <summary>
    /// 화자 키를 사람 키로 나눠 씬의 월드 배율을 추정한다. 자막 크기와 오프셋을 모두 이 값의
    /// 배수로 표현하면 ×1 씬과 ×20 씬에서 같은 결과가 나온다 — 배율을 상수로 박아 두면 한쪽에서
    /// 반드시 어긋난다.
    /// </summary>
    float EstimateWorldScale()
    {
        var height = 0f;

        foreach (var renderer in _renderers)
        {
            if (!IsDrawn(renderer)) continue;
            height = Mathf.Max(height, renderer.bounds.size.y);
        }

        if (height > 0.0001f) return Mathf.Clamp(height / HumanHeightMeters, 0.01f, 1000f);

        var lossy = _boundsRoot != null ? _boundsRoot.lossyScale.y : 1f;
        return lossy > 0.0001f ? lossy : 1f;
    }

    /// <summary>
    /// <c>ScreenFader.ResolveCamera</c>와 같은 규칙. 컷씬이 주 카메라를 끄면 <see cref="Camera.main"/>이
    /// null이 되므로, 실제로 그리고 있는 카메라로 폴백한다.
    /// </summary>
    static Camera ResolveCamera()
    {
        var main = Camera.main;
        if (main != null) return main;

        var cameras = Camera.allCameras;
        Camera last = null;
        for (var i = 0; i < cameras.Length; i++)
            if (last == null || cameras[i].depth > last.depth)
                last = cameras[i];

        return last;
    }

    /// <summary>
    /// 우리가 만든 머티리얼 인스턴스만 파괴한다. <c>Graphic.material</c>을 그대로 읽어 파괴하면,
    /// 인스턴스 생성이 실패한 경우 엔진의 공용 캔버스 머티리얼을 파괴해 빌드 전체의 UI가 깨진다.
    /// </summary>
    void OnDestroy()
    {
        if (_panelMaterial != null) Destroy(_panelMaterial);
        if (_tailMaterial != null) Destroy(_tailMaterial);
    }
}
