using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Play 씬을 에디터에서 직접 실행했을 때 정식 진입 경로로 되돌린다.
///
/// 빌드는 언제나 StartScene에서 시작하므로 <see cref="GameFlow"/>가 저장 진행을 읽고 목적지를
/// 정한 뒤에 Play 씬이 열린다. 반면 에디터에서 Play 씬을 직접 누르면 그 결정 단계가 통째로
/// 빠진다. 그러면 저장 진행이 Play 씬 공정(tutorial~gongpoPuzzle)을 가리키지 않는 한
/// <c>QuestManager</c>의 <c>requireSavedDefinition</c> 게이트가 러너를 대기 상태로 두고, 씬은
/// 열렸는데 아무것도 시작하지 않는 상태가 된다.
///
/// 씬이나 프리팹에 오브젝트를 하나도 놓지 않는 정적 부트스트랩으로 만든 이유는 공용 자산을
/// 건드리지 않기 위해서다. Play 씬과 PlayLoop·CoreSystems 프리팹은 여러 사람이 함께 쓰는
/// 파일이고, 에디터 편의를 위한 장치를 그 안에 심으면 병합 충돌과 빌드 포함 여부를 매번
/// 신경 써야 한다. <c>RuntimeInitializeOnLoadMethod</c>는 그 비용이 없다
/// (<see cref="DialogueVoiceBootstrap"/>과 같은 방식).
///
/// 진입은 <see cref="GameFlow.ContinueAsync"/>를 그대로 재사용한다. 여기서 씬 이름을 직접
/// 고르거나 진행 데이터를 손보면 게이팅·저장·컷씬 규칙이 실플레이와 갈라진다 — 이어하기
/// 버튼과 같은 경로를 타야 저장이 prologue를 가리킬 때 프롤로그 컷씬으로 가고, Play 공정을
/// 가리킬 때 Play 씬이 정식 전환 절차를 거쳐 다시 열린다.
/// </summary>
public static class PlaySceneBootstrap
{
    /// <summary>flow.json이 Play 공정들의 목적지로 지정한 씬 이름.</summary>
    const string PlaySceneName = "Play";

    /// <summary>
    /// 정적 데이터는 Addressables를 거치고 첫 실행에는 카탈로그 준비까지 붙는다. 느린 기계에서도
    /// 넉넉하도록 잡았다 — 여기에 걸린다는 것은 지연이 아니라 고장이라는 뜻이다.
    /// </summary>
    const float TimeoutSeconds = 15f;

    static bool _triggered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        // 도메인 리로드를 끈 채 플레이에 들어가면 이전 세션의 플래그가 그대로 남는다.
        _triggered = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        // 빌드는 StartScene에서 시작하므로 원래 여기 걸릴 일이 없다. 이중 가드다 — 이 장치가
        // 출시 빌드에서 진입 흐름에 끼어드는 경우를 아예 없앤다.
        if (!Application.isEditor) return;

        // 세션의 첫 씬만 본다. GameFlow가 Play 씬을 정식으로 열 때도 이 훅은 다시 불리지
        // 않지만, 플래그와 함께 두어 재진입 고리가 생기지 않도록 한다.
        if (_triggered) return;
        if (SceneManager.GetActiveScene().name != PlaySceneName) return;

        _triggered = true;

        // 첫 씬이 Play가 아닌 정상 경로에서는 아무 로그도 남기지 않는다.
        Debug.Log("[PlayBootstrap] Play 씬 직접 실행을 감지했습니다 — 이어하기 경로로 재진입합니다.");
        _ = ReenterAsync();
    }

    static async Task ReenterAsync()
    {
        try
        {
            var flow = await WaitForServicesAsync();
            if (flow == null) return;

            // 저장 진행의 목적지가 이미 이 씬이면 재진입하지 않는다. 씬의 QuestManager가 정상
            // 시작했는데 리로드를 겹치면, 전환 시작이 첫 대사를 끊고 죽어가는 러너가 다음 대사를
            // 쏘아 리로드 후의 러너와 같은 대사를 두 번 재생한다. 재진입은 목적지가 다른 곳
            // (프롤로그 미완 등)이라 이 씬에 있을 이유가 없을 때만 필요하다.
            if (string.Equals(
                    flow.GetDestinationScene(DataManager.Instance.Progress.NextProcess),
                    PlaySceneName, StringComparison.Ordinal))
            {
                Debug.Log("[PlayBootstrap] 저장 진행이 이미 이 씬을 가리켜 재진입을 생략합니다.");
                return;
            }

            await flow.ContinueAsync();
        }
        catch (Exception exception)
        {
            // 개발 편의 장치가 예외로 플레이를 막아서는 안 된다. 실패해도 씬은 그대로 열려 있고
            // 러너만 대기 상태로 남는다.
            Debug.LogWarning($"[PlayBootstrap] 재진입에 실패했습니다: {exception.Message}");
        }
    }

    /// <summary>
    /// <see cref="GameFlow"/>와 <see cref="DataManager"/>가 준비될 때까지 프레임 단위로 기다린다.
    ///
    /// 폴링인 이유는 두 싱글턴의 준비 완료가 이벤트로 오되 이미 지난 이벤트를 되받을 방법이
    /// 없어서다. 이 훅이 도는 <c>AfterSceneLoad</c> 시점이면 씬의 CoreSystems Awake가 이미
    /// 끝나 초기화가 진행 중일 수도, 아직 시작 전일 수도 있다. 두 경우를 한 코드로 다루려면
    /// 상태를 직접 보는 쪽이 단순하다.
    /// </summary>
    static async Task<GameFlow> WaitForServicesAsync()
    {
        var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

        // 접근만으로 씬에서 찾거나 새로 만든다(Singleton). CoreSystems가 빠진 씬에서도
        // 진입이 되도록 두되, 그마저 실패하면 아래 타임아웃이 받는다.
        var flow = GameFlow.Instance;

        while (true)
        {
            if (flow != null && flow.IsReady &&
                DataManager.HasInstance && DataManager.Instance.IsReady)
                return flow;

            if (Time.realtimeSinceStartup > deadline)
            {
                Debug.LogWarning(
                    $"[PlayBootstrap] {TimeoutSeconds:F0}초 안에 GameFlow·DataManager가 준비되지 " +
                    "않아 재진입을 포기합니다. 씬에 CoreSystems가 있는지, 정적 데이터 로드가 " +
                    "실패하지 않았는지 확인하십시오.");
                return null;
            }

            await Task.Yield();

            // 플레이 모드를 나가면 더 기다릴 대상이 없다.
            if (!Application.isPlaying) return null;

            // 초기화 도중 파괴됐다면(씬 재로드 등) 다시 잡는다.
            if (flow == null) flow = GameFlow.Instance;
        }
    }
}
