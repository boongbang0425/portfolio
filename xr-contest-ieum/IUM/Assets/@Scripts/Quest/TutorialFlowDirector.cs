using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// 조작 튜토리얼 동안 플레이어의 자유 행동을 막고, 배우는 조작만 목표 순서대로 열어 준다.
/// <see cref="QuestManager"/>가 실행하는 퀘스트의 공정이 <see cref="ProcessId.Tutorial"/>일 때만
/// 동작하며, 제작 공정 퀘스트로 넘어가면 스스로 모든 잠금을 놓는다.
///
/// 잠금은 <see cref="InputLockService"/> 토큰 하나로만 관리한다. 목표가 바뀔 때마다 "아직 잠겨
/// 있어야 할 플래그 집합"을 다시 계산해 새 토큰을 잡고 이전 토큰을 놓는다(순서가 반대이면 두
/// 토큰 사이에서 한 프레임 동안 전부 풀린 상태가 스쳐 지나간다).
///
/// 잠금 스케줄의 근거는 <see cref="ProcessStep.Evaluate"/>가 각 조건에서 읽는 필드와
/// <see cref="InputLockService.Apply"/>가 각 플래그에서 지우는 필드의 대응이다:
///
/// - look 조건은 <c>commands.Look</c> → Look 플래그가 지운다.
/// - move 조건은 <c>commands.Move</c> → Locomotion 플래그가 지운다.
/// - snapTurn 조건은 <c>commands.SnapTurn</c> → Look이 아니라 <b>Locomotion</b> 플래그가 지운다
///   (Apply에서 Move와 SnapTurn을 함께 0으로 만든다). 그래서 turn 목표는 move에서 이미 열린
///   Locomotion을 그대로 쓰고 새로 열 것이 없다.
/// - point 조건은 손의 Hovered 판정이라 잠금 플래그의 영향을 받지 않는다. 그래도 이 목표에서
///   Interact를 여는 이유는 "가리켜서 다루기"가 여기서 배우는 조작이기 때문이며, 여는 쪽은
///   판정을 막지 않으므로 안전하다.
/// - grab 조건은 <c>GrabPhase.Pressed</c> → Grab 플래그가 이 프레임을 None으로 바꾼다.
/// - place 조건은 잡은 물체를 소켓에 놓는 것이라 grab에서 연 Grab을 그대로 쓴다.
/// - pushToTalk 조건은 <c>commands.PushToTalk</c> → PushToTalk 플래그가 지운다.
/// - greeting 조건은 none(자동 통과)이라 아무 입력도 필요 없다. 인트로와 같은 전체 잠금으로 둔다.
///
/// Pause는 어느 단계에서도 잠그지 않는다. 잠그면 튜토리얼 도중 일시정지 메뉴로 빠져나갈 수 없다.
///
/// 잠금 외의 연출 두 가지도 여기서 맡는다:
/// - 발화자 주시: <see cref="InGameDialogue.LineStarted"/>를 구독해 화자가 바뀐 첫 줄에서만
///   플레이어 시점을 그 화자(노장·이음이) 쪽으로 1회 돌린다. 줄마다 돌리지 않는 이유는 이음이가
///   계속 움직이는 부유 NPC라 매 줄 회전이 끝없는 시점 추적이 되기 때문이다.
/// - 이음이 리액션: 목표 대사 종류에 맞춰 인사(인트로)·기웃(재안내)·백덤블링(성공 종료 후)을
///   재생한다. 종류 판별은 시퀀스 ID 접미사 규약이 아니라 현재 목표(ProcessStepData)의 대사
///   필드와 직접 비교한다 — tutorial_greeting처럼 접미사 규약을 따르지 않는 인트로 대사가 있다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialFlowDirector : MonoBehaviour
{
    const string LockReason = "튜토리얼 진행";

    // 이음이 리액션 지속시간(초). 대기 릴레이(CoPlayIdleReaction)가 쓰는 값과 맞춘다.
    const float GreetSeconds = 2.0f;
    const float WonderSeconds = 2.5f;
    const float HappySeconds = 1.5f;

    /// <summary>튜토리얼이 처음에 잠그는 전부. Pause만 빠진다.</summary>
    const InputLockFlags FullLock =
        InputLockFlags.Locomotion | InputLockFlags.Look | InputLockFlags.Grab |
        InputLockFlags.Interact | InputLockFlags.PushToTalk;

    /// <summary>
    /// 목표 노드 id별로 <b>아직 잠긴 채 남는</b> 플래그. quest.json의 tutorial 그래프 노드 id와
    /// 짝을 이룬다. 여기 없는 노드 id를 만나면 전부 풀고 경고를 남긴다 — 새 목표가 요구하는 입력을
    /// 모르는 채로 잠가 두면 진행이 막히기 때문이다.
    /// </summary>
    static readonly Dictionary<string, InputLockFlags> RemainingLocks =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // 인사: 조건 none. 대사만 듣는다.
            ["greeting"] = FullLock,

            // 둘러보기: 시점만 연다.
            ["look"] = FullLock & ~InputLockFlags.Look,

            // 이동: 걷기를 연다.
            ["move"] = FullLock & ~(InputLockFlags.Look | InputLockFlags.Locomotion),

            // 방향 전환: 스냅 턴은 Locomotion에 속하므로 move에서 이미 열려 있다.
            ["turn"] = FullLock & ~(InputLockFlags.Look | InputLockFlags.Locomotion),

            // 가리키기: 상호작용을 연다.
            ["point"] = FullLock & ~(InputLockFlags.Look | InputLockFlags.Locomotion |
                                     InputLockFlags.Interact),

            // 잡기: 잡기를 연다.
            ["grab"] = FullLock & ~(InputLockFlags.Look | InputLockFlags.Locomotion |
                                    InputLockFlags.Interact | InputLockFlags.Grab),

            // 놓기: grab에서 연 것을 그대로 쓴다.
            ["place"] = FullLock & ~(InputLockFlags.Look | InputLockFlags.Locomotion |
                                     InputLockFlags.Interact | InputLockFlags.Grab),

            // PTT: 마지막 조작이라 여기서 전부 열린다.
            ["ptt"] = InputLockFlags.None
        };

    [Header("참조")]
    [Tooltip("비우면 같은 트리에서, 없으면 씬에서 찾습니다.")]
    [SerializeField] QuestManager quest;

    [Tooltip("비우면 씬에서 찾습니다.")]
    [SerializeField] Player player;

    [Header("동작")]
    [Tooltip("끄면 잠금 없이 대사와 주시 연출만 수행합니다.")]
    [SerializeField] bool restrictInput = true;

    [Tooltip("화자가 바뀐 대사 첫 줄에서 플레이어 시선을 발화자 쪽으로 돌립니다. XR 장치가 켜져 있으면 생략합니다.")]
    [SerializeField] bool turnToSpeaker = true;

    [Tooltip("목표 대사(인트로·재안내·성공)에 맞춰 이음이 리액션을 재생합니다.")]
    [SerializeField] bool syncIeumiReactions = true;

    [Tooltip("시선을 돌리는 데 걸리는 시간(초).")]
    [SerializeField, Min(0f)] float turnSeconds = 1f;

    [Tooltip("노장 루트에서 얼굴까지의 높이(m). 발밑이 아니라 얼굴을 보게 만듭니다.")]
    [SerializeField, Min(0f)] float speakerEyeHeight = 1.5f;

    IDisposable _lock;
    InputLockFlags _lockedFlags = InputLockFlags.None;
    Coroutine _turn;
    bool _faceIsHeadBone;
    bool _subscribed;
    bool _dialogueHooked;

    /// <summary>마지막으로 주시 회전을 건 화자. 화자가 바뀔 때만 다시 돌리기 위한 기준이다.</summary>
    DialogueSpeaker? _watchedSpeaker;

    /// <summary>마지막으로 리액션 큐를 판정한 시퀀스 ID. 시퀀스 첫 줄에서 한 번만 판정한다.</summary>
    string _cueSequenceId;

    /// <summary>성공 대사 시퀀스 ID. 이 시퀀스가 끝까지 재생되면 백덤블링을 건다.</summary>
    string _pendingHappySequenceId;

    NojangBehaviour _nojang;
    EeumBehaviour _ieumi;

    void Awake()
    {
        if (quest == null) quest = GetComponentInChildren<QuestManager>(true);
        if (quest == null) quest = FindAnyObjectByType<QuestManager>();
        if (quest == null)
        {
            Debug.LogWarning("[TutorialFlow] 씬에 QuestManager가 없어 튜토리얼 진행을 통제하지 않습니다.", this);
            enabled = false;
            return;
        }

        if (player == null) player = FindAnyObjectByType<Player>();

        quest.QuestChanged += OnQuestChanged;
        quest.ObjectiveChanged += OnObjectiveChanged;
        quest.StateChanged += OnStateChanged;
        _subscribed = true;

        HookDialogue();
    }

    /// <summary>
    /// 시작 이벤트를 놓친 경우(이 컴포넌트가 늦게 깨어났거나, 튜토리얼 도중 껐다 켠 경우)를 위해
    /// 현재 상태로 한 번 맞춘다. <see cref="OnDisable"/>이 토큰을 놓기 때문에 다시 켤 때 복구가
    /// 필요하다.
    /// </summary>
    void OnEnable()
    {
        if (quest == null) return;

        HookDialogue();

        if (!IsTutorial) return;

        if (quest.State == QuestRuntimeState.Objective && quest.CurrentNode != null)
            ApplyForNode(quest.CurrentNode.Id);
        else if (quest.State == QuestRuntimeState.Intro)
            SetLock(FullLock);
    }

    void OnDisable()
    {
        UnhookDialogue();
        ReleaseAll();
    }

    void OnDestroy()
    {
        UnhookDialogue();
        ReleaseAll();

        if (!_subscribed || quest == null) return;
        quest.QuestChanged -= OnQuestChanged;
        quest.ObjectiveChanged -= OnObjectiveChanged;
        quest.StateChanged -= OnStateChanged;
        _subscribed = false;
    }

    /// <summary>
    /// 대사 이벤트를 구독한다. InGameDialogue는 지연 생성 싱글턴이라 Awake 시점에 없을 수 있어
    /// OnEnable과 퀘스트 시작에서 다시 시도한다. 대사 첫 줄(LineStarted)은 비동기 합성 이후에야
    /// 나가므로 퀘스트 시작 직후 재시도로도 놓치지 않는다.
    /// </summary>
    void HookDialogue()
    {
        if (_dialogueHooked) return;
        if (!InGameDialogue.TryGetInstance(out var dialogue)) return;

        dialogue.LineStarted += OnDialogueLineStarted;
        dialogue.SequenceFinished += OnDialogueSequenceFinished;
        _dialogueHooked = true;
    }

    void UnhookDialogue()
    {
        if (!_dialogueHooked) return;
        _dialogueHooked = false;

        if (!InGameDialogue.TryGetInstance(out var dialogue)) return;
        dialogue.LineStarted -= OnDialogueLineStarted;
        dialogue.SequenceFinished -= OnDialogueSequenceFinished;
    }

    bool IsTutorial => quest != null && quest.Process == ProcessId.Tutorial;

    /// <summary>
    /// 퀘스트가 시작될 때마다 한 번. QuestManager는 <c>BeginQuest</c>에서 Process를 정한 뒤에
    /// 이 이벤트를 보내므로 여기서 공정을 판별할 수 있다.
    /// </summary>
    void OnQuestChanged(QuestDefinition definition)
    {
        // 퀘스트 단위 연출 상태를 초기화한다. 새 퀘스트 첫 대사는 항상 주시 회전 대상이 된다.
        _watchedSpeaker = null;
        _cueSequenceId = null;
        _pendingHappySequenceId = null;

        if (definition == null || definition.Process != ProcessId.Tutorial)
        {
            // 같은 씬에서 제작 공정으로 이어지는 경우가 여기다. 남은 잠금을 넘기지 않는다.
            ReleaseAll();
            return;
        }

        // Awake 시점에 InGameDialogue가 아직 없었을 수 있어 여기서 다시 구독을 시도한다.
        HookDialogue();
        SetLock(FullLock);
    }

    void OnObjectiveChanged(QuestNodeData node, int index)
    {
        if (!IsTutorial || node == null) return;
        ApplyForNode(node.Id);
    }

    void OnStateChanged(QuestRuntimeState state)
    {
        switch (state)
        {
            // 완료 대사부터는 자유 행동으로 돌려준다. Missing·Dormant는 이 러너가 튜토리얼을
            // 실행하지 않는다는 뜻이므로 잠근 채 남겨 두면 그대로 진행 불가가 된다.
            case QuestRuntimeState.Complete:
            case QuestRuntimeState.Leaving:
            case QuestRuntimeState.Missing:
            case QuestRuntimeState.Dormant:
                ReleaseAll();
                break;
        }
    }

    void ApplyForNode(string nodeId)
    {
        if (!restrictInput) return;

        if (string.IsNullOrWhiteSpace(nodeId) || !RemainingLocks.TryGetValue(nodeId, out var remaining))
        {
            Debug.LogWarning(
                $"[TutorialFlow] 잠금 스케줄에 없는 목표 노드 '{nodeId}'입니다. " +
                "요구 입력을 알 수 없어 잠금을 모두 해제합니다. quest.json이 바뀌었다면 " +
                $"{nameof(TutorialFlowDirector)}의 스케줄도 함께 고쳐야 합니다.", this);
            SetLock(InputLockFlags.None);
            return;
        }

        SetLock(remaining);
    }

    /// <summary>
    /// 잠금 집합을 통째로 갈아 끼운다. 새 토큰을 먼저 잡고 이전 토큰을 놓아, 두 호출 사이에
    /// 잠금이 잠시 전부 풀리는 프레임을 만들지 않는다.
    /// </summary>
    void SetLock(InputLockFlags flags)
    {
        if (!restrictInput) flags = InputLockFlags.None;
        if (_lock != null && _lockedFlags == flags) return;

        var next = flags == InputLockFlags.None ? null : InputLockService.Acquire(flags, LockReason);
        _lock?.Dispose();
        _lock = next;
        _lockedFlags = flags;
    }

    void ReleaseAll()
    {
        _lock?.Dispose();
        _lock = null;
        _lockedFlags = InputLockFlags.None;

        if (_turn == null) return;
        StopCoroutine(_turn);
        _turn = null;
    }

    /// <summary>
    /// 대사 줄이 실제로 재생되는 순간을 연출 기준으로 삼는다. 퀘스트 이벤트(ObjectiveChanged 등)는
    /// 대사보다 스텝 간격만큼 먼저 도착해 연출이 음성과 어긋나고, 인트로 대사에는 대응하는 퀘스트
    /// 이벤트 자체가 없다.
    /// </summary>
    void OnDialogueLineStarted(DialogueLine line)
    {
        if (!IsTutorial || line == null) return;

        HandleSequenceCue();

        // Speaker는 로드 시 시퀀스 화자로 정규화되어 실제로는 항상 값이 있다. 내레이션은 화자
        // 위치가 없으므로 주시 대상에서 빼고, 직전 화자 기억도 바꾸지 않는다.
        if (line.Speaker == null || line.Speaker == DialogueSpeaker.Narration) return;

        var speaker = line.Speaker.Value;
        if (_watchedSpeaker == speaker) return;

        _watchedSpeaker = speaker;
        BeginTurnToSpeaker(speaker);
    }

    /// <summary>
    /// 시퀀스 첫 줄에서 이음이 리액션을 대사 종류에 맞춰 건다. 인트로·재안내는 대사 시작과 함께,
    /// 성공은 시퀀스 종료 후로 미룬다(백덤블링은 시선을 통째로 놓는 연출이라 말하는 도중에 걸면
    /// 발화자를 막 바라본 플레이어와 시선이 어긋난다).
    /// </summary>
    void HandleSequenceCue()
    {
        if (!syncIeumiReactions) return;
        if (!InGameDialogue.TryGetInstance(out var dialogue)) return;

        var sequence = dialogue.Current;
        if (sequence == null ||
            string.Equals(sequence.Id, _cueSequenceId, StringComparison.OrdinalIgnoreCase))
            return;

        _cueSequenceId = sequence.Id;

        var objective = quest.CurrentObjective;
        if (objective == null) return;

        if (MatchesDialogue(sequence.Id, objective.IntroDialogue))
        {
            var ieumi = ResolveIeumi();
            if (ieumi != null) ieumi.PlayGreetAnim(GreetSeconds);
        }
        else if (MatchesDialogue(sequence.Id, objective.RetryDialogue))
        {
            var ieumi = ResolveIeumi();
            if (ieumi != null) ieumi.PlayWonderAnim(WonderSeconds);
        }
        else if (MatchesDialogue(sequence.Id, objective.SuccessDialogue))
        {
            _pendingHappySequenceId = sequence.Id;
        }
    }

    void OnDialogueSequenceFinished(DialogueSequence sequence, bool completed)
    {
        // 같은 시퀀스가 연달아 다시 재생될 수 있으므로(재안내 반복) 판정 기준을 비운다.
        _cueSequenceId = null;

        if (!IsTutorial || !syncIeumiReactions || sequence == null) return;
        if (!MatchesDialogue(sequence.Id, _pendingHappySequenceId)) return;

        _pendingHappySequenceId = null;

        // 끊긴 대사(전체 건너뛰기 등)에는 축하 리액션을 걸지 않는다.
        if (!completed) return;

        var ieumi = ResolveIeumi();
        if (ieumi != null) ieumi.PlayHappyReaction(HappySeconds);
    }

    static bool MatchesDialogue(string sequenceId, string dialogueId) =>
        !string.IsNullOrWhiteSpace(dialogueId) &&
        string.Equals(sequenceId, dialogueId, StringComparison.OrdinalIgnoreCase);

    NojangBehaviour ResolveNojang()
    {
        if (_nojang == null) _nojang = FindAnyObjectByType<NojangBehaviour>();
        return _nojang;
    }

    EeumBehaviour ResolveIeumi()
    {
        if (_ieumi == null) _ieumi = FindAnyObjectByType<EeumBehaviour>();
        return _ieumi;
    }

    /// <summary>
    /// 화자가 바뀐 첫 줄에서 플레이어가 그 발화자를 보게 한다. 인트로에서는 Look이 잠겨 있어
    /// 입력과 충돌하지 않고, Look이 열린 목표에서는 인트로와 동일하게 1초 동안 강제 회전이
    /// 마우스 입력 위에 얹힌다.
    /// </summary>
    void BeginTurnToSpeaker(DialogueSpeaker speaker)
    {
        if (!turnToSpeaker || !isActiveAndEnabled) return;

        // HMD 착용 상태에서 시점을 강제로 돌리면 멀미 요인이 된다(F-001 안전 원칙). XR에서는
        // 회전을 생략하고 로그만 남긴다 — VR에서 어떻게 주시를 유도할지는 실기기 검증 항목이다
        // (이슈 로그 ISSUE-028).
        if (XRSettings.isDeviceActive)
        {
            Debug.Log($"[TutorialFlow] XR 장치가 활성이라 발화자({speaker}) 주시 회전을 생략합니다.");
            return;
        }

        if (player == null) player = FindAnyObjectByType<Player>();
        if (player == null || player.View == null) return;

        var face = ResolveSpeakerFace(speaker);
        if (face == null)
        {
            Debug.Log($"[TutorialFlow] 씬에서 발화자({speaker})를 찾지 못해 주시 회전을 생략합니다.");
            return;
        }

        if (_turn != null) StopCoroutine(_turn);
        _turn = StartCoroutine(TurnRoutine(face));
    }

    /// <summary>화자별 조준점. 노장은 휴머노이드 머리 본, 이음이는 GazeTracker 머리 트랜스폼.</summary>
    Transform ResolveSpeakerFace(DialogueSpeaker speaker)
    {
        switch (speaker)
        {
            case DialogueSpeaker.Nojang:
            {
                var nojang = ResolveNojang();
                return nojang != null ? ResolveFace(nojang.transform) : null;
            }

            case DialogueSpeaker.Ieumi:
            {
                var ieumi = ResolveIeumi();
                if (ieumi == null) return null;

                // 이음이 FBX는 Generic 리그라 ResolveFace의 휴머노이드 머리 본 조회가 통하지 않아
                // 루트+고정 오프셋 폴백으로 떨어지는데, 부유체는 루트가 이미 눈높이 근처라 그
                // 오프셋이 머리 위 허공을 조준한다. GazeTracker가 쓰는 머리 트랜스폼을 재사용하고
                // 오프셋 없이 그대로 겨눈다(FaceTransform의 루트 폴백 포함).
                _faceIsHeadBone = true;
                return ieumi.FaceTransform;
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// 조준점은 휴머노이드 머리 본을 우선한다. 맵 스케일이 커서 루트 기준 고정 오프셋(m)은
    /// 실제 얼굴 높이와 어긋난다 — 오프셋 폴백은 리그가 없는 대역 모델용으로만 남긴다.
    /// </summary>
    Transform ResolveFace(Transform speaker)
    {
        _faceIsHeadBone = false;

        var animator = speaker.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
            {
                _faceIsHeadBone = true;
                return headBone;
            }
        }

        return speaker;
    }

    /// <summary>
    /// <see cref="PauseService.Now"/>로 진행한다. 일시정지 중에는 이 시각이 멈추므로 회전도 함께
    /// 멈추고, timeScale과 무관하게 같은 속도로 돈다.
    /// </summary>
    IEnumerator TurnRoutine(Transform speaker)
    {
        var view = player.View;
        var head = player.Head;
        var from = head.forward;
        var elapsed = 0f;
        var last = PauseService.Now;

        while (true)
        {
            var to = SpeakerDirection(speaker, head);
            var t = turnSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / turnSeconds);

            // 시작과 끝을 부드럽게 — 등속으로 돌리면 시작하는 순간이 튄다.
            view.SetLookDirection(Vector3.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t)));

            if (t >= 1f) break;

            yield return null;

            if (speaker == null || player == null) break;

            var now = PauseService.Now;
            elapsed += Mathf.Max(0f, now - last);
            last = now;
        }

        _turn = null;
    }

    Vector3 SpeakerDirection(Transform face, Transform head)
    {
        // 머리 본이면 오프셋 없이 그대로, 루트 폴백이면 오프셋으로 얼굴 높이를 근사한다.
        var target = _faceIsHeadBone
            ? face.position
            : face.position + Vector3.up * speakerEyeHeight;

        var direction = target - head.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : head.forward;
    }
}
