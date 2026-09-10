using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 인게임 대사. Plays 노장 lines while the player keeps control of movement and hands — only
/// 공정 판정 is held, and only for instructional sequences (F-011 1.4).
///
/// 이음이와는 잠금이 아니라 우선순위로 겹침을 막는다: PTT가 눌리면 재생 중인 대사를 끊고
/// (<see cref="InterruptForConversation"/>), 대화 중에 도착한 대사는 대기열에서 기다린다.
/// 근거는 <c>AcquireHolds</c>의 주석에 있다.
///
/// Voices play 2D: 노장 has no model in the scene and is represented by screen-fixed UI, so no
/// speaker owns a world position.
///
/// Cutscene dialogue is deliberately not routed through here. A cutscene owns the whole frame and
/// its lines are linear, whereas these arrive unpredictably from process events and need a queue.
/// Both share <see cref="DialoguePlayer"/> underneath.
/// </summary>
public sealed class InGameDialogue : Singleton<InGameDialogue>, ISceneEventListener
{
    /// <summary>Static data key registered in manifest.json.</summary>
    public const string DataKey = "dialogue";

    /// <summary>
    /// Supplies the TTS service used when a line has no recorded clip, per speaker — 노장과 이음이가
    /// 같은 목소리로 말하지 않도록 화자를 받는다. Null keeps dialogue on recorded audio only, which
    /// is the offline-safe default and the correct behaviour if 사전 녹음 is chosen (07 문서 미정 16번).
    ///
    /// <see cref="DialogueVoiceBootstrap"/>가 기본값을 채운다. 줄마다 호출되므로 늦게 준비돼도 된다.
    /// </summary>
    public static Func<DialogueSpeaker, IAiTextToSpeechService> TextToSpeechResolver { get; set; }

    readonly List<DialogueSequence> _queue = new();

    DialogueTable _table;
    DialoguePlayer _player;
    DialogueVoiceLibrary _voices;
    Task _initialization;
    IDisposable _processHold;

    /// <summary>Suppresses queue handling while a sequence is being swapped out for another.</summary>
    bool _swapping;

    /// <summary>
    /// 이음이 호출 때문에 현재 시퀀스를 끊는 중. <see cref="DialoguePlayer.Stop"/>이 그 자리에서
    /// <see cref="OnSequenceFinished"/>를 부르는데, 그 시점에는 대화 상태가 아직 Listening으로
    /// 바뀌기 전이라 <see cref="ConversationBusy"/>만으로는 대기열이 곧바로 흘러나간다.
    /// </summary>
    bool _interrupting;

    public bool IsReady { get; private set; }
    public bool IsSpeaking => _player != null && _player.IsPlaying;
    public DialogueSequence Current => _player?.CurrentSequence;
    public int QueuedCount => _queue.Count;

    /// <summary>
    /// 이음이가 질문을 받는 중이면 참. 그동안 새 노장 대사는 시작하지 않고 대기열에 쌓인다.
    ///
    /// <c>HasInstance</c>로 확인한다 — 이음이가 없는 씬에서 <c>Instance</c>를 건드리면 대화
    /// 매니저가 그 씬에 생겨 버린다(<see cref="PushToTalkLockBridge"/>와 같은 규칙).
    /// </summary>
    static bool ConversationBusy =>
        AiConversationManager.HasInstance && AiConversationManager.Instance.IsBusy;

    /// <summary>Hook for lipsync and speaking animation.</summary>
    public event Action<DialogueLine> LineStarted;

    public event Action<DialogueLine> LineFinished;

    /// <summary>Empty text means "hide the subtitle". 대사 볼륨이 0이어도 자막은 표시한다 (F-018 1.4).</summary>
    public event Action<DialogueSpeaker, string> SubtitleChanged;

    /// <summary>Second argument is false when the sequence was cut.</summary>
    public event Action<DialogueSequence, bool> SequenceFinished;

    public event Action Ready;

    protected override void Awake()
    {
        base.Awake();
        if (!ReferenceEquals(Instance, this)) return;

        SceneController.Instance?.RegisterListener(this);
        _ = InitializeAsync();
    }

    public Task InitializeAsync() => _initialization ??= InitializeInternalAsync();

    async Task InitializeInternalAsync()
    {
        _table = await LoadTableAsync();
        _table.Prepare();

        _voices = new DialogueVoiceLibrary(speaker => TextToSpeechResolver?.Invoke(speaker));
        _player = new DialoguePlayer(transform, _table.Settings, _voices);
        _player.LineStarted += OnLineStarted;
        _player.LineFinished += OnLineFinished;
        _player.SubtitleChanged += OnSubtitleChanged;
        _player.SequenceFinished += OnSequenceFinished;

        IsReady = true;
        Ready?.Invoke();
    }

