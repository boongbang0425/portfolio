using UnityEngine;

/// <summary>
/// Mirrors the PTT input lock into <see cref="AiConversationManager"/>. Kept as a bridge instead
/// of having each holder call SetInputLocked directly: 컷씬·튜토리얼·일시정지가 동시에 PTT를 잡을
/// 수 있고, 직접 호출하면 먼저 끝난 쪽이 나머지가 아직 진행 중인데도 이음이를 풀어 버린다.
/// <see cref="InputLockService"/> already reference counts, so the conversation layer only has to
/// follow the combined result.
///
/// 인게임 노장 대사는 더 이상 이 경로를 타지 않는다 — 대사 중 PTT를 막으면 호출이 소실돼서
/// <see cref="InGameDialogue"/>의 우선순위 규칙(끊기·미루기)으로 옮겼다.
/// </summary>
public static class PushToTalkLockBridge
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        InputLockService.Changed -= OnLocksChanged;
        InputLockService.Changed += OnLocksChanged;
    }

    static void OnLocksChanged(InputLockFlags locks)
    {
        // HasInstance, not Instance: 이음이 is absent from scenes that only play 노장 대사, and
        // touching Instance would create the conversation manager there.
        if (!AiConversationManager.HasInstance) return;
        AiConversationManager.Instance.SetInputLocked((locks & InputLockFlags.PushToTalk) != 0);
    }
}
