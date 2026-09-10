using TMPro;
using UnityEngine;

/// <summary>
/// 월드 말풍선이 쓸 한글 가능 TMP 폰트를 런타임에 고른다.
///
/// 프로젝트 사정 때문에 "그냥 참조하면 되는" 경로가 없다. 한글이 되는 폰트는
/// <c>Assets/@Developers/RYU/Quest/UI/Fonts/</c> 아래에만 있고 <c>Resources</c> 폴더 밖이라
/// <see cref="Resources.Load"/>로 잡히지 않으며, 말풍선은 씬·프리팹을 건드리지 않고 코드로만
/// 세우므로 인스펙터 참조도 걸 수 없다. 그래서 이미 메모리에 올라온 폰트를 훑어 고른다.
///
/// TMP 기본값에 기대면 안 된다: <c>TMP Settings.asset</c>의 기본 폰트는 LiberationSans SDF이고
/// <c>m_fallbackFontAssets</c>가 비어 있어, 폰트를 지정하지 않으면 한글이 통째로 네모가 된다.
/// 그래서 후보마다 실제로 한글 글리프를 낼 수 있는지(<see cref="TMP_FontAsset.HasCharacter"/>)
/// 확인하고, 아무것도 못 찾으면 OS 폰트로 동적 에셋을 직접 만든다.
///
/// 정적 아틀라스보다 동적 아틀라스를 앞에 둔다. <c>Giants-Bold SDF.asset</c>(정적)은 아틀라스가
/// 넘쳐 한글 30여 자가 빠져 있고, 그 사고 때문에 <c>Giants-Bold Dynamic SDF.asset</c>(동적)이
/// 따로 만들어졌다 — 같은 함정을 다시 밟지 않도록 점수에 반영한다.
/// </summary>
public static class DialogueBubbleFont
{
    /// <summary>한글 렌더링 가능 여부를 판정할 때 쓰는 표본. 흔한 음절 위주로 고른다.</summary>
    const string KoreanProbe = "이음가나";

    /// <summary>이름에 이 조각이 들어간 폰트를 가장 먼저 고른다. 프로젝트 지정 서체다.</summary>
    const string PreferredNameFragment = "Giants";

    /// <summary>Resources에 폰트를 떨어뜨려 지정하고 싶을 때 쓰는 경로. 없으면 그냥 지나간다.</summary>
    const string ResourcesPath = "Fonts/DialogueBubbleFont";

    /// <summary>
    /// OS에서 찾아볼 한글 서체 이름. 앞에서부터 시도한다 — 에디터(Windows)는 맑은 고딕,
    /// Quest(Android)는 Noto Sans CJK 계열이 잡힌다.
    /// </summary>
    static readonly string[] OsKoreanFamilies =
    {
        "Noto Sans CJK KR", "Noto Sans KR", "NotoSansCJK", "NanumGothic", "Nanum Gothic",
        "Malgun Gothic", "맑은 고딕", "Gulim", "Batang", "Dotum", "Apple SD Gothic Neo"
    };

    static TMP_FontAsset _resolved;
    static TMP_FontAsset _override;
    static bool _warned;

    /// <summary>
    /// 폰트를 밖에서 지정한다. 인스펙터 참조를 걸 수 없는 구조라 남겨 둔 주입 지점이며,
    /// 씬에 확정 폰트가 생기면 여기에 한 줄 넣는 것으로 자동 탐색을 대체할 수 있다.
    /// </summary>
    public static TMP_FontAsset Override
    {
        get => _override;
        set
        {
            _override = value;
            if (value != null) _resolved = value;
        }
    }

    /// <summary>
    /// 지금까지 고른 폰트. 아직 고르지 않았으면 null이며, 이 프로퍼티는 선택을 유발하지 않는다.
    /// 디버그 표시처럼 매 프레임 도는 곳에서 <see cref="Resolve"/>를 부르면 아직 씬이 자리 잡기
    /// 전에 선택이 굳는다.
    /// </summary>
    public static TMP_FontAsset Current => _override != null ? _override : _resolved;

    /// <summary>
    /// 캐시를 버리고 다음 <see cref="Resolve"/>에서 다시 고르게 한다. 씬이 바뀌면 지정 서체가
    /// 그제서야 메모리에 올라오는 경우가 있어서다 — 폰트를 잡는 유일한 수단이 "이미 로드된
    /// 것 훑기"이므로, 언제 훑느냐에 따라 결과가 달라진다.
    ///
    /// 이미 지정 서체를 잡았으면 아무것도 하지 않는다. 그보다 나은 후보는 없으므로 다시 훑는
    /// 비용만 든다.
    /// </summary>
    public static void Invalidate()
    {
        if (_override != null || IsPreferred(_resolved)) return;

        _resolved = null;
        _warned = false;
    }

