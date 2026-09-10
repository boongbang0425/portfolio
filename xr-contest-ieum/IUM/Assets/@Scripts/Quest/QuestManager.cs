using System;
using System.Threading.Tasks;
using UnityEngine;

public enum QuestRuntimeState
{
    Loading,
    Intro,
    Objective,
    ObjectiveSuccess,
    Complete,
    Leaving,
    Missing,

    // 저장 진행이 같은 씬의 다른 러너(제작 공정) 구간을 가리켜 조용히 쉬는 상태. 오류가 아니므로
    // HUD는 카드를 내린다.
    Dormant
}

public enum QuestObjectiveBlockReason
{
    None,
    Inactive,
    Paused,
    Cutscene,
    Dialogue,
    ProcessGate,
    StepGap
}

/// <summary>
/// GameFlow가 진입시킨 한 퀘스트 그래프를 실행한다. 조작 튜토리얼과 제작 공정을 가리지 않는
/// 유일한 실행기이며, 목표 판정과 진행 기록을 맡고 다음 목적지 결정은 GameFlow에 맡긴다.
/// 실행 전 그래프 계약은 <see cref="QuestGraphValidator"/>로 검증한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestManager : MonoBehaviour
{
    public const string DataKey = "quest";

    [Header("참조")]
    [Tooltip("비우면 씬에서 찾습니다.")]
    [SerializeField] Player player;

    [Header("퀘스트")]
    [Tooltip("씬을 직접 실행할 때 사용할 퀘스트 ID입니다.")]
    [SerializeField] string sceneQuestId = "tutorial";

    [Tooltip("끄면 저장된 진행을 무시하고 sceneQuestId를 실행합니다.")]
    [SerializeField] bool useSavedProgress = true;

    [Tooltip("켜면 저장된 진행에 해당하는 퀘스트가 없을 때 sceneQuestId로 폴백하지 않고 대기한다. " +
             "공정 러너와 공존하는 Play 씬 전용.")]
    [SerializeField] bool requireSavedDefinition;

    [Tooltip("현재 퀘스트 완료 후 다음 공정의 퀘스트를 같은 씬에서 이어서 실행한다. 메인 플레이 씬용.")]
    [SerializeField] bool continueInSameScene;

    [Header("건너뛰기")]
    [Tooltip("튜토리얼 공정에서 강제 통과 키를 이 시간(초) 이상 길게 누르면 퀘스트 전체를 건너뛴다. " +
             "개발 게이트가 없는 제품 기능이다.")]
    [SerializeField, Min(0.5f)] float skipQuestHoldSeconds = 3f;

    [Header("개발 도구")]
    [SerializeField] bool showOverlay;
    [SerializeField] bool allowForceObjective = true;
    [SerializeField] KeyCode forceObjectiveKey = KeyCode.Return;

    QuestTable _table;
    QuestDefinition _definition;
    QuestNodeData _node;
    ProcessStep _objective;
    QuestRuntimeState _state = QuestRuntimeState.Loading;
    string _failureReason;
    bool _dormant;

    /// <summary>대기(Dormant)로 들어갈 때의 저장 진행. 이 값이 바뀌면 다시 해석해 깨어난다.</summary>
    ProcessId _dormantProcess;
    int _objectiveIndex = -1;
    int _objectiveCount;
    float _idleSeconds;
    float _resumeAt;
    bool _introPlayed;

    // 강제 통과 키의 탭/홀드 판정 상태. KeyDown에서 무장(armed)하고 KeyUp에서 탭을 판정하며,
    // 홀드 시간이 차면 그 자리에서 전체 건너뛰기를 발동한다.
    bool _skipHoldArmed;
    bool _skipHoldFired;
    float _skipHoldElapsed;

    public string QuestId => _definition?.Id;
    public string QuestTitle => string.IsNullOrWhiteSpace(_definition?.Title) ? QuestId : _definition.Title;
    public ProcessId Process { get; private set; }
    public QuestRuntimeState State => _state;
    public string FailureReason => _failureReason;
    public bool IsRunning => _state is QuestRuntimeState.Intro or QuestRuntimeState.Objective or
        QuestRuntimeState.ObjectiveSuccess or QuestRuntimeState.Complete;
    public bool IsObjectiveActive => _state == QuestRuntimeState.Objective;
    public QuestObjectiveBlockReason ObjectiveBlockReason
    {
        get
        {
            if (!IsObjectiveActive) return QuestObjectiveBlockReason.Inactive;
            if (PauseService.IsPaused) return QuestObjectiveBlockReason.Paused;
            if (CutsceneDirector.TryGetInstance(out var director) && director.IsPlaying)
                return QuestObjectiveBlockReason.Cutscene;
            if (IsSpeaking) return QuestObjectiveBlockReason.Dialogue;
            if (!ProcessGate.IsOpen) return QuestObjectiveBlockReason.ProcessGate;
            if (PauseService.Now < _resumeAt) return QuestObjectiveBlockReason.StepGap;
            return QuestObjectiveBlockReason.None;
        }
    }
    public bool CanEvaluateObjective => ObjectiveBlockReason == QuestObjectiveBlockReason.None;
    public int ObjectiveNumber => _objectiveIndex >= 0 ? _objectiveIndex + 1 : 0;
    public int ObjectiveCount => _objectiveCount;
    public float ObjectiveProgress => _objective?.Progress ?? 0f;
    public bool AllowForceObjective => allowForceObjective && DevelopmentFeaturesEnabled;
    public KeyCode ForceObjectiveKey => forceObjectiveKey;

    /// <summary>
    /// 퀘스트 전체 건너뛰기 가능 여부. 튜토리얼 공정 한정이며, 개발 게이트가 없는 제품 기능이다
    /// — 다른 공정은 실제 가공 결과물이 필요해 통째로 건너뛸 수 없다.
    /// </summary>
    public bool CanSkipQuest => Process == ProcessId.Tutorial && IsRunning;

    /// <summary>홀드 계측 중인지. HUD가 진행 게이지를 홀드 중에만 표시하는 데 쓴다.</summary>
    public bool IsSkipHolding => _skipHoldArmed && !_skipHoldFired && _skipHoldElapsed > 0f;

    /// <summary>홀드 진행률(0~1).</summary>
    public float SkipHoldProgress =>
        skipQuestHoldSeconds <= 0f ? 1f : Mathf.Clamp01(_skipHoldElapsed / skipQuestHoldSeconds);
    public QuestNodeData CurrentNode => _node;
    public ProcessStepData CurrentObjective => _objective?.Data;

    public event Action<QuestDefinition> QuestChanged;
    public event Action<QuestNodeData, int> ObjectiveChanged;
    public event Action<QuestRuntimeState> StateChanged;
    public event Action<ProcessId> Completed;

    /// <summary>
    /// 재안내가 나간 직후. <see cref="ProcessRunner.RetryHinted"/>와 같은 계약 — 이음이가 실패
    /// 1회로 계상한다 (F-012 2.4).
    /// </summary>
    public event Action<ProcessStepData> RetryHinted;

    void Awake()
    {
        if (player == null) player = FindAnyObjectByType<Player>();
        if (player == null)
            Debug.LogWarning("[Quest] 씬에 Player가 없어 입력 기반 목표를 판정할 수 없습니다.", this);

        _ = InitializeAsync();
    }

    async Task InitializeAsync()
    {
        _table = await LoadTableAsync();
        _table.Settings ??= new ProcessSettings();
        _table.Settings.Clamp();
        _table.Quests ??= new System.Collections.Generic.List<QuestDefinition>();

        var validation = QuestGraphValidator.Validate(_table);
        foreach (var error in validation)
            Debug.LogError($"[Quest] {error}", this);

        if (InGameDialogue.TryGetInstance(out var dialogue))
        {
            try
            {
                await dialogue.InitializeAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Quest] 대사 시스템 준비에 실패해 대사 없이 진행합니다: {exception.Message}");
            }
        }

        if (this == null) return;

        if (!ResolveQuest(out var quest) || !BeginQuest(quest))
        {
            // 공존 게이트로 쉬는 것은 오류가 아니다. ResolveQuest가 이미 정보 로그를 남겼다.
            if (_dormant)
            {
                SetState(QuestRuntimeState.Dormant);
                return;
            }

            var message = $"이 씬에서 실행할 퀘스트 '{sceneQuestId}'를 찾지 못했습니다.";
            Debug.LogWarning($"[Quest] {message}", this);
            SetState(QuestRuntimeState.Missing, message);
        }
    }

    /// <summary>
    /// 대기(Dormant)는 저장 진행이 이 러너의 구간이 아닐 때 들어온다. 같은 씬에서 GameFlow가
    /// 진행을 올려 주면 — 예: 게임플레이 씬 직행 시 프롤로그를 외부 완료해 Tutorial로 승격 —
    /// 다시 해석해서 깨어난다. 이 경로가 없으면 새 데이터로 Play 씬을 직접 실행할 때
    /// 프롤로그 승격이 러너의 첫 해석보다 늦게 끝나 영원히 대기한다.
    /// </summary>
    void TryWakeFromDormant()
    {
        if (_state != QuestRuntimeState.Dormant) return;
        if (SceneController.IsTransitioning) return;
        if (!DataManager.TryGetInstance(out var data) || !data.IsReady) return;
        if (data.Progress.NextProcess == _dormantProcess) return;

        if (ResolveQuest(out var quest) && BeginQuest(quest)) return;

        // 여전히 이 러너의 구간이 아니면 새 진행 값으로 계속 대기한다.
        if (_dormant) SetState(QuestRuntimeState.Dormant);
    }

    bool ResolveQuest(out QuestDefinition quest)
    {
        quest = null;
        _dormant = false;
        if (useSavedProgress && DataManager.TryGetInstance(out var data) && data.IsReady)
        {
            if (_table.TryGet(data.Progress.NextProcess, out quest)) return true;

            // Prologue는 "저장 없음"의 파수값이다(UserProgressData.HasSaveData). 미시작 데이터로
            // 게임플레이 씬에 직접 들어왔다면 새 게임의 첫 구간이므로, 대기하지 않고 씬 기본
            // 퀘스트(튜토리얼)를 곧바로 시작한다. 프롤로그 승격은 GameFlow의 외부 완료가 따라잡고,
            // 튜토리얼 완료 시 Complete가 진행을 올바르게 앞으로 옮긴다.
            if (data.Progress.NextProcess == ProcessId.Prologue && _table.TryGet(sceneQuestId, out quest))
                return true;

            // Play 씬에는 공정 러너가 함께 있다. 저장 진행이 가리키는 쪽만 시작해야 하므로,
            // 해당 퀘스트가 없다는 것은 이 러너의 구간이 아니라는 뜻이다 — 폴백하지 않는다.
            if (requireSavedDefinition)
            {
                _dormant = true;
                _dormantProcess = data.Progress.NextProcess;
                Debug.Log($"[Quest] 저장된 진행 '{data.Progress.NextProcess}'에 해당하는 퀘스트가 없어 " +
                          "이 러너는 대기합니다.");
                return false;
            }

            if (_table.TryGet(sceneQuestId, out quest))
            {
                Debug.LogWarning(
                    $"[Quest] 저장된 진행 '{data.Progress.NextProcess}'에 해당하는 퀘스트가 없어 " +
                    $"씬 기본값 '{sceneQuestId}'를 실행합니다.", this);
                return true;
            }

            return false;
        }

        if (_table.TryGet(sceneQuestId, out quest)) return true;

        if (useSavedProgress)
            Debug.LogWarning(
                $"[Quest] 저장 데이터가 준비되지 않아 씬 기본값 '{sceneQuestId}'도 찾지 못했습니다.", this);

        return false;
    }

    bool BeginQuest(QuestDefinition definition)
    {
        if (definition == null || !definition.HasGraph ||
            !definition.TryGetNode(definition.EntryNode, out var entry) ||
            entry.Kind != QuestNodeKind.Entry)
            return false;

        _definition = definition;
        _node = entry;
        _objective = null;
        _objectiveIndex = -1;
        _objectiveCount = CountObjectives(definition);
        _idleSeconds = 0f;
        _resumeAt = 0f;
        _introPlayed = false;
        Process = definition.Process;
        SetState(QuestRuntimeState.Intro);

        PlayDialogue(definition.IntroDialogue);
        QuestChanged?.Invoke(definition);
        return true;
    }

    static int CountObjectives(QuestDefinition definition)
    {
        var count = 0;
        if (definition?.Nodes == null) return count;
        foreach (var node in definition.Nodes)
            if (node?.Kind == QuestNodeKind.Objective)
                count++;
        return count;
    }

    async Task<QuestTable> LoadTableAsync()
    {
        try
        {
            await DataManager.Instance.InitializeAsync();
            if (DataManager.Instance.Static != null &&
                DataManager.Instance.Static.TryGet<QuestTable>(DataKey, out var table) &&
                table != null)
                return table;

            Debug.LogWarning($"[Quest] 정적 데이터 '{DataKey}'가 없어 빈 퀘스트 표로 시작합니다.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Quest] 퀘스트 데이터를 불러오지 못했습니다: {exception.Message}");
        }

        return QuestTable.CreateEmpty();
    }

    void Update()
    {
        if (PauseService.IsPaused || !IsRunning)
        {
            // 일시정지·비실행 구간의 홀드는 버린다. 계측을 이어 가면 일시정지 중에 시간이 차서
            // 재개 직후 전체 건너뛰기가 오발동한다(일시정지 중 발동 금지).
            ResetSkipHold();
            if (!PauseService.IsPaused) TryWakeFromDormant();
            return;
        }

        // 씬 전환이 시작되면 이 러너는 곧 파괴된다. 전환 시작이 현재 대사를 끊기 때문에, 여기서
        // 멈추지 않으면 IsSpeaking이 풀린 것을 보고 다음 목표로 넘어가 새 대사를 쏘고, 그 대사가
        // 전환을 넘어 살아남아 다음 씬의 러너와 겹친다(직접 실행 재진입에서 실제로 발생).
        if (SceneController.IsTransitioning) return;

        // 건너뛰기 입력은 대사·목표 준비 대기와 무관하게 받아야 하므로 아래 차단들보다 먼저 본다.
        TickSkipInput();
        if (!IsRunning) return; // 전체 건너뛰기가 방금 발동했으면 상태 진행은 여기서 끝낸다.

        if (CutsceneDirector.TryGetInstance(out var director) && director.IsPlaying) return;
        if (PauseService.Now < _resumeAt) return;

        switch (_state)
        {
            case QuestRuntimeState.Intro:
                if (!IsSpeaking) AdvanceFromCurrent();
                break;

            case QuestRuntimeState.Objective:
                TickObjective();
                break;

            case QuestRuntimeState.ObjectiveSuccess:
                if (!IsSpeaking) AdvanceFromCurrent();
                break;

            case QuestRuntimeState.Complete:
                if (!IsSpeaking) BeginLeave();
                break;
        }
    }

    void TickObjective()
    {
        if (_objective == null) return;

        if (!_introPlayed)
        {
            _introPlayed = true;
            PlayDialogue(_objective.Data.IntroDialogue);
        }

        if (!ProcessGate.IsOpen) return;

        var delta = Time.unscaledDeltaTime;
        _objective.Tick(delta);
        if (_objective.IsSatisfied)
        {
            SucceedObjective();
            return;
        }

        TickRetry(delta);
    }

    void TickRetry(float delta)
    {
        if (_objective == null || string.IsNullOrWhiteSpace(_objective.Data.RetryDialogue)) return;

        if (_objective.MadeProgress)
        {
            _idleSeconds = 0f;
            return;
        }

        _idleSeconds += delta;
        if (_idleSeconds < _table.Settings.RetryAfterSeconds) return;

        _idleSeconds = 0f;
        PlayDialogue(_objective.Data.RetryDialogue);
        RetryHinted?.Invoke(_objective.Data);
    }

    void SucceedObjective()
    {
        SetState(QuestRuntimeState.ObjectiveSuccess);
        _idleSeconds = 0f;
        PlayDialogue(_objective.Data.SuccessDialogue);
    }

    void AdvanceFromCurrent()
    {
        if (_definition == null || _node == null || !_definition.TryGetNext(_node.Id, out var next))
        {
            FailGraph($"노드 '{_node?.Id ?? "<null>"}'의 다음 노드를 찾지 못했습니다.");
            return;
        }

        EnterNode(next);
    }

    void EnterNode(QuestNodeData node)
    {
        _node = node;
        switch (node.Kind)
        {
            case QuestNodeKind.Objective:
                if (node.Objective == null)
                {
                    FailGraph($"목표 노드 '{node.Id}'에 objective가 없습니다.");
                    return;
                }

                _objectiveIndex++;
                _objective = new ProcessStep(node.Objective, player);
                SetState(QuestRuntimeState.Objective);
                _idleSeconds = 0f;
                _introPlayed = false;
                _resumeAt = PauseService.Now + _table.Settings.StepGapSeconds;

                ApplyLocks(node.Objective);
                _objective.Arm();
                ObjectiveChanged?.Invoke(node, _objectiveIndex);
                break;

            case QuestNodeKind.Complete:
                BeginComplete();
                break;

            default:
                FailGraph($"진행 중 Entry 노드 '{node.Id}'에 다시 진입했습니다.");
                break;
        }
    }

    void BeginComplete()
    {
        _objective = null;
        ProcessTarget.SetAllAvailable(false);
        SetState(QuestRuntimeState.Complete);
        PlayDialogue(_definition.CompleteDialogue);
    }

    void BeginLeave()
    {
        SetState(QuestRuntimeState.Leaving);
        _ = LeaveAsync();
    }

    /// <summary>
    /// 현재 퀘스트를 즉시 완주 처리하고 떠난다(튜토리얼 전체 건너뛰기). 진행·완료 대사를 전부
    /// 끊고 <see cref="BeginLeave"/> 경로로 직행하므로 완료 대사도 재생되지 않는다.
    /// <see cref="LeaveAsync"/>가 진행 기록·저장·<see cref="Completed"/> 발화를 수행하고,
    /// continueInSameScene이면 다음 공정을 같은 씬에서 이어 시작한다. GameFlow.CompleteQuestAsync를
    /// 부르지 않는 이유는 그 경로가 기록 직후 반드시 씬 이동을 수행하기 때문이다(LeaveAsync 주석).
    /// </summary>
    public void SkipCurrentQuest()
    {
        // IsRunning이 Leaving·Missing·Dormant를 걸러 중복 실행과 재진입을 막는다. Update의
        // Complete→BeginLeave 흐름과도 서로 배타적이다 — 어느 쪽이든 먼저 Leaving으로 바꾼 뒤에는
        // 다른 쪽이 진입하지 못한다.
        if (!IsRunning) return;

        Debug.Log($"[Quest] 퀘스트 '{QuestId}'({Process})를 건너뜁니다.");

        // 진행 중인 안내·완료 대사와 대기열을 모두 끊는다. 떠난 뒤 대사가 살아남아 다음 퀘스트
        // 위로 겹치지 않게 한다.
        if (InGameDialogue.TryGetInstance(out var dialogue)) dialogue.Stop(clearQueue: true);

        _objective = null;
        ProcessTarget.SetAllAvailable(false);
        BeginLeave();
    }

    /// <summary>
    /// 완료 처리. 진행 기록과 저장을 GameFlow(CompleteQuestAsync)에 넘기지 않고 여기서 직접 하는
    /// 이유는 같은 씬 연속 진행 때문이다 — GameFlow의 완료 경로는 기록 직후 반드시 다음 목적지로
    /// 이동시키는데, 제작 공정은 여러 개가 한 작업장에서 이어지므로 그때마다 씬을 다시 로드하면
    /// 방금까지 가공하던 목재와 도구가 초기화된다. 기록·저장만 러너가 하고, 씬을 실제로 떠날 때만
    /// GameFlow에 목적지를 묻는다.
    /// </summary>
    async Task LeaveAsync()
    {
        var process = Process;
        var continueHere = false;
        QuestDefinition nextQuest = null;

        if (DataManager.TryGetInstance(out var data) && data.IsReady)
        {
            var progress = data.Progress;
            progress.Complete(process, _definition.Grade);

            continueHere = continueInSameScene && _table.TryGet(progress.NextProcess, out nextQuest);

            // 같은 작업장에서 다음 공정을 이어 갈 때는 위치도 작업 상태의 일부다. 엔딩처럼 씬을
            // 실제로 떠날 때만 지워 다음 장소에 이전 좌표가 적용되지 않게 한다.
            if (!continueHere) progress.PlayerPose = null;

            try
            {
                await data.SaveUserAsync();
            }
            catch (Exception exception)
            {
                // 치명적이지 않다. 메모리상 진행은 이미 올라갔으므로 다음 저장 때 함께 기록된다.
                Debug.LogError($"[Quest] 진행 저장에 실패했습니다: {exception.Message}");
            }
        }

        if (this == null) return;

        Completed?.Invoke(process);

        if (continueHere && BeginQuest(nextQuest))
        {
            Debug.Log($"[Quest] 같은 씬에서 다음 퀘스트 '{QuestId}'({Process})를 시작합니다.");
            return;
        }

        if (!GameFlow.TryGetInstance(out var flow))
        {
            Debug.LogError("[Quest] 다음 목적지를 결정할 GameFlow가 없습니다.", this);
            return;
        }

        flow.GoToCurrent();
    }

    void FailGraph(string message)
    {
        _objective = null;
        ProcessTarget.SetAllAvailable(false);
        SetState(QuestRuntimeState.Missing, message);
        Debug.LogError($"[Quest] {message}", this);
    }

    void SetState(QuestRuntimeState state, string failureReason = null)
    {
        var changed = _state != state || !string.Equals(_failureReason, failureReason, StringComparison.Ordinal);
        _state = state;
        _failureReason = state == QuestRuntimeState.Missing ? failureReason : null;
        if (changed) StateChanged?.Invoke(state);
    }

    static void ApplyLocks(ProcessStepData step)
    {
        var keys = step.Unlock != null && step.Unlock.Count > 0 ? step.Unlock : null;

        foreach (var target in ProcessTarget.All)
        {
            if (target == null) continue;

            var unlocked = keys != null
                ? keys.Contains(target.Key)
                : string.Equals(target.Key, step.Target, StringComparison.Ordinal);

            target.SetAvailable(unlocked);
        }
    }

    static bool IsSpeaking =>
        InGameDialogue.TryGetInstance(out var dialogue) && dialogue.IsSpeaking;

    static void PlayDialogue(string sequenceId)
    {
        if (string.IsNullOrWhiteSpace(sequenceId)) return;

        if (!InGameDialogue.TryGetInstance(out var dialogue) || !dialogue.IsReady)
        {
            Debug.LogWarning($"[Quest] 대사 시스템이 준비되지 않아 '{sequenceId}'를 재생하지 못했습니다.");
            return;
        }

        dialogue.Play(sequenceId);
    }

    /// <summary>VR 강제 통과에 쓰는 손. 왼손 Primary는 이미 PTT라 오른손을 쓴다.</summary>
    const XRHandSide SkipHand = XRHandSide.Right;

    /// <summary>
    /// VR 강제 통과 버튼. 오른손 Primary(A)는 <see cref="XRInputSource"/>가 컷씬 건너뛰기로도
    /// 읽으므로 '건너뛰기'가 게임 전체에서 한 버튼으로 통일된다.
    /// </summary>
    const XRInputButton SkipButton = XRInputButton.Primary;

    /// <summary>
    /// 강제 통과 입력의 탭/홀드 판정. 짧게 눌렀다 떼면 기존 개발용 현재 목표 건너뛰기,
    /// 홀드 시간이 차면 그 자리에서 튜토리얼 전체 건너뛰기다(발동 후 놓는 것은 무시).
    ///
    /// 조작: 데스크톱은 <see cref="forceObjectiveKey"/>(기본 Enter), VR은 오른손 컨트롤러
    /// Primary(A) 버튼이다. 둘은 OR로 합류해 같은 홀드 게이지를 채운다 — 튜토리얼은 이동 잠금이
    /// 걸린 채 시작하므로, VR 대체 입력이 없으면 HMD만 쓴 플레이어가 첫 목표에서 빠져나올 수단이
    /// 없었다. 전체 건너뛰기(홀드)는 개발 게이트가 없는 제품 기능이라 실기기 빌드에서도 동작한다.
    /// </summary>
    void TickSkipInput()
    {
        var input = UserInput.Instance;
        if (input == null) return;

        var held = input.GetKey(forceObjectiveKey) || input.GetXRButton(SkipHand, SkipButton);

        // 눌리는 순간부터 계측을 무장한다. 퀘스트 전환이나 일시정지를 넘어 이어져 온 홀드가 새
        // 퀘스트에서 탭이나 전체 건너뛰기로 이어지지 않게 하기 위해서다.
        if (input.GetKeyDown(forceObjectiveKey) || input.GetXRButtonDown(SkipHand, SkipButton))
        {
            _skipHoldArmed = true;
            _skipHoldFired = false;
            _skipHoldElapsed = 0f;
        }

        if (!_skipHoldArmed) return;

        if (held)
        {
            // 일시정지 중에는 Update가 진입하지 않으므로 이 누적은 자연히 멈춘다.
            _skipHoldElapsed += Time.unscaledDeltaTime;

            if (!_skipHoldFired && CanSkipQuest && _skipHoldElapsed >= skipQuestHoldSeconds)
            {
                _skipHoldFired = true;
                SkipCurrentQuest();
            }

            return;
        }

        // 입력이 떨어졌다. 놓는 프레임이 아니면(포커스 상실 등) 탭으로 치지 않는다.
        var released = input.GetKeyUp(forceObjectiveKey) || input.GetXRButtonUp(SkipHand, SkipButton);
        var isTap = released && !_skipHoldFired && _skipHoldElapsed < skipQuestHoldSeconds;
        ResetSkipHold();

        if (!isTap || !AllowForceObjective) return;
        if (_state != QuestRuntimeState.Objective || _objective == null) return;
        if (PauseService.Now < _resumeAt) return;

        _objective.ForceSatisfy();
        SucceedObjective();
    }

    void ResetSkipHold()
    {
        _skipHoldArmed = false;
        _skipHoldFired = false;
        _skipHoldElapsed = 0f;
    }

    static bool DevelopmentFeaturesEnabled => Application.isEditor || Debug.isDebugBuild;

    void OnGUI()
    {
        if (!showOverlay) return;

        var step = _objective?.Data;
        var lines = new[]
        {
            $"퀘스트: {QuestId ?? "-"}   목표: {(_definition != null ? $"{_objectiveIndex + 1}/{_objectiveCount}" : "-")}   상태: {_state}",
            $"노드: {_node?.Id ?? "-"}",
            $"목표: {(step != null ? step.Goal : _state == QuestRuntimeState.Missing ? "그래프 오류" : "-")}",
            $"조건: {(step != null ? $"{step.Condition} {_objective.Progress * 100f:F0}%" : "-")}",
            $"공정 판정: {ProcessGate.Describe()}",
            AllowForceObjective
                ? $"{forceObjectiveKey} 짧게 현재 목표 강제 통과" +
                  (CanSkipQuest ? " · 길게 튜토리얼 건너뛰기" : string.Empty) + " · Esc 일시정지"
                : CanSkipQuest
                    ? $"{forceObjectiveKey} 길게 튜토리얼 건너뛰기 · Esc 일시정지"
                    : "Esc 일시정지"
        };

        GUI.Box(new Rect(10f, 10f, 680f, 22f * lines.Length + 12f), string.Empty);
        for (var i = 0; i < lines.Length; i++)
            GUI.Label(new Rect(20f, 16f + i * 22f, 660f, 22f), lines[i]);
    }
}
