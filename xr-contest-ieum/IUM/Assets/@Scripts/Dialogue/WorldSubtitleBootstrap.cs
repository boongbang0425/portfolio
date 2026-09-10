using UnityEngine;

/// <summary>
/// 월드 말풍선 자막을 씬 수정 없이 세운다.
///
/// 자막은 대사가 있는 모든 씬에서 필요하고, 그 씬들은 여러 사람이 나눠 갖고 있다. 씬마다 매니저를
/// 배치하면 배치 누락이 곧 무음 자막이 되고, 공용 자산 수정 승인도 씬 수마다 필요해진다. 진입점에서
/// 한 번 거는 쪽이 싸다 — <c>DialogueVoiceBootstrap</c>·<c>CoreAudioBootstrap</c>과 같은 방식이다.
///
/// <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>에서 세우는 이유는 첫 씬의 첫 대사를
/// 놓치지 않기 위해서다. 매니저가 씬 로드 뒤에 붙으면 그 사이에 시작된 시퀀스의 첫 줄이 지나간다
/// (같은 종류의 사고가 ISSUE-021이었다).
/// </summary>
public static class WorldSubtitleBootstrap
{
    /// <summary>
    /// 이 시점에는 씬이 아직 하나도 로드되지 않았으므로 여기서 만든 것이 항상 정품이 된다.
    /// 씬에 <c>WorldSubtitleDirector</c>를 따로 배치하면 <c>Singleton.Awake</c>가 그것을 파괴하고
    /// 그쪽 인스펙터 설정은 무시된다 — 표시 경로를 바꾸려면 실행 중 DontDestroyOnLoad에 있는
    /// 이 오브젝트를 고르거나 <see cref="WorldSubtitleDirector.DisplayMode"/>에 대입한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var host = new GameObject(nameof(WorldSubtitleDirector));
        Object.DontDestroyOnLoad(host);
        host.AddComponent<WorldSubtitleDirector>();
    }
}