    static bool IsPreferred(TMP_FontAsset font) =>
        font != null &&
        font.name.IndexOf(PreferredNameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// 고른 폰트. 한 번 정해지면 <see cref="Invalidate"/>가 있을 때까지 캐시한다 — 마지막 수단인
    /// 기본 폰트로 내려간 경우도 마찬가지다. 매번 다시 고르게 하면 한글 폰트가 없는 프로젝트에서
    /// 자막 한 줄마다 로드된 오브젝트 전수 조회와 OS 폰트 열거가 돈다.
    /// </summary>
    public static TMP_FontAsset Resolve()
    {
        if (_override != null) return _override;
        if (_resolved != null) return _resolved;

        _resolved = FindLoaded() ?? FromResources() ?? FromLoadedTtf() ?? FromOperatingSystem();

        if (_resolved != null)
        {
            Debug.Log($"[Subtitle] 말풍선 폰트로 '{_resolved.name}'를 씁니다.");
            return _resolved;
        }

        // 마지막 수단. 한글은 깨지지만 자막 자체가 사라지는 것보다는 낫고, 로그로 원인이 남는다.
        _resolved = TMP_Settings.defaultFontAsset;
        if (!_warned)
        {
            _warned = true;
            Debug.LogWarning(
                "[Subtitle] 한글을 낼 수 있는 TMP 폰트를 찾지 못해 TMP 기본 폰트로 내려갑니다. " +
                $"한글이 네모로 보이면 DialogueBubbleFont.Override에 폰트를 지정하거나 " +
                $"Resources/{ResourcesPath}에 한글 TMP 폰트를 두십시오.");
        }

        return _resolved;
    }

    /// <summary>도메인 리로드를 끈 채 플레이에 들어가면 이전 세션의 런타임 폰트가 남는다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        _resolved = null;
        _override = null;
        _warned = false;
    }

    /// <summary>
    /// 이미 로드된 TMP 폰트 중 가장 점수가 높은 것. 씬이 물고 온 폰트를 그대로 쓰는 경로라
    /// 추가 로드가 없고, Play 씬은 QuestBoard 프리팹이 Giants 동적 폰트를 이미 올려 둔다.
    /// </summary>
    static TMP_FontAsset FindLoaded()
    {
        var candidates = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        TMP_FontAsset best = null;
        var bestScore = 0;

        foreach (var candidate in candidates)
        {
            var score = Score(candidate);
            if (score <= bestScore) continue;

            best = candidate;
            bestScore = score;
        }

        return best;
    }

    /// <summary>
    /// 후보 점수. 0이면 쓰지 않는다.
    ///
    /// 여기서 <b>글리프를 굽지 않는</b> 것이 중요하다. <c>HasCharacter</c>에 <c>tryAddCharacter</c>를
    /// 켜면 TMP가 그 자리에서 아틀라스에 글자를 새겨 넣는데, 후보는 대부분 프로젝트의 온디스크
    /// 에셋이라 폰트를 고르기만 해도 공용 자산이 수정된 상태가 된다. 자막이 폰트를 고르는 행위가
    /// 에셋을 더럽혀서는 안 된다.
    ///
    /// 그래서 동적 아틀라스는 굽지 않고 통과시킨다 — 어차피 필요한 글자는 렌더 시점에 알아서
    /// 추가된다. 정적 아틀라스만 이미 구워진 글자로 판정하며, 이 규칙이
    /// <c>Giants-Bold SDF.asset</c>(정적, 한글 30여 자 누락)을 정확히 걸러 낸다.
    /// </summary>
    static int Score(TMP_FontAsset font)
    {
        if (font == null) return 0;

        var dynamic = font.atlasPopulationMode is AtlasPopulationMode.Dynamic or AtlasPopulationMode.DynamicOS;
        var baked = HasBakedKorean(font);

        // 정적인데 한글이 안 구워져 있으면 그 글자는 영원히 네모다.
        if (!dynamic && !baked) return 0;

        var score = dynamic ? baked ? 4 : 2 : 3;
        if (IsPreferred(font)) score += 8;
        return score;
    }

    static TMP_FontAsset FromResources()
    {
        var font = Resources.Load<TMP_FontAsset>(ResourcesPath);
        return font != null && Score(font) > 0 ? font : null;
    }

