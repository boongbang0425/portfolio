using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <see cref="StartMenuXRPointer"/>를 씬 자산을 고치지 않고 런타임에 붙인다.
///
/// StartScene은 빌더(<c>StartSceneReworkBuilder</c>)로 생성된 공용 자산이고 재생성이 금지되어
/// 있어, 컴포넌트 하나를 넣자고 씬 파일을 건드리지 않는다. <c>CoreAudioBootstrap</c>과 같은
/// 진입점 설치 방식이다.
///
/// 대상 판별은 씬 이름이 아니라 <see cref="StartScenePlayerInteraction"/>의 존재로 한다. 그
/// 컴포넌트가 있는 씬이 곧 "월드 uGUI 버튼을 크로스헤어로 누르는 씬"이고, 지금은 StartScene
/// 하나다. 씬 이름을 박으면 씬을 복제하거나 이름을 바꿀 때 조용히 끊긴다.
/// </summary>
public static class StartMenuXRPointerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        // AfterSceneLoad 시점에는 첫 씬의 sceneLoaded가 이미 지나갔으므로 직접 한 번 건다.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    static void Attach()
    {
        if (Object.FindAnyObjectByType<StartMenuXRPointer>(FindObjectsInactive.Include) != null) return;

        var host = Object.FindAnyObjectByType<StartScenePlayerInteraction>(FindObjectsInactive.Include);
        if (host == null) return;

        // 같은 오브젝트에 붙인다. 어댑터가 크로스헤어 경로를 직접 물어야 하기 때문이다
        // (중복 실행 차단과 미명중 시 폴백).
        host.gameObject.AddComponent<StartMenuXRPointer>();
    }
}
