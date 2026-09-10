using System.Collections;
using GazeSystem;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 노장 행동. paaalop 원본 GazeSystem/ElderlyAnimationTrigger의 우리 소유 재작성판으로, 공용 골격은
/// <see cref="NpcBehaviourBase"/>에 있고 여기에는 노장 고유 연출만 남는다:
///
/// - 중량감: 애니메이터 전체 배속을 낮춰(기본 0.7) 노인의 느릿한 몸짓을 만든다.
/// - 순찰: 경로 목록을 돌며 목적지마다 도착 이벤트(웅크려 관찰·끄덕·절레 등)를 수행한다.
/// - 자유이동(추종): 웨이포인트 큐가 비어 있으면 XZ 사각 영역 안에서 플레이어를 따라 걷고,
///   가까워지면 멈춰 마주 본 채 끄덕(3초)→절레(3초) 고개짓 대화를 돈다. 플레이어가 영역 밖이면
///   경계까지만 접근한다. 인게임 대사 재생 중에는 이동을 멈추고 플레이어를 바라본다 (대사 > 추종).
///   <see cref="SetFreeRoamEnabled"/>로 끄면 그 자리에서 고개짓 대기만 돈다.
/// </summary>
public sealed class NojangBehaviour : NpcBehaviourBase
{
    [Header("노장 연출")]
    [Range(0.3f, 1.2f)]
    [SerializeField]
    [Tooltip("애니메이션 전체 배속. 0.6~0.75가 노장의 묵직한 움직임에 맞는다.")]
    float elderlyAnimationSpeed = 0.7f;

    [Header("이동")]
    [SerializeField]
    [Tooltip("보행 속도(m/s).")]
    float moveSpeed = 1.5f;

    [SerializeField]
    [Tooltip("방향 전환 보간 속도.")]
    float rotationSpeed = 5f;

    [Header("이동 영역 (XZ 사각)")]
    [SerializeField]
    [Tooltip("이동 영역 중심(월드 좌표). (0,0,0)으로 두면 시작 시 스폰 위치를 중심으로 삼는다.")]
    Vector3 roamAreaCenter = Vector3.zero;

    [SerializeField]
    [Tooltip("이동 영역의 X·Z 변 길이(m). 모든 이동 목표가 이 사각 안으로 클램프된다.")]
    Vector2 roamAreaSize = new(50f, 50f);

    [Header("플레이어 추종")]
    [SerializeField]
    [Tooltip("자유이동(플레이어 추종) 사용 여부. 끄면 제자리 고개짓 대기만 돈다. SetFreeRoamEnabled로도 토글된다.")]
    bool freeRoamEnabled = true;

    [SerializeField]
    [Tooltip("추종 보행 속도(m/s). 순찰 속도(moveSpeed)와 별도로 튜닝한다.")]
    float followMoveSpeed = 6f;

    [SerializeField]
    [Tooltip("플레이어(영역 클램프 목표)와 이 거리(m) 이상 벌어지면 걸어서 따라간다.")]
    float followStartDistance = 12f;

    [SerializeField]
    [Tooltip("따라가다 이 거리(m) 안으로 들어오면 멈춰 선다. 시작 거리보다 작아야 한다.")]
    float followStopDistance = 5f;

    /// <summary>시작 시 확정된 이동 영역 중심. roamAreaCenter가 zero면 스폰 위치가 들어온다.</summary>
    Vector3 _roamCenterResolved;

    /// <summary>추종 근접 대기에서 끄덕/절레를 번갈아 재생하기 위한 토글.</summary>
    bool _freeRoamNodNext = true;

    protected override float MoveSpeed => moveSpeed;
    protected override float RotationSpeed => rotationSpeed;

    protected override void Start()
    {
        // 영역 중심 미설정(zero)이면 스폰 위치를 중심으로 삼는다 — 씬 수정 없이 코드 기본값만으로 돌게 한다.
        _roamCenterResolved = roamAreaCenter == Vector3.zero ? transform.position : roamAreaCenter;

        base.Start();
        ApplyElderlySpeed();
    }

    void OnValidate()
    {
        // 정지 거리가 시작 거리를 넘으면 걷기 시작·정지 판정이 매 프레임 진동하므로 막는다.
        if (followStopDistance > followStartDistance) followStopDistance = followStartDistance;

        // 인스펙터에서 배속을 만지면 플레이 중에도 바로 반영해 연출을 눈으로 조율할 수 있게 한다.
        if (Application.isPlaying && animator != null) ApplyElderlySpeed();
    }

