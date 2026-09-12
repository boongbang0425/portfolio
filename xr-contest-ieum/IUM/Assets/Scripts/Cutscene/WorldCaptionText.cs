using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 카메라 앞에 고정 거리로 뜨는 월드 공간 문구. 컷씬의 마지막 문구처럼 화자가 없는 글을 VR에서
/// 보이게 하려고 만든다.
///
/// 왜 필요한가 — <c>CutsceneContext.SetCaption</c>은 화면 공간(UIToolkit) 캡션으로 간다. VR에서는
/// 화면 공간 UI가 아예 렌더되지 않아 HMD에서 한 글자도 보이지 않는다. 화자 대사는
/// <see cref="WorldSubtitleDirector"/>가 이미 월드로 옮겼지만 캡션 채널은 그 경로를 타지 않는다.
///
/// 설계는 <see cref="WorldSubtitleBubble"/>을 그대로 따른다. 크기를 월드 단위로 박지 않고
/// <b>시야각</b>으로 정해(캔버스는 고정 픽셀, 루트 스케일은 거리에 비례) 씬의 월드 배율이 ×1이든
/// ×20이든 화면에서 같은 크기로 보이게 한다. 폰트도 같은 <see cref="DialogueBubbleFont"/>로
/// 해석해 한글이 네모로 깨지지 않게 한다.
///
/// 씬·프리팹을 만들지 않는다. 계층 전체를 코드로 세우고, 만든 쪽이 <see cref="Create"/>에 넘긴
/// 씬에 소속시켜 그 씬이 언로드될 때 함께 사라진다.
/// </summary>
public sealed class WorldCaptionText : MonoBehaviour
{
    /// <summary>캔버스 기준 픽셀 폭. 실제 크기는 루트 스케일이 정하므로 이 값은 해상도일 뿐이다.</summary>
    const float ReferenceWidth = 1200f;
    const float ReferenceHeight = 420f;

    /// <summary>본문 최대 폭(px). 이 폭에서 줄바꿈이 일어난다.</summary>
    const float MaxTextWidth = 1120f;

    const float FontSize = 62f;

    /// <summary>
    /// 문구가 화면에서 차지할 가로 시야각. 거리 스케일의 기준이다. 한 줄 높이는
    /// <see cref="AngularWidthDegrees"/> × <see cref="FontSize"/> ÷ <see cref="ReferenceWidth"/>
    /// ≈ 1.7°로, VR에서 편하게 읽히는 자막 높이(1.5~2°) 구간이다.
    /// </summary>
    const float AngularWidthDegrees = 32f;

    /// <summary>글자 외곽선. 밝은 하늘 위에서도 읽히게 하는 유일한 보험이다.</summary>
    const float OutlineWidth = 0.14f;

    /// <summary>CompareFunction.Always. UnityEngine.Rendering을 끌어오지 않으려고 값으로 둔다.</summary>
    const float AlwaysZTest = 8f;

    /// <summary>시선 중심에서 내려 앉는 비율(배치 거리 대비). 건물 정면을 가리지 않도록.</summary>
    const float DropRatio = 0.16f;

    RectTransform _root;
    Canvas _canvas;
    CanvasGroup _group;
    TextMeshProUGUI _text;

    readonly List<TMP_SubMeshUI> _subMeshes = new();

    Camera _camera;
    float _distance = 3f;
    float _fadeSeconds = 1.2f;

    float _alpha;
    float _targetAlpha;

    public bool IsVisible => _targetAlpha > 0.001f;

    /// <summary>
    /// 문구 오브젝트를 만든다. <paramref name="owner"/>가 속한 씬에 넣어 그 씬과 수명을 같이한다 —
    /// 컷씬 씬이 언로드될 때 남지 않게 하려는 것이다. 부모로 붙이지 않는 이유는
    /// <see cref="WorldSubtitleBubble"/>과 같다: 부모의 스케일을 물려받으면 거리 스케일 계산이
    /// 무너진다.
    /// </summary>
    public static WorldCaptionText Create(GameObject owner, string name)
    {
        var host = new GameObject(name, typeof(RectTransform));

        if (owner != null && owner.scene.IsValid())
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, owner.scene);

