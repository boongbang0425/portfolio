using UnityEngine;

/// <summary>
/// Keyboard driver for 대사 verification without a headset, and a live readout of the two gates
/// the system holds. Stands in for the 3~4단계 process systems that will eventually request these
/// sequences. Add it to development scenes only.
///
/// Z 챕터 도입 · X 먹매김 도입 · C 훈수(판정 차단 안 함) · V 평가(우선순위 높음) ·
/// B 이음이 인사 · N 자막 표시 경로 전환 · Backspace 중단 (Esc는 일시정지가 쓴다)
/// </summary>
public sealed class DialogueDebugController : MonoBehaviour
{
    [SerializeField] bool showOverlay = true;

    void Update()
    {
        var input = UserInput.Instance;
        if (input == null) return;

        if (input.GetKeyDown(KeyCode.Z)) Play("chapter1_intro");
        else if (input.GetKeyDown(KeyCode.X)) Play("makmeok_intro");
        else if (input.GetKeyDown(KeyCode.C)) Play("makmeok_hint_slow");
        else if (input.GetKeyDown(KeyCode.V)) Play("makmeok_result_pass");
        else if (input.GetKeyDown(KeyCode.B)) Play("tutorial_greeting");

        if (input.GetKeyDown(KeyCode.N)) CycleSubtitleMode();
        if (input.GetKeyDown(KeyCode.Backspace)) InGameDialogue.Instance.Stop();
    }

    static void Play(string sequenceId) => InGameDialogue.Instance.Play(sequenceId);

    /// <summary>
    /// 말풍선 → 화면 자막 → 둘 다 순으로 돈다. 데스크톱에서 월드 말풍선의 위치·크기를 기존
    /// 화면 자막과 나란히 놓고 확인하는 경로다 — VR에서는 화면 자막이 렌더되지 않아 비교가 불가능하다.
    /// </summary>
    static void CycleSubtitleMode() => WorldSubtitleDirector.DisplayMode =
        WorldSubtitleDirector.DisplayMode switch
        {
            SubtitleDisplayMode.BubbleOnly => SubtitleDisplayMode.ScreenOnly,
            SubtitleDisplayMode.ScreenOnly => SubtitleDisplayMode.Both,
            _ => SubtitleDisplayMode.BubbleOnly
        };

    /// <summary>
    /// 말풍선이 고른 폰트 이름. 한글이 네모로 보일 때 어느 폰트로 떨어졌는지 바로 알 수 있다.
    /// 선택을 유발하지 않으므로 첫 자막 전에는 "미정"이다.
    /// </summary>
    static string DescribeFont()
    {
        var font = DialogueBubbleFont.Current;
        return font != null ? font.name : "미정";
    }

    void OnGUI()
    {
        if (!showOverlay) return;

        var dialogue = InGameDialogue.Instance;
        if (dialogue == null) return;

        // The point of the overlay is that the two gates are observable: a held ProcessGate is
        // what stops a process from judging, and it is otherwise invisible.
        var current = dialogue.Current;
        var lines = new[]
        {
            $"대사: {(current != null ? current.Id : "없음")}   대기: {dialogue.QueuedCount}",
            $"공정 판정: {ProcessGate.Describe()}",
            $"입력 잠금: {InputLockService.Describe()}",
            $"자막 표시: {WorldSubtitleDirector.DisplayMode}   말풍선 폰트: {DescribeFont()}",
            "Z 챕터 도입 · X 먹매김 도입 · C 훈수 · V 평가 · B 이음이 · N 자막 경로 · Backspace 중단"
        };

        GUI.Box(new Rect(10f, 10f, 640f, 22f * lines.Length + 12f), string.Empty);
        for (var i = 0; i < lines.Length; i++)
            GUI.Label(new Rect(20f, 16f + i * 22f, 620f, 22f), lines[i]);
    }
}
