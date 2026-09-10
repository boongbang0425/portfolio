using UnityEngine;

/// <summary>
/// 말풍선 배경에 쓰는 스프라이트를 코드로 굽는다.
///
/// 이미지 에셋을 만들지 않는 이유는 이 시스템 전체가 씬·프리팹·에셋을 새로 만들지 않는다는
/// 전제 위에 서 있기 때문이다. 라운드 사각형과 삼각형 정도는 몇 줄의 픽셀 계산으로 끝나고,
/// 아틀라스에 들어가지 않는 작은 텍스처 두 장은 VR에서도 문제가 되지 않는다.
///
/// 텍스처는 한 번 만들어 캐시하고 <see cref="HideFlags.DontUnloadUnusedAsset"/>로 씬 경계를
/// 넘긴다 — 자막은 씬이 바뀌어도 이어지는 시스템이다.
/// </summary>
public static class BubbleSprites
{
    const int RoundedSize = 64;
    const int RoundedRadius = 24;
    const int TriangleSize = 48;

    static Sprite _rounded;
    static Sprite _triangle;

    /// <summary>9-슬라이스 라운드 사각형. 모서리 반경은 늘어나지 않고 가운데만 늘어난다.</summary>
    public static Sprite RoundedRect()
    {
        if (_rounded != null) return _rounded;

        var texture = CreateTexture(RoundedSize, RoundedSize, "BubbleRoundedRect");
        var pixels = new Color32[RoundedSize * RoundedSize];

        for (var y = 0; y < RoundedSize; y++)
        for (var x = 0; x < RoundedSize; x++)
            pixels[y * RoundedSize + x] = new Color32(255, 255, 255, RoundedAlpha(x, y));

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        var border = new Vector4(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius);
        _rounded = Sprite.Create(
            texture,
            new Rect(0f, 0f, RoundedSize, RoundedSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);

        _rounded.name = "BubbleRoundedRect";
        _rounded.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return _rounded;
    }

    /// <summary>아래를 가리키는 꼬리. 꼭짓점이 화자 머리 쪽을 향한다.</summary>
    public static Sprite DownTriangle()
    {
        if (_triangle != null) return _triangle;

        var texture = CreateTexture(TriangleSize, TriangleSize, "BubbleTail");
        var pixels = new Color32[TriangleSize * TriangleSize];

        for (var y = 0; y < TriangleSize; y++)
        for (var x = 0; x < TriangleSize; x++)
            pixels[y * TriangleSize + x] = new Color32(255, 255, 255, TriangleAlpha(x, y));

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        _triangle = Sprite.Create(
            texture,
            new Rect(0f, 0f, TriangleSize, TriangleSize),
            new Vector2(0.5f, 0f),
            100f,
            0,
            SpriteMeshType.FullRect);

        _triangle.name = "BubbleTail";
        _triangle.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return _triangle;
    }

    static Texture2D CreateTexture(int width, int height, string name)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = name,
            filterMode = FilterMode.Bilinear,

            // 슬라이스 경계에서 반대편 픽셀이 새어 들어오면 모서리에 실선이 생긴다.
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };

        return texture;
    }

    /// <summary>모서리 안쪽 거리로 알파를 구한다. 1픽셀 폭으로 부드럽게 떨어뜨려 계단을 없앤다.</summary>
    static byte RoundedAlpha(int x, int y)
    {
        // 픽셀 중심 기준. 각 모서리의 원 중심에서 얼마나 벗어났는지만 보면 된다.
        var px = x + 0.5f;
        var py = y + 0.5f;

        var cx = Mathf.Clamp(px, RoundedRadius, RoundedSize - RoundedRadius);
        var cy = Mathf.Clamp(py, RoundedRadius, RoundedSize - RoundedRadius);

        var distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        var coverage = Mathf.Clamp01(RoundedRadius - distance + 0.5f);
        return (byte)Mathf.RoundToInt(coverage * 255f);
    }

    /// <summary>위쪽 변이 가득 차고 아래 꼭짓점으로 좁아지는 이등변삼각형.</summary>
    static byte TriangleAlpha(int x, int y)
    {
        var px = (x + 0.5f) / TriangleSize;
        var py = (y + 0.5f) / TriangleSize;

        // y=0(아래)에서 폭 0, y=1(위)에서 폭 1. 중앙에서의 허용 반경과 비교한다.
        var halfWidth = py * 0.5f;
        var offset = Mathf.Abs(px - 0.5f);

        var coverage = Mathf.Clamp01((halfWidth - offset) * TriangleSize + 0.5f);
        return (byte)Mathf.RoundToInt(coverage * 255f);
    }
}