    /// <summary>
    /// A missing table is not fatal. The entry is optional in the manifest so the system runs
    /// before the Addressables asset exists, and a broken file degrades the same way.
    /// </summary>
    async Task<DialogueTable> LoadTableAsync()
    {
        try
        {
            await DataManager.Instance.InitializeAsync();
            if (DataManager.Instance.Static != null &&
                DataManager.Instance.Static.TryGet<DialogueTable>(DataKey, out var table) &&
                table != null)
                return table;

            Debug.LogWarning($"[Dialogue] 정적 데이터 '{DataKey}'가 없어 빈 대사표로 시작합니다.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Dialogue] 대사 데이터를 불러오지 못했습니다: {exception.Message}");
        }

        return DialogueTable.CreateEmpty();
    }

    // The player already runs on PauseService.Now, so a paused game would not advance anyway; this
    // makes the intent explicit and skips the work. 일시정지 중에는 줄 스킵도 함께 막힌다.
    void Update()
    {
        if (PauseService.IsPaused) return;

        // 대사 줄 스킵 Tab(개발용). Enter는 QuestManager·ProcessRunner·PlaceholderProcess의 강제
        // 진행 키이고 →는 이동 대체 키라 겹친다. Tab은 프로젝트 어디에서도 쓰지 않는다.
        if (IsSpeaking && UserInput.HasInstance && UserInput.Instance.GetKeyDown(KeyCode.Tab))
            SkipLine();

        _player?.Tick();

        // 이음이 때문에 미뤄 둔 대사를 이어 받는다. 정상 경로에서는 OnSequenceFinished가 대기열을
        // 비우므로 여기서 할 일이 생기는 것은 대화가 끝난 직후 한 번뿐이다.
        DrainQueue();
    }

    /// <summary>대기열의 다음 시퀀스를 시작한다. 이미 말하는 중이거나 이음이가 대화 중이면 기다린다.</summary>
    void DrainQueue()
    {
        if (!IsReady || IsSpeaking || _interrupting || _queue.Count == 0 || ConversationBusy) return;

        var next = _queue[0];
        _queue.RemoveAt(0);
        StartSequence(next);
    }

    /// <summary>현재 줄을 즉시 끝내고 다음 줄로 넘어간다. 재생 중이 아니면 아무 일도 하지 않는다.</summary>
    public void SkipLine() => _player?.SkipLine();

    /// <summary>Plays a sequence by id. Returns false when the id is unknown or data is not ready.</summary>
    public bool Play(string sequenceId)
    {
        if (!IsReady)
        {
            Debug.LogWarning($"[Dialogue] 초기화 전에 '{sequenceId}' 재생을 요청했습니다.");
            return false;
        }

        if (!_table.TryGet(sequenceId, out var sequence))
        {
            Debug.LogWarning($"[Dialogue] 대사 '{sequenceId}'를 찾을 수 없습니다.");
            return false;
        }

        PlaySequence(sequence);
        return true;
    }

    /// <summary>
    /// Plays or queues a sequence. A higher priority cuts an interruptible sequence; anything
    /// else waits in priority order behind what is already queued.
    /// </summary>
    public void PlaySequence(DialogueSequence sequence)
    {
        if (!IsReady || sequence == null || !sequence.HasLines) return;

        // 이음이가 질문을 받는 중이면 우선순위와 무관하게 미룬다. 대사 우선순위는 대사끼리의
        // 규칙이라 대화를 밀어낼 근거가 되지 않는다 — 특히 재안내 대사는 타이머로 주기적으로
        // 들어오므로, 여기서 시작하면 질문하는 도중에 노장이 말을 덮어 쓴다.
        if (ConversationBusy)
        {
            Enqueue(sequence);
            return;
        }

        if (!IsSpeaking)
        {
            StartSequence(sequence);
            return;
        }

        var current = Current;
        if (sequence.Priority > current.Priority && current.Interruptible)
        {
            // The cut sequence is dropped rather than requeued: replaying an instruction the
            // player has already moved past reads as a bug, not as recovery.
            StartSequence(sequence);
            return;
        }

        Enqueue(sequence);
    }

    /// <summary>Stops the current sequence. Used by 공정 다시 시작 and scene teardown.</summary>
    public void Stop(bool clearQueue = true)
    {
        if (clearQueue) _queue.Clear();
        _player?.Stop();
    }

