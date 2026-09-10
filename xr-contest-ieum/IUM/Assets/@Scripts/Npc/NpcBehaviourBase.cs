using System.Collections;
using System.Collections.Generic;
using GazeSystem;
using UnityEngine;

/// <summary>
/// 이음이·노장 행동 컴포넌트의 공용 골격. paaalop 소유 원본
/// (GazeSystem/EeumAnimationTrigger·ElderlyAnimationTrigger)을 수정하지 않기 위해 신규로 재작성한
/// 우리 소유 구현이며, 겉보기 동작은 원본과 동일한 것을 목표로 한다. 시선·고개짓 자체는 paaalop
/// 라이브러리(GazeTracker·NPCGazeController)의 공개 API 호출로만 수행하고 여기서는 "언제 무엇을
/// 하는가"만 담당한다.
///
/// 원본 두 파일에서 절반 이상이 서로 복사본이었다: 행동 큐와 순차 실행기, 시선락·watch 제어,
/// 시선 오버라이드 판정, 플레이어 정면 정렬, 대상 탐색 헬퍼. 그 공통 골격이 이 클래스다.
/// NPC별 고유 연출(이음이의 비행 추종, 노장의 순찰·복귀 대화)은 파생 클래스에 있다.
///
/// 데이터 계약: 직렬화 필드명과 공개 메서드명은 원본과 동일하게 유지한다. GongpoScene 인스턴스의
/// 값 이전과 UnityEvent(onArrived) 재바인딩을 PlayNpcBuilder가 이름 기준으로 수행하기 때문이다.
/// </summary>
[RequireComponent(typeof(Animator))]
public abstract class NpcBehaviourBase : MonoBehaviour
{
    [Header("이동 경로")]
    [SerializeField]
    [Tooltip("순찰/작업 경로와 도착 시 실행할 이벤트 목록. 비어 있으면 제자리 대기 연출만 돈다.")]
    protected List<WaypointAction> patrolRoutes = new();

    protected Animator animator;
    protected GazeTracker gazeTracker;
    protected NPCGazeController gazeController;

    /// <summary>초기 patrolRoutes와 동적 등록분을 순서대로 소비하는 이동 경로 큐.</summary>
    protected readonly Queue<WaypointAction> routeQueue = new();

    /// <summary>
    /// onArrived UnityEvent는 슬롯을 한꺼번에 호출하므로, Play* 계열이 태스크만 쌓고
    /// <see cref="CoExecuteActionQueue"/>가 순차로 소비해 "동시 호출 → 순차 실행"을 만든다.
    /// </summary>
    protected readonly Queue<ActionTask> actionQueue = new();

    protected Vector3 startPosition;
    protected Quaternion startRotation;

    protected bool isPerformingAction;
    protected ActionType currentRunningTaskType = ActionType.Crouch;

    protected bool isGazeLocked;
    protected bool lastGazeLockState;
    protected bool watch;
    protected bool lastWatchState;

    protected Transform cachedObjectTarget;
    protected Transform currentActiveLookTarget;

    /// <summary>순찰 중이 아니라 제자리(홈)에서 플레이어와 마주 보는 상태인지.</summary>
    protected bool isAtHomeBase;

    Coroutine _sequenceCoroutine;
    Coroutine _gazeLockTimerCoroutine;

    /// <summary>이동 속도(m/s). 기본값과 툴팁이 NPC마다 달라 파생이 직렬화 필드로 소유한다.</summary>
    protected abstract float MoveSpeed { get; }

    /// <summary>회전 보간 속도. 위와 같은 이유로 파생 소유다.</summary>
    protected abstract float RotationSpeed { get; }

    #region ================= 시선락 · watch 공개 API =================

    /// <summary>물리적으로 대상을 강제 응시하는 시선락을 켜고 끈다. 애니메이터에는 관여하지 않는다.</summary>
    public void SetGazeLock(bool enabled) => isGazeLocked = enabled;

    public bool GetGazeLock() => isGazeLocked;

