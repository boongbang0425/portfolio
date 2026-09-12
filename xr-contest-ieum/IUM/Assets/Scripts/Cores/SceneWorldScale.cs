using UnityEngine;

/// <summary>
/// 지금 열려 있는 씬의 월드 배율(1미터가 몇 유닛인가)을 한 곳에서 돌려준다.
///
/// <see cref="Player.WorldUnitsPerMetre"/>가 유일한 근거다. 리그 <c>lossyScale</c>로 추정하면
/// Play 씬처럼 <b>플레이어 리그는 ×1인데 월드 지오메트리만 ×12로 저작된</b> 씬에서 1이 나와
/// 미터 저작 상수가 전부 1/12로 줄어든다 — 메뉴가 눈앞 11cm에 뜨고 3D 감쇠가 8cm에서 시작하는
/// 증상이 전부 여기서 나온다.
///
/// 미터 저작 상수를 쓰는 쪽(월드 메뉴 거리, AudioSource 감쇠 반경, 말풍선 폴백 거리)이
/// <b>사용 지점에서</b> 이 값을 곱한다. ×1 씬(GongpoScene·StartScene·컷씬 씬)은 1이 나오므로
/// 곱해도 결과가 바뀌지 않는다.
/// </summary>
public static class SceneWorldScale
{
    /// <summary>플레이어를 못 찾았을 때 재탐색 간격. 매 프레임 Find를 도는 것을 막는다.</summary>
    const float LookupIntervalSeconds = 0.5f;

    static Player _player;
    static float _cached = 1f;
    static float _nextLookup;

    /// <summary>
    /// 도메인 리로드를 끈 플레이 모드에서는 정적 필드가 이전 세션의 플레이어를 물고 남는다.
    /// 진입 시점에 비워 두지 않으면 ×1 씬에서 이전 세션의 ×12가 그대로 적용된다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _player = null;
        _cached = 1f;
        _nextLookup = 0f;
    }

    /// <summary>
    /// 이 씬의 1미터에 해당하는 월드 유닛. 플레이어가 없는 씬(컷씬 전용 씬 등)에서는 1이다.
    /// </summary>
    public static float Current
    {
        get
        {
            TryResolve(out var scale);
            return scale;
        }
    }

    /// <summary>
    /// 씬이 실제로 배율을 선언했는지까지 알아야 하는 호출자용. 플레이어가 없으면 false를 돌려주고
    /// <paramref name="scale"/>에는 안전한 기본값 1을 넣는다 — 폴백 로직을 따로 태울 수 있게 한다.
    /// </summary>
    public static bool TryResolve(out float scale)
    {
        if (_player == null)
        {
            // Time.unscaledTime은 일시정지 중에도 흐른다. 일시정지 메뉴가 열리는 순간 배율을 묻는
            // 경로가 있어 timeScale에 묶인 시계를 쓰면 재탐색이 영영 돌지 않는다.
            if (Time.unscaledTime >= _nextLookup)
            {
                _nextLookup = Time.unscaledTime + LookupIntervalSeconds;
                _player = Object.FindAnyObjectByType<Player>(FindObjectsInactive.Include);
                _cached = _player != null ? Mathf.Max(0.0001f, _player.WorldUnitsPerMetre) : 1f;
            }
        }
        else
        {
            _cached = Mathf.Max(0.0001f, _player.WorldUnitsPerMetre);
        }

        scale = _cached;
        return _player != null;
    }

    /// <summary>
    /// 3D AudioSource의 감쇠 반경을 이 씬의 단위로 맞춘다. 미터 저작값을 그대로 넣으면 ×12 씬에서
    /// minDistance 1유닛(=8cm)부터 감쇠가 시작돼 손에 든 도구 소리조차 거의 들리지 않는다.
    /// </summary>
    /// <param name="minDistanceMetres">이 거리 안에서는 감쇠하지 않는다(미터).</param>
    /// <param name="maxDistanceMetres">Linear 롤오프가 0에 닿는 거리(미터).</param>
    public static void Configure3D(AudioSource source, float minDistanceMetres, float maxDistanceMetres)
    {
        if (source == null) return;

        var scale = Current;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = Mathf.Max(0.01f, minDistanceMetres) * scale;
        source.maxDistance = Mathf.Max(source.minDistance, maxDistanceMetres * scale);
    }

    /// <summary>도구가 직접 만든 AudioSource용 기본값. 매니저 풀과 같은 1m/15m를 쓴다.</summary>
    public static void Configure3D(AudioSource source) => Configure3D(source, 1f, 15f);

    /// <summary>
    /// <see cref="AudioSource.PlayClipAtPoint(AudioClip, Vector3, float)"/> 대체. 유니티 구현은
    /// minDistance 1 · maxDistance 500 · Logarithmic 롤오프를 박아 두므로 ×12 씬에서 타격음이
    /// 두 걸음만 떨어져도 사라진다. 같은 수명(클립 길이 뒤 자동 파괴)을 유지하면서 감쇠만 맞춘다.
    /// </summary>
    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        var host = new GameObject($"OneShot_{clip.name}");
        host.transform.position = position;

        var source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 1f;
        Configure3D(source);
        source.Play();

        // 클립 길이 그대로 두면 pitch를 건드린 호출에서 잘린다. 이 경로는 pitch 1 고정이라
        // 클립 길이가 곧 재생 길이다.
        Object.Destroy(host, clip.length + 0.1f);
    }
}