    void OnDrawGizmosSelected()
    {
        // 이동 영역을 씬 뷰에 표시한다. 플레이 전에 중심이 미설정(zero)이면 현재 위치 기준으로 그린다.
        var center = roamAreaCenter;
        if (center == Vector3.zero)
            center = Application.isPlaying ? _roamCenterResolved : transform.position;

        Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 1f);
        Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(roamAreaSize.x), 0.1f, Mathf.Abs(roamAreaSize.y)));
    }

    /// <summary>노장은 프리팹에 시선 스택이 내장되어 있어야 한다. 없으면 시퀀스를 시작하지 않는다.</summary>
    protected override bool AcquireGazeComponents()
    {
        gazeTracker = GetComponent<GazeTracker>();
        gazeController = GetComponent<NPCGazeController>();

        if (gazeTracker == null || gazeController == null)
        {
            Debug.LogError(
                $"[{gameObject.name}] GazeTracker 혹은 NPCGazeController가 없어 시퀀스를 구동하지 못했습니다.", this);
            return false;
        }

        return true;
    }

    void Update()
    {
        TickWatchSync();
        TickGazeOverride();
        CommitGazeStates();
    }

    /// <summary>애니메이터 배속을 설정값으로 적용해 노장의 중량감을 만든다.</summary>
    public void ApplyElderlySpeed()
    {
        if (animator != null) animator.speed = elderlyAnimationSpeed;
    }

    /// <summary>런타임에 외부에서 이동/작업 목적지를 동적으로 끼워 넣는 진입점.</summary>
    public void EnqueueWaypoint(Transform newWaypoint, Transform customLookTarget = null, UnityAction customEvent = null)
    {
        if (newWaypoint == null) return;

        var route = new WaypointAction
        {
            waypoint = newWaypoint,
            lookTarget = customLookTarget,
            onArrived = new UnityEvent()
        };
        if (customEvent != null) route.onArrived.AddListener(customEvent);

        routeQueue.Enqueue(route);
    }

    /// <summary>웅크려서 대상 물체를 두리번두리번 훑어보는 관찰 행동을 등록한다.</summary>
    public void PlayCrouchAndLookAround(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.Crouch, duration = duration });

    protected override void OnBeforeAction(ActionTask task)
    {
        // 원본 노장만 태스크 종류를 기록하고 직전 애니메이션 상태를 정리한 뒤 다음 연출로 넘어간다.
        // (시선락은 병렬로 계속 돌아야 하므로 여기서 건드리지 않는다.)
        currentRunningTaskType = task.type;

        if (animator == null) return;
        animator.SetBool("crouching", false);
        animator.SetBool("back", false);
        animator.SetBool("cross", false);
        animator.ResetTrigger("crouch");
    }

    protected override IEnumerator RunAction(ActionTask task)
    {
        switch (task.type)
        {
            case ActionType.Crouch:
                yield return StartCoroutine(CoPlayCrouchAndLookAroundInternal(task.duration));
                break;
            case ActionType.Nod:
                yield return StartCoroutine(CoPlayNodReactionInternal(task.duration));
                break;
            case ActionType.Shake:
                yield return StartCoroutine(CoPlayShakeReactionInternal(task.duration));
                break;
            case ActionType.WatchAnim:
                yield return StartCoroutine(CoPlayWatchAnimInternal(task.duration));
                break;
            case ActionType.FaceGaze:
                yield return StartCoroutine(CoFaceGazeCore(task.duration));
                break;
            case ActionType.AutomaticGaze:
                yield return StartCoroutine(CoAutomaticGazeCore(task.duration));
                break;
        }
    }

    #region ================= 노장 고유 연출 코루틴 =================

    IEnumerator CoPlayCrouchAndLookAroundInternal(float duration)
    {
        SetCrouching(true);

        var activeLookTarget = currentActiveLookTarget != null ? currentActiveLookTarget : cachedObjectTarget;
        var target = GetGazeTargetFromTransform(activeLookTarget) ?? FindPlayerGazeTarget();

        if (target != null && gazeTracker != null)
        {
            gazeTracker.SetTarget(target);
            // 낮은 속도의 절레절레 = 찬찬히 두리번거리며 살펴보는 그림이 된다.
            gazeTracker.TriggerShake(duration: duration, speed: 3.0f, intensity: 10f);
        }

        yield return new WaitForSeconds(duration);

        SetCrouching(false);
        if (gazeController != null) gazeController.DisableGaze();

        yield return new WaitForSeconds(3.0f);
    }

    IEnumerator CoPlayNodReactionInternal(float duration)
    {
        if (animator != null) animator.SetBool("back", true);
        if (gazeTracker != null) gazeTracker.TriggerNod(duration, 10f, 13f);

        yield return new WaitForSeconds(duration);

        if (animator != null) animator.SetBool("back", false);
        ResumeOrDisableGaze();
        yield return new WaitForSeconds(2.0f);
    }

    IEnumerator CoPlayShakeReactionInternal(float duration)
    {
        if (animator != null) animator.SetBool("cross", true);
        if (gazeTracker != null) gazeTracker.TriggerShake(duration, 10f, 12f);

        yield return new WaitForSeconds(duration);

        if (animator != null) animator.SetBool("cross", false);
        ResumeOrDisableGaze();
        yield return new WaitForSeconds(2.0f);
    }

    /// <summary>웅크림 진입/기립. 기립은 CrossFade로 강제 전이해 상태 꼬임을 푼다.</summary>
    void SetCrouching(bool crouching)
    {
        if (animator == null) return;

        if (crouching)
        {
            animator.SetBool("cross", false);
            animator.SetBool("back", false);
            animator.SetTrigger("crouch");
            animator.SetBool("crouching", true);
        }
        else
        {
            animator.SetBool("crouching", false);
            animator.ResetTrigger("crouch");
            animator.CrossFade("Idle", 0.25f);
        }
    }

    #endregion

    protected override void ResetAllAnimatorActions()
    {
        if (animator != null)
        {
            animator.SetBool("crouching", false);
            animator.SetBool("back", false);
            animator.SetBool("cross", false);
            animator.ResetTrigger("crouch");
        }

        SetGazeLock(false);
        SetWatchAnim(false);
    }

    /// <summary>노장은 홈에 복귀해 있을 때만 플레이어 얼굴로 시선을 되돌린다. 순찰 중엔 중립.</summary>
    protected override void RestoreSequenceGazeTarget()
    {
        if (gazeController == null) return;

        var horizontalDistance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(startPosition.x, 0, startPosition.z));
        if (routeQueue.Count == 0 && horizontalDistance < 0.5f)
            gazeController.ForceGazeToType(GazeTargetType.Face);
    }

    protected override IEnumerator CoSequenceLoop()
    {
        yield return new WaitForSeconds(1.5f);

        while (true)
        {
            if (routeQueue.Count > 0)
            {
                isAtHomeBase = false;
                yield return StartCoroutine(CoProcessRoute(routeQueue.Dequeue()));
            }
            else if (freeRoamEnabled)
            {
                // 웨이포인트 큐가 비어 있으면 자유이동(플레이어 추종)이 돈다. 우선순위: 대사 > 추종 > 대기.
                yield return StartCoroutine(CoFreeRoamStep());
            }
            else
            {
                // 추종을 끈 상태: 그 자리에서 플레이어와 고개짓 대화만 돈다. SetFreeRoamEnabled(false)의
                // 계약이 '이동 정지'라 원본의 스폰 복귀 걷기는 수행하지 않는다.
                isAtHomeBase = true;
                yield return StartCoroutine(CoPerformDialogueReactions());
                yield return new WaitForSeconds(1.0f);
            }
        }
    }

    /// <summary>도착 이벤트가 비어 있으면 기본 작업: 웅크려 5초 관찰 후 3초 대기.</summary>
    protected override IEnumerator CoIdleAtWaypoint(WaypointAction route)
    {
        var lookTrans = route.lookTarget != null ? route.lookTarget : cachedObjectTarget;
        var target = GetGazeTargetFromTransform(lookTrans) ?? FindPlayerGazeTarget();

        SetCrouching(true);

        if (target != null && gazeTracker != null)
        {
            gazeTracker.SetTarget(target);
            gazeTracker.TriggerShake(duration: 5.0f, speed: 3.0f, intensity: 10f);
        }

        yield return new WaitForSeconds(5.0f);

        SetCrouching(false);
        if (gazeController != null) gazeController.DisableGaze();

        yield return new WaitForSeconds(3.0f);
    }

    /// <summary>홈 대화 루틴: 얼굴 응시+끄덕(3초) → 자동 시선(1초) → 얼굴 응시+절레(3초) → 자동 시선(1초).</summary>
    IEnumerator CoPerformDialogueReactions()
    {
        yield return StartCoroutine(CoHomeNodPhase());
        yield return StartCoroutine(CoHomeShakePhase());
    }

    /// <summary>고개짓 대화의 끄덕 페이즈: 얼굴 응시+끄덕(3초) → 자동 시선(1초).</summary>
    IEnumerator CoHomeNodPhase()
    {
        if (gazeController != null) gazeController.ForceGazeToType(GazeTargetType.Face);

        if (animator != null) animator.SetBool("back", true);
        if (gazeTracker != null) gazeTracker.TriggerNod(3.0f, 13f, 16f);
        yield return new WaitForSeconds(3.0f);
        if (animator != null) animator.SetBool("back", false);

        if (gazeController != null) gazeController.ResumeAutomaticGaze();
        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>고개짓 대화의 절레 페이즈: 얼굴 응시+절레(3초) → 자동 시선(1초).</summary>
    IEnumerator CoHomeShakePhase()
    {
        if (gazeController != null) gazeController.ForceGazeToType(GazeTargetType.Face);

        if (animator != null) animator.SetBool("cross", true);
        if (gazeTracker != null) gazeTracker.TriggerShake(3.0f, 12f, 14f);
        yield return new WaitForSeconds(3.0f);
        if (animator != null) animator.SetBool("cross", false);

        if (gazeController != null) gazeController.ResumeAutomaticGaze();
        yield return new WaitForSeconds(1.0f);
    }

    #region ================= 자유이동(플레이어 추종) =================

    /// <summary>
    /// 자유이동(추종)을 켜고 끈다. 끄면 진행 중인 추종 걷기가 다음 프레임 조건 검사에서 스스로
    /// 멈추고, 시퀀스 루프가 제자리 고개짓 대기로 돌아간다. 게임 진행 스크립트용 공개 API다.
    /// </summary>
    public void SetFreeRoamEnabled(bool enabled) => freeRoamEnabled = enabled;

    /// <summary>인게임 대사가 재생 중인지. 컷씬 대사는 이 경로를 타지 않는다(컷씬이 프레임을 소유).</summary>
    static bool IsDialogueSpeaking() =>
        InGameDialogue.TryGetInstance(out var dialogue) && dialogue.IsSpeaking;

    /// <summary>위치를 XZ 사각 영역 안으로 클램프한다. y는 건드리지 않는다(콜라이더 없는 평지 보행).</summary>
    Vector3 ClampToRoamArea(Vector3 worldPos)
    {
        var half = new Vector2(Mathf.Abs(roamAreaSize.x), Mathf.Abs(roamAreaSize.y)) * 0.5f;
        worldPos.x = Mathf.Clamp(worldPos.x, _roamCenterResolved.x - half.x, _roamCenterResolved.x + half.x);
        worldPos.z = Mathf.Clamp(worldPos.z, _roamCenterResolved.z - half.y, _roamCenterResolved.z + half.y);
        return worldPos;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0;
        b.y = 0;
        return Vector3.Distance(a, b);
    }

    /// <summary>
    /// 자유이동 1스텝: 대사 중이면 정지·주시, 멀어졌으면 추종 보행, 가까우면 고개짓 한 페이즈.
    /// 플레이어가 영역 밖이면 클램프된 경계 지점을 목표로 삼아 경계까지만 접근한다.
    /// 근접 대기를 페이즈 단위로 쪼갠 것은 페이즈 사이마다 거리·대사 상태를 다시 판정해
    /// 반응 지연을 한 페이즈(약 4초) 이내로 묶기 위해서다.
    /// </summary>
    IEnumerator CoFreeRoamStep()
    {
        var playerTrans = FindPlayerTransform();
        if (playerTrans == null)
        {
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        if (IsDialogueSpeaking())
        {
            yield return StartCoroutine(CoHoldStillForDialogue());
            yield break;
        }

        var followTarget = ClampToRoamArea(playerTrans.position);
        if (HorizontalDistance(transform.position, followTarget) >= followStartDistance)
        {
            isAtHomeBase = false;
            yield return StartCoroutine(CoFollowWalk());
            yield break;
        }

        // 근처 대기: 홈 대화와 같은 고개짓을 한 페이즈씩 번갈아 재생한다.
        isAtHomeBase = true;
        if (_freeRoamNodNext) yield return StartCoroutine(CoHomeNodPhase());
        else yield return StartCoroutine(CoHomeShakePhase());
        _freeRoamNodNext = !_freeRoamNodNext;
    }

    /// <summary>대사 재생 동안 제자리에서 플레이어 쪽으로 몸을 돌린 채 얼굴을 주시한다 (대사 > 추종).</summary>
    IEnumerator CoHoldStillForDialogue()
    {
        isAtHomeBase = true;
        if (animator != null) animator.SetBool("walk", false);
        if (gazeController != null) gazeController.ForceGazeToType(GazeTargetType.Face);

        while (IsDialogueSpeaking() && freeRoamEnabled && routeQueue.Count == 0)
        {
            var playerTrans = FindPlayerTransform();
            if (playerTrans != null)
            {
                var toPlayer = playerTrans.position - transform.position;
                toPlayer.y = 0;
                if (toPlayer.magnitude > 0.1f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * rotationSpeed);
            }

            yield return null;
        }

        ResumeOrDisableGaze();
    }

    /// <summary>
    /// 추종 보행: 플레이어(영역 클램프 목표)를 향해 걷다가 정지 거리 안으로 들어오면 멈춰
    /// 플레이어를 마주 본다. 대사 시작·추종 해제·웨이포인트 등록 시 즉시 걸음을 끝낸다.
    /// 목표가 매 프레임 움직이는 지속 추종이라 단발 도달형 <see cref="CoWalkToPosition"/>과 별도 루프다.
    /// </summary>
    IEnumerator CoFollowWalk()
    {
        if (animator != null)
        {
            animator.SetBool("walk", true);
            animator.SetBool("crouching", false);
            animator.ResetTrigger("crouch");
            animator.CrossFade("Idle", 0.1f); // 웅크린 채 걷기 시작하지 않도록 강제 기립.
        }

        // 걷는 동안에는 시선 연산을 멈춰 앞을 보고 걷게 한다.
        if (gazeController != null) gazeController.DisableGaze();

        while (freeRoamEnabled && routeQueue.Count == 0 && !IsDialogueSpeaking())
        {
            var playerTrans = FindPlayerTransform();
            if (playerTrans == null) break;

            var followTarget = ClampToRoamArea(playerTrans.position);
            if (HorizontalDistance(transform.position, followTarget) <= followStopDistance) break;

            var targetDir = followTarget - transform.position;
            targetDir.y = 0;
            if (targetDir.magnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * rotationSpeed);

                var nextPos = Vector3.MoveTowards(
                    transform.position,
                    new Vector3(followTarget.x, transform.position.y, followTarget.z),
                    followMoveSpeed * Time.deltaTime);
                // 목표가 이미 영역 안이라 직선 경로도 영역 안이지만, 자기 위치도 한 번 더 클램프해
                // 어떤 경우에도 영역을 벗어나지 않게 한다.
                transform.position = ClampToRoamArea(nextPos);
            }

            yield return null;
        }

        if (animator != null) animator.SetBool("walk", false);
        yield return StartCoroutine(CoAlignBodyToPlayer());
    }

    #endregion

    /// <summary>보행 이동: walk 애니메이션을 켜고 수평으로 걸어가 도착 후 대상 쪽으로 몸을 돌린다.</summary>
    protected override IEnumerator CoWalkToPosition(
        Vector3 targetPos, bool isAtWaypoint, Transform customLookTarget = null)
    {
        if (animator != null)
        {
            animator.SetBool("walk", true);
            animator.SetBool("crouching", false);
            animator.ResetTrigger("crouch");
            animator.CrossFade("Idle", 0.1f); // 웅크린 채 걷기 시작하지 않도록 강제 기립.
        }

        // 걷는 동안에는 시선 연산을 멈춰 앞을 보고 걷게 한다.
        if (gazeController != null) gazeController.DisableGaze();

        const float threshold = 0.25f;
        const float timeout = 8.0f; // 오브젝트 끼임 자동 복구용.
        var elapsed = 0f;

        while (Vector3.Distance(
                   new Vector3(transform.position.x, 0, transform.position.z),
                   new Vector3(targetPos.x, 0, targetPos.z)) > threshold)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[{gameObject.name}] 이동 타임아웃 초과. 다음 단계로 강제 전이합니다.", this);
                break;
            }

            var targetDir = targetPos - transform.position;
            targetDir.y = 0;
            if (targetDir.magnitude > 0.01f)
            {
                var targetRot = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    new Vector3(targetPos.x, transform.position.y, targetPos.z),
                    moveSpeed * Time.deltaTime);
            }

            yield return null;
        }

        if (animator != null) animator.SetBool("walk", false);

        if (isAtWaypoint)
        {
            var lookTrans = customLookTarget != null ? customLookTarget : cachedObjectTarget;
            if (lookTrans != null)
                yield return StartCoroutine(CoAlignBodyTo(lookTrans.position, snapAtEnd: false));
        }
        else
        {
            yield return StartCoroutine(CoAlignBodyToPlayer());
        }
    }
}