    /// <summary>애니메이터의 'watch'(Bool) 파라미터를 직접 켜고 끈다. 물리 시선 조준에는 관여하지 않는다.</summary>
    public void SetWatchAnim(bool enabled)
    {
        watch = enabled;
        if (animator != null && HasParameter(animator, "watch"))
            animator.SetBool("watch", enabled);
    }

    public bool GetWatchAnim() => watch;

    #endregion

    #region ================= 인스펙터 이벤트 바인딩용 행동 등록 API =================

    // 메서드명은 원본과 동일하게 유지한다 — UnityEvent 퍼시스턴트 바인딩이 이름 문자열로 걸린다.

    /// <summary>지정 시간 동안 물리 시선을 작업 대상에 고정한다.</summary>
    public void PlayGazeLock(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.GazeLock, duration = duration });

    /// <summary>바로 다음에 등록된 행동의 지속시간을 상속받아 그 동안 시선을 고정한다.</summary>
    public void PlayGazeLock() =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.GazeLock, duration = -1f });

    public void PlayWatchAnim(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.WatchAnim, duration = duration });

    public void PlayNodReaction(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.Nod, duration = duration });

    public void PlayShakeReaction(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.Shake, duration = duration });

    public void PlayFaceGaze(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.FaceGaze, duration = duration });

    public void PlayAutomaticGaze(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.AutomaticGaze, duration = duration });

    #endregion

    protected virtual void Start()
    {
        animator = GetComponent<Animator>();

        // 루트 모션이 이동 코루틴과 싸우면 바닥 파묻힘이 나므로 원본과 같이 원천 차단한다.
        if (animator != null) animator.applyRootMotion = false;

        if (!AcquireGazeComponents()) return;

        startPosition = transform.position;
        startRotation = transform.rotation;

        if (patrolRoutes != null)
            foreach (var route in patrolRoutes)
                if (route != null && route.waypoint != null)
                    routeQueue.Enqueue(route);

        FindDefaultObjectTarget();

        _sequenceCoroutine = StartCoroutine(CoSequenceLoop());

        lastGazeLockState = isGazeLocked;
        lastWatchState = watch;
    }

    /// <summary>
    /// 시선 스택을 확보한다. 확보 방식이 NPC마다 다르다 — 이음이는 없으면 스스로 붙이고,
    /// 노장은 프리팹에 있는 것을 요구한다. false를 돌려주면 시퀀스를 시작하지 않는다.
    /// </summary>
    protected abstract bool AcquireGazeComponents();

    /// <summary>NPC별 메인 행동 루프. 이음이는 추종·대기 릴레이, 노장은 순찰·복귀 대화다.</summary>
    protected abstract IEnumerator CoSequenceLoop();

    /// <summary>목적지까지 이동한다. 비행(이음이)과 보행(노장)이 달라 파생이 구현한다.</summary>
    protected abstract IEnumerator CoWalkToPosition(
        Vector3 targetPos, bool isAtWaypoint, Transform customLookTarget = null);

    /// <summary>시선 오버라이드가 풀렸을 때 돌아갈 기본 시선 상태. NPC마다 정책이 다르다.</summary>
    protected abstract void RestoreSequenceGazeTarget();

    /// <summary>행동 종료 시 애니메이터를 중립으로 되돌린다. 파라미터 구성이 NPC마다 다르다.</summary>
    protected abstract void ResetAllAnimatorActions();

    /// <summary><see cref="CoExecuteActionQueue"/>가 태스크 하나를 실제 연출로 바꾼다.</summary>
    protected abstract IEnumerator RunAction(ActionTask task);

    /// <summary>태스크 시작 직전 훅. 노장은 여기서 이전 애니메이터 상태를 정리한다.</summary>
    protected virtual void OnBeforeAction(ActionTask task) { }

    protected virtual void OnDisable()
    {
        if (_sequenceCoroutine != null) StopCoroutine(_sequenceCoroutine);
    }

    #region ================= 프레임 틱 공용 조각 =================

    /// <summary>애니메이터 쪽에서 'watch'가 바뀌었으면 스크립트 상태를 따라가게 한다.</summary>
    protected void TickWatchSync()
    {
        if (animator == null || !HasParameter(animator, "watch")) return;

        var animWatch = animator.GetBool("watch");
        if (animWatch != watch) watch = animWatch;
    }

    /// <summary>
    /// 물리 시선 오버라이드 판정. 시선락·watch가 켜져 있거나, 순찰 중 작업 연출(플레이어 대면형
    /// 제외)이 돌고 있으면 작업 대상을 강제 응시하고, 조준이 풀리는 순간 기본 시선으로 복원한다.
    /// 프레임 말미에 <see cref="CommitGazeStates"/>를 함께 불러야 복원 판정이 한 번만 일어난다.
    /// </summary>
    protected void TickGazeOverride()
    {
        var isPerformingWorkGaze = isPerformingAction &&
                                   currentRunningTaskType != ActionType.FaceGaze &&
                                   currentRunningTaskType != ActionType.AutomaticGaze;

        var shouldGazeAtTarget = isGazeLocked || watch || (!isAtHomeBase && isPerformingWorkGaze);

        if (shouldGazeAtTarget)
        {
            var targetTrans = currentActiveLookTarget != null ? currentActiveLookTarget : cachedObjectTarget;
            var target = GetGazeTargetFromTransform(targetTrans) ?? FindPlayerGazeTarget();

            if (target != null && gazeTracker != null) gazeTracker.SetTarget(target);
        }
        else if (lastGazeLockState || lastWatchState ||
                 (!isPerformingAction && !isAtHomeBase &&
                  currentRunningTaskType != ActionType.FaceGaze &&
                  currentRunningTaskType != ActionType.AutomaticGaze))
        {
            RestoreSequenceGazeTarget();
        }
    }

    /// <summary>이번 프레임의 시선락·watch 상태를 다음 프레임 비교 기준으로 남긴다.</summary>
    protected void CommitGazeStates()
    {
        lastGazeLockState = isGazeLocked;
        lastWatchState = watch;
    }

    #endregion

    #region ================= 공용 행동 코루틴 =================

    /// <summary>
    /// 큐에 쌓인 태스크를 순차 소비한다. 시선락만은 대기 없이 백그라운드로 돌리고 즉시 다음
    /// 태스크로 넘어간다 — "시선을 고정한 채 다른 행동"이 한 이벤트 목록으로 설계되게 하기 위해서다.
    /// </summary>
    protected IEnumerator CoExecuteActionQueue()
    {
        while (actionQueue.Count > 0)
        {
            var task = actionQueue.Dequeue();

            if (task.type == ActionType.GazeLock)
            {
                if (_gazeLockTimerCoroutine != null) StopCoroutine(_gazeLockTimerCoroutine);
                _gazeLockTimerCoroutine = StartCoroutine(CoPlayGazeLockInternal(task.duration));
                continue;
            }

            // currentRunningTaskType 기록은 OnBeforeAction 몫이다. 원본에서 노장만 기록했고
            // 이음이는 기록하지 않았는데, 시선 오버라이드 판정이 이 값을 읽으므로 차이를 보존한다.
            OnBeforeAction(task);
            yield return StartCoroutine(RunAction(task));
        }

        ResetAllAnimatorActions();
        isPerformingAction = false;
        currentRunningTaskType = ActionType.Crouch;
        currentActiveLookTarget = null;
    }

    protected IEnumerator CoPlayGazeLockInternal(float duration)
    {
        var targetDuration = duration;

        // 상속 모드(-1): 다음 태스크의 지속시간을 엿보아 차용한다. 없으면 3초 폴백.
        if (targetDuration < 0f)
        {
            targetDuration = 3.0f;
            if (actionQueue.Count > 0)
            {
                var nextTask = actionQueue.Peek();
                if (nextTask != null && nextTask.duration > 0f) targetDuration = nextTask.duration;
            }
        }

        SetGazeLock(true);
        yield return new WaitForSeconds(targetDuration);
        SetGazeLock(false);

        ResumeOrDisableGaze();
    }

    protected IEnumerator CoPlayWatchAnimInternal(float duration)
    {
        SetWatchAnim(true);
        yield return new WaitForSeconds(duration);
        SetWatchAnim(false);
        yield return new WaitForSeconds(2.0f);
    }

    /// <summary>플레이어 얼굴만 집중 응시. 시작 전에 몸을 플레이어 쪽으로 정렬한다.</summary>
    protected IEnumerator CoFaceGazeCore(float duration)
    {
        yield return StartCoroutine(CoAlignBodyToPlayer());

        if (gazeController != null) gazeController.ForceGazeToType(GazeTargetType.Face);

        yield return new WaitForSeconds(duration);

        ResumeOrDisableGaze();
        yield return new WaitForSeconds(2.0f);
    }

    /// <summary>플레이어 얼굴-손 자동 번갈아 보기. 시작 전에 몸을 플레이어 쪽으로 정렬한다.</summary>
    protected IEnumerator CoAutomaticGazeCore(float duration)
    {
        yield return StartCoroutine(CoAlignBodyToPlayer());

        if (gazeController != null) gazeController.ResumeAutomaticGaze();

        yield return new WaitForSeconds(duration);

        // 원본이 이 행동에서만 홈 상태를 다시 켜지 않던 것을 그대로 둔다.
        if (gazeController != null && !isAtHomeBase) gazeController.DisableGaze();
        yield return new WaitForSeconds(2.0f);
    }

    /// <summary>0.8초에 걸쳐 몸통을 플레이어 방향으로 돌리고 정확히 마주 보게 고정한다.</summary>
    protected IEnumerator CoAlignBodyToPlayer()
    {
        var playerTrans = FindPlayerTransform();
        if (playerTrans == null) yield break;

        yield return StartCoroutine(CoAlignBodyTo(playerTrans.position));
    }

    /// <summary>
    /// 0.8초에 걸쳐 몸통을 지정 위치 방향(수평)으로 돌린다. 원본 4곳의 반복 블록이다.
    /// 플레이어 대면은 마지막에 정확히 마주 보게 스냅하고, 작업물 대면은 원본대로 보간까지만 한다.
    /// </summary>
    protected IEnumerator CoAlignBodyTo(Vector3 worldPosition, bool snapAtEnd = true)
    {
        var direction = worldPosition - transform.position;
        direction.y = 0;
        if (direction.magnitude <= 0.1f) yield break;

        var targetRotation = Quaternion.LookRotation(direction);
        const float alignSeconds = 0.8f;
        var elapsed = 0f;
        while (elapsed < alignSeconds)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation, Time.deltaTime * RotationSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (snapAtEnd) transform.rotation = targetRotation;
    }

    /// <summary>
    /// 행동이 끝난 뒤의 시선 복귀 공통 규칙: 홈에서 대화 중이면 자동 시선 재개, 순찰 중이면
    /// 플레이어를 흘끔거리지 않도록 시선을 끈다. 원본 전체에서 여덟 번 반복되던 분기다.
    /// </summary>
    protected void ResumeOrDisableGaze()
    {
        if (gazeController == null) return;

        if (isAtHomeBase) gazeController.ResumeAutomaticGaze();
        else gazeController.DisableGaze();
    }

    /// <summary>
    /// 경로 하나를 소비한다: 목적지 이동 → 도착 이벤트가 있으면 순차 실행, 없으면 NPC별 기본
    /// 대기 연출 → 초기 경로였다면 무한 순찰을 위해 큐 끝에 재삽입.
    /// </summary>
    protected IEnumerator CoProcessRoute(WaypointAction route)
    {
        if (route == null || route.waypoint == null) yield break;

        currentActiveLookTarget = route.lookTarget != null ? route.lookTarget : cachedObjectTarget;
        yield return StartCoroutine(CoWalkToPosition(
            route.waypoint.position, isAtWaypoint: true, customLookTarget: route.lookTarget));

        if (route.onArrived != null && route.onArrived.GetPersistentEventCount() > 0)
        {
            actionQueue.Clear();
            isPerformingAction = true;
            route.onArrived.Invoke();
            yield return StartCoroutine(CoExecuteActionQueue());
        }
        else
        {
            yield return StartCoroutine(CoIdleAtWaypoint(route));
        }

        if (patrolRoutes.Contains(route)) routeQueue.Enqueue(route);
    }

    /// <summary>도착 이벤트가 비어 있을 때의 기본 대기 연출. 기본은 3초 정지다.</summary>
    protected virtual IEnumerator CoIdleAtWaypoint(WaypointAction route)
    {
        yield return new WaitForSeconds(3.0f);
    }

    #endregion

    #region ================= 대상 탐색 헬퍼 =================

    /// <summary>씬 전역의 기본 작업 대상 오브젝트를 이름으로 캐싱한다. 없어도 무방하다.</summary>
    protected void FindDefaultObjectTarget()
    {
        var targetObj = GameObject.Find("ObjectTarget");
        if (targetObj == null) targetObj = GameObject.Find("WorkTarget");
        if (targetObj != null) cachedObjectTarget = targetObj.transform;
    }

    /// <summary>
    /// 트랜스폼을 시선 대상으로 바꾼다. GazeTarget이 없으면 붙여 주는데, 분류 필드가 라이브러리
    /// 내부(private)라 원본과 같은 리플렉션 경로로 Other를 넣는다 — paaalop 파일을 고치지 않고
    /// 같은 결과를 내는 유일한 방법이다.
    /// </summary>
    protected IGazeTarget GetGazeTargetFromTransform(Transform targetTrans)
    {
        if (targetTrans == null) return null;

        var target = targetTrans.GetComponent<GazeTarget>();
        if (target == null)
        {
            target = targetTrans.gameObject.AddComponent<GazeTarget>();
            var typeField = typeof(GazeTarget).GetField(
                "targetType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            typeField?.SetValue(target, GazeTargetType.Other);
        }

        return target;
    }

    /// <summary>플레이어 얼굴 GazeTarget을 씬에서 찾는다. PlayerGazeTargetAutoSetup이 만들어 둔다.</summary>
    protected IGazeTarget FindPlayerGazeTarget()
    {
        foreach (var target in Object.FindObjectsByType<GazeTarget>(FindObjectsSortMode.None))
            if (target.TargetType == GazeTargetType.Face)
                return target;

        return null;
    }

    /// <summary>
    /// 플레이어 몸통 트랜스폼. PlayerMovement(paaalop 개발 씬) → 'Player' 태그 → 메인 카메라 순으로
    /// 폴백한다. 원본 노장은 PlayerMovement만 봐서 우리 Play 씬(공용 Player, 태그 없음)에서는
    /// 대면 정렬이 통째로 죽었다 — 이 폴백 체인이 원본 이음이가 이미 쓰던 경로라 그대로 공용화했다.
    /// </summary>
    protected Transform FindPlayerTransform()
    {
        var movement = Object.FindAnyObjectByType<PlayerMovement>();
        if (movement != null) return movement.transform;

        var tagged = GameObject.FindWithTag("Player");
        if (tagged != null) return tagged.transform;

        var mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    protected static bool HasParameter(Animator target, string parameterName)
    {
        foreach (var parameter in target.parameters)
            if (parameter.name == parameterName)
                return true;

        return false;
    }

    protected static AnimatorControllerParameterType GetParameterType(Animator target, string parameterName)
    {
        foreach (var parameter in target.parameters)
            if (parameter.name == parameterName)
                return parameter.type;

        return AnimatorControllerParameterType.Trigger;
    }

    #endregion
}