        var caption = host.AddComponent<WorldCaptionText>();
        caption.Build();
        return caption;
    }

    /// <summary>표시에 쓸 카메라. 비우면 <see cref="Camera.main"/>과 활성 카메라 폴백으로 간다.</summary>
    public void SetCamera(Camera camera) => _camera = camera;

    /// <summary>
    /// 카메라 앞 배치 거리(월드 단위). 각도로 크기를 잡으므로 이 값은 보이는 크기가 아니라
    /// "무엇보다 앞에 놓을지"만 정한다 — 비추는 대상보다 앞이고 근평면보다 뒤면 된다.
    /// </summary>
    public void SetDistance(float distance) => _distance = Mathf.Max(0.01f, distance);

    public void SetFadeSeconds(float seconds) => _fadeSeconds = Mathf.Max(0.01f, seconds);

    public void Show(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Hide();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // 폰트는 지금 고른다. 이 오브젝트는 컷씬 씬이 올라온 뒤에 생기므로 프로젝트 서체가
        // 이미 메모리에 있고, 미리 골라 두면 OS 폰트가 캐시돼 눌러앉는다.
        ApplyFont(DialogueBubbleFont.Resolve());

        _text.text = text;
        Layout(text);
        ApplySubMeshZTest();

        _targetAlpha = 1f;

        // 첫 프레임을 기다리지 않고 지금 배치한다. 한 프레임이라도 원점에 뜨면 눈에 띈다.
        Place();
    }

    public void Hide() => _targetAlpha = 0f;

    // ---- 계층 ----

    void Build()
    {
        _root = (RectTransform)transform;
        _root.localPosition = Vector3.zero;
        _root.localRotation = Quaternion.identity;
        _root.localScale = Vector3.one;
        _root.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
        _root.pivot = new Vector2(0.5f, 0.5f);

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 100;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // GraphicRaycaster를 붙이지 않는다. 문구는 조작 대상이 아니고, 있으면 월드 포인터가 집는다.

        BuildText();
        gameObject.SetActive(false);
    }

    void BuildText()
    {
        var host = new GameObject("Text", typeof(RectTransform));
        var rect = (RectTransform)host.transform;
        rect.SetParent(_root, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _text = host.AddComponent<TextMeshProUGUI>();
        _text.raycastTarget = false;
        _text.fontSize = FontSize;
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.richText = false;
        _text.color = Color.white;

        ApplyFont(null);
    }

    /// <summary>
    /// 폰트를 갈아 끼우고 글자 머티리얼 설정을 다시 얹는다. <see cref="WorldSubtitleBubble"/>과
    /// 같은 이유로 <c>TMP_Text.outlineWidth</c> 프로퍼티를 쓰지 않는다 — 폰트를 바꿔도 캐시된
    /// 필드가 그대로라 두 번째 호출부터 조용히 무시된다.
    /// </summary>
    void ApplyFont(TMP_FontAsset font)
    {
        if (font != null && _text.font != font) _text.font = font;

        ShaderUtilities.GetShaderPropertyIDs();

        var material = _text.fontMaterial;
        if (material == null) return;

        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.85f));
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
        material.SetFloat(ShaderUtilities.ShaderTag_ZTestMode, AlwaysZTest);

        _text.UpdateMeshPadding();
    }

    /// <summary>
    /// 폴백 글리프와 넘침 아틀라스는 TMP가 서브메시로 따로 그린다. 그 머티리얼에도 같은 설정을
    /// 얹지 않으면 한글 일부만 건물 뒤로 사라진다.
    /// </summary>
    void ApplySubMeshZTest()
    {
        _text.ForceMeshUpdate();

        _subMeshes.Clear();
        _text.GetComponentsInChildren(true, _subMeshes);

        foreach (var subMesh in _subMeshes)
        {
            if (subMesh == null) continue;

            var material = subMesh.material;
            if (material == null) continue;

            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.85f));
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            material.SetFloat(ShaderUtilities.ShaderTag_ZTestMode, AlwaysZTest);
        }
    }

    void Layout(string text)
    {
        var preferred = _text.GetPreferredValues(text, MaxTextWidth, 0f);
        var height = Mathf.Max(preferred.y, FontSize * 1.4f);
        _root.sizeDelta = new Vector2(ReferenceWidth, height);
    }

    // ---- 배치 ----

    void LateUpdate()
    {
        StepFade();
        if (_alpha <= 0.001f && _targetAlpha <= 0.001f) return;

        Place();
    }

    /// <summary>
    /// unscaledDeltaTime이라 일시정지 중에도 페이드가 끝까지 간다. 반투명하게 얼어붙는 것보다
    /// 낫다는 <see cref="WorldSubtitleBubble"/>의 판단을 그대로 따른다.
    /// </summary>
    void StepFade()
    {
        var step = Time.unscaledDeltaTime / _fadeSeconds;
        _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, step);
        _group.alpha = _alpha;

        if (_alpha > 0.001f || _targetAlpha > 0.001f || !gameObject.activeSelf) return;

        gameObject.SetActive(false);
    }

    void Place()
    {
        var camera = ResolveCamera();
        if (camera == null) return;

        var cameraTransform = camera.transform;
        var cameraPosition = cameraTransform.position;

        // 시선 정면에 그대로 건다. 컷씬 카메라는 연출이 정한 대로만 움직이므로 데드존을 둘
        // 이유가 없다 — 플레이어 고개를 따라가는 UI가 아니다.
        transform.position = cameraPosition
                             + cameraTransform.forward * _distance
                             - Vector3.up * (_distance * DropRatio);

        // 빌보드. 캔버스의 +Z는 보는 쪽의 반대를 향해야 글자가 바로 보인다. 롤은 월드 up으로
        // 고정해 HMD를 기울여도 문구가 같이 기울지 않게 한다.
        var away = transform.position - cameraPosition;
        if (away.sqrMagnitude > 0.0000001f)
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);

        ApplyDistanceScale(away.magnitude);
    }

    /// <summary>
    /// 거리에 비례해 스케일을 잡아 화면에서 차지하는 각도를 일정하게 만든다. 월드 배율이 얼마든
    /// 결과가 같은 이유가 여기 있다.
    /// </summary>
    void ApplyDistanceScale(float distance)
    {
        var widthAtUnitDistance = 2f * Mathf.Tan(AngularWidthDegrees * 0.5f * Mathf.Deg2Rad);
        var unitsPerPixel = widthAtUnitDistance / ReferenceWidth;

        var value = Mathf.Max(distance, 0.01f) * unitsPerPixel;
        transform.localScale = new Vector3(value, value, value);
    }

    /// <summary>
    /// 지정 카메라 → <see cref="Camera.main"/> → 가장 앞에 그리는 활성 카메라. 컷씬이 주 카메라를
    /// 끄면 <c>Camera.main</c>이 null이 되므로 <c>ScreenFader.ResolveCamera</c>와 같은 폴백을 둔다.
    /// </summary>
    Camera ResolveCamera()
    {
        if (_camera != null && _camera.isActiveAndEnabled) return _camera;

        var main = Camera.main;
        if (main != null) return main;

        var cameras = Camera.allCameras;
        Camera best = null;
        for (var i = 0; i < cameras.Length; i++)
            if (best == null || cameras[i].depth > best.depth)
                best = cameras[i];

        return best;
    }
}