    /// <summary>
    /// 로드된 TTF에서 동적 폰트를 만든다. StartScene처럼 TMP 에셋 없이 원본 폰트만 물고 있는
    /// 씬을 위한 경로다. <c>FixedUIStartMenuAdapter</c>가 쓰는 것과 같은 방법이다.
    /// </summary>
    static TMP_FontAsset FromLoadedTtf()
    {
        var fonts = Resources.FindObjectsOfTypeAll<Font>();
        Font best = null;

        foreach (var font in fonts)
        {
            if (font == null || string.IsNullOrEmpty(font.name)) continue;
            if (font.name.IndexOf(PreferredNameFragment, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            best = font;
            break;
        }

        return best != null ? Create(() => TMP_FontAsset.CreateFontAsset(best), best.name) : null;
    }

    /// <summary>
    /// OS 설치 폰트로 동적 에셋을 만든다. 프로젝트 서체가 씬에 하나도 없어도 한글이 깨지지 않게
    /// 하는 안전망이며, 서체가 프로젝트 지정과 달라진다는 점만 감수한다.
    /// </summary>
    static TMP_FontAsset FromOperatingSystem()
    {
        string[] installed;
        try
        {
            installed = Font.GetOSInstalledFontNames();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Subtitle] OS 폰트 목록을 읽지 못했습니다: {exception.Message}");
            return null;
        }

        foreach (var family in OsKoreanFamilies)
        {
            // 설치 목록에 흔적이 없으면 건너뛴다. TMP가 실패할 때마다 로그를 남기므로,
            // 후보 전부를 무작정 두드리면 콘솔이 "찾을 수 없음"으로 뒤덮인다.
            if (installed != null && installed.Length > 0 && !IsInstalled(installed, family)) continue;

            // 스타일 이름은 플랫폼마다 다르게 붙는다. 표준 이름과 빈 문자열을 모두 시도한다.
            var font = Create(() => TMP_FontAsset.CreateFontAsset(family, "Regular", 90), family)
                       ?? Create(() => TMP_FontAsset.CreateFontAsset(family, string.Empty, 90), family);

            if (font != null) return font;
        }

        return null;
    }

    static bool IsInstalled(string[] installed, string family)
    {
        foreach (var name in installed)
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (name.IndexOf(family, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    /// <summary>생성 실패와 "만들었지만 한글이 안 되는" 경우를 한 곳에서 걸러 낸다.</summary>
    static TMP_FontAsset Create(System.Func<TMP_FontAsset> factory, string label)
    {
        TMP_FontAsset font;
        try
        {
            font = factory();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Subtitle] '{label}'로 폰트를 만들지 못했습니다: {exception.Message}");
            return null;
        }

        if (font == null) return null;

        // 우리가 방금 만든 폰트다. 여기서는 글리프를 구워 봐도 더럽힐 프로젝트 에셋이 없고,
        // 갓 만든 동적 아틀라스는 비어 있으므로 실제로 구워 봐야 원본 폰트에 한글이 있는지 안다.
        if (!CanBakeKorean(font))
        {
            Discard(font);
            return null;
        }

        font.name = $"DialogueBubble ({label})";

        // 씬이 바뀌어도 살아남아야 한다. 자막은 씬 경계를 넘어 이어지는 시스템이다.
        font.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return font;
    }

    /// <summary>
    /// 런타임에 만든 폰트를 버린다. 폰트 에셋은 아틀라스 텍스처와 머티리얼을 딸려 만들기 때문에
    /// 에셋만 파괴하면 그 둘이 메모리에 남는다 — OS 폰트 후보를 여럿 두드리는 경로에서 한 번에
    /// 1024×1024 텍스처씩 새기 때문에 무시할 양이 아니다.
    /// </summary>
    static void Discard(TMP_FontAsset font)
    {
        if (font == null) return;

        if (font.material != null) Object.Destroy(font.material);
        if (font.atlasTexture != null) Object.Destroy(font.atlasTexture);
        Object.Destroy(font);
    }

    /// <summary>이미 아틀라스에 구워진 글자만으로 한글이 되는지. 아무것도 바꾸지 않는다.</summary>
    static bool HasBakedKorean(TMP_FontAsset font)
    {
        if (font == null) return false;

        foreach (var character in KoreanProbe)
            if (!font.HasCharacter(character, true, false))
                return false;

        return true;
    }

    /// <summary>
    /// 실제로 글리프를 추가해 보고 한글이 되는지 확인한다. <b>우리가 만든 폰트에만</b> 쓴다 —
    /// 프로젝트 에셋에 쓰면 아틀라스가 수정된다.
    /// </summary>
    static bool CanBakeKorean(TMP_FontAsset font)
    {
        if (font == null) return false;

        foreach (var character in KoreanProbe)
            if (!font.HasCharacter(character, true, true))
                return false;

        return true;
    }
}