    /// <summary>
    /// 이음이 호출이 들어와 현재 대사를 끊는다 (F-011 1.4 완화). 잘린 시퀀스는 <see cref="StartSequence"/>의
    /// 우선순위 가로채기와 같은 규칙으로 버린다 — 이미 지나간 안내를 다시 트는 편이 더 버그처럼 보인다.
    /// 대기열은 남으며 <see cref="DrainQueue"/>가 대화가 끝난 뒤 이어서 재생한다.
    /// </summary>
    public void InterruptForConversation()
    {
        if (!IsSpeaking) return;

        _interrupting = true;
        _player.Stop();
        _interrupting = false;
    }

    void Enqueue(DialogueSequence sequence)
    {
        // 같은 시퀀스를 두 번 쌓지 않는다. 재안내 대사는 타이머로 반복되므로, 대화 때문에 미루는
        // 동안 두 번 이상 들어오면 대화가 끝난 뒤 같은 말을 연달아 하게 된다.
        for (var i = 0; i < _queue.Count; i++)
            if (ReferenceEquals(_queue[i], sequence)) return;

        // Higher priority first, FIFO within the same priority.
        var index = _queue.Count;
        for (var i = 0; i < _queue.Count; i++)
        {
            if (_queue[i].Priority >= sequence.Priority) continue;
            index = i;
            break;
        }

        _queue.Insert(index, sequence);
    }

    void StartSequence(DialogueSequence sequence)
    {
        _swapping = true;
        _player.Stop();
        _swapping = false;

        ReleaseHolds();
        AcquireHolds(sequence);

        _swapping = true;
        _player.Play(sequence);
        _swapping = false;
    }

    /// <summary>
    /// 인게임 대사는 PTT를 잠그지 않는다.
    ///
    /// F-011 1.4의 "노장 대사 중 이음이 질문 불가"를 <see cref="InputLockFlags.PushToTalk"/> 토큰으로
    /// 구현했더니 호출이 통째로 사라졌다. 토큰을 잡으면 <see cref="InputLockService.Apply"/>가
    /// <c>commands.PushToTalk</c>를 0으로 만들어 <c>VoiceInputModule</c>이 누름 자체를 못 보고,
    /// 동시에 <see cref="PushToTalkLockBridge"/>가 <c>SetInputLocked(true)</c>를 걸어 진행 중이던
    /// 파이프라인까지 취소했다. 재안내 대사가 주기적으로 반복되는 구간에서는 사실상 상시 차단이었다.
    ///
    /// 대신 "우선순위"로 바꿨다 — PTT가 대사를 끊고(<see cref="InterruptForConversation"/>),
    /// 대화 중에 들어온 대사는 <see cref="Enqueue"/>로 미룬다. 겹쳐 들리는 일은 그대로 없고
    /// 호출은 소실되지 않는다.
    ///
    /// 컷씬·튜토리얼·일시정지의 PTT 잠금은 그대로다. 각각 <c>CutsceneDirector</c>,
    /// <c>TutorialFlowDirector</c>, <c>PauseController</c>가 자기 토큰으로 잡으며, 그 구간은
    /// 이음이를 부르면 안 되는 것이 맞다.
    /// </summary>
    void AcquireHolds(DialogueSequence sequence)
    {
        if (sequence.BlocksProcess)
            _processHold = ProcessGate.Hold($"대사:{sequence.Id}");
    }

    void ReleaseHolds()
    {
        _processHold?.Dispose();
        _processHold = null;
    }

    void OnLineStarted(DialogueLine line) => LineStarted?.Invoke(line);
    void OnLineFinished(DialogueLine line) => LineFinished?.Invoke(line);
    void OnSubtitleChanged(DialogueSpeaker speaker, string text) => SubtitleChanged?.Invoke(speaker, text);

    void OnSequenceFinished(DialogueSequence sequence, bool completed)
    {
        // A swap already released and re-acquired the holds; running the queue here would
        // start a second sequence on top of the one being installed.
        if (_swapping) return;

        ReleaseHolds();
        SequenceFinished?.Invoke(sequence, completed);

        // 이음이가 대화 중이면 여기서 다음 줄을 시작하지 않는다. DrainQueue가 대화가 끝난 뒤 받는다.
        DrainQueue();
    }

    public void OnSceneLoadStart(string sceneName)
    {
        Stop();
        ReleaseHolds();
        _voices?.ReleaseAll();
    }

    public void OnSceneLoadComplete(string sceneName) { }

    protected override void OnDestroy()
    {
        if (SceneController.TryGetInstance(out var controller))
            controller.UnregisterListener(this);

        ReleaseHolds();
        _player?.Dispose();
        _voices?.ReleaseAll();
        base.OnDestroy();
    }
}
