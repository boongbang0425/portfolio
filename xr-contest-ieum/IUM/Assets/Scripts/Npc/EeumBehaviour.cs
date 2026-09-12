using System.Collections;
using GazeSystem;
using UnityEngine;

/// <summary>
/// 이음이 행동. paaalop 원본 GazeSystem/EeumAnimationTrigger의 우리 소유 재작성판으로, 공용 골격은
/// <see cref="NpcBehaviourBase"/>에 있고 여기에는 이음이 고유 연출만 남는다:
///
/// - 비행 추종: 플레이어의 실제 이동 방향 기준 왼쪽 스팟을 절대거리로 사수하며 떠다닌다.
///   플레이어가 다가오면 그 방향 그대로 밀려나고, 거리가 급격히 벌어지면(3.5배 이상) 시야 안에서
///   순간이동하는 이질감을 피해 고속 따라잡기 비행으로 좁힌다. 문턱 아래로 들어오면 알아서 평상시
///   추종으로 되돌아간다.
/// - 부유 연출: 정지 중 사인파 상하 흔들림을 추종 SmoothDamp "바깥"에서 최종 위치에 가산하고
///   (제어 루프 안에 넣으면 봅이 만든 변위를 이동 판정이 되먹임해 스스로 꺼진다), 가감속에 목뼈가
///   쏠리는 관성 틸트를 얹는다(LateUpdate).
/// - 대기 릴레이: 홈 상태에서 인사→끄덕→절레→백덤블링→기웃→얼굴·손 번갈아 보기를 순환 재생한다.
///
/// 원본의 액션 큐 매핑 특성도 그대로 둔다 — PlayWatchAnim과 PlayWonderAnim은 같은 태스크로
/// 들어가 기웃거리기로 재생되고, PlayFaceGaze와 PlayGreetAnim은 인사로 재생된다. 데이터가 이미
/// 그 전제로 만들어져 있을 수 있으므로 "고치지" 않는다.
/// </summary>
public sealed class EeumBehaviour : NpcBehaviourBase
{
    [Header("NPC 유형")]
    [SerializeField]
    [Tooltip("비행형 NPC(이음이)이면 켠다. 끄면 지상 NPC처럼 제자리 시선·리액션만 돈다.")]
    bool isFlyingNPC = true;

    [Header("비행 추종")]
    [SerializeField]
    [Tooltip("플레이어 이동 방향 기준 왼쪽으로 유지할 절대거리(m).")]
    float followDistance = 6.0f;

    [SerializeField]
    [Tooltip("플레이어 위치 기준 떠 있을 높이(m).")]
    float hoverHeight = 0.9f;

    [SerializeField]
    [Tooltip("이동 속도(m/s). 가까울 때의 기준 추종 속도다.")]
    float moveSpeed = 4.0f;

    [SerializeField]
    [Tooltip("방향 전환 보간 속도.")]
    float rotationSpeed = 8f;

    [SerializeField]
    [Tooltip("천천히 쫓아올 때의 감쇠 시간. 클수록 늦게 따라온다.")]
    float followSmoothTime = 0.65f;

    [Header("안착 앵커 (선택)")]
    [SerializeField]
    [Tooltip("설정하면 플레이어가 반경 안에 있을 때 추종 대신 이 자리에 고정된다.")]
    Transform homeAnchor;

    [SerializeField]
    [Tooltip("홈 앵커 고정이 활성화되는 반경(m).")]
    float anchorTriggerRange = 5.0f;

    [Header("개발 테스트")]
    [SerializeField]
    [Tooltip("켜면 제자리에서 전체 리액션을 순차 반복 재생한다.")]
    bool testAllAnimations;

    [Header("부유 연출")]
    [SerializeField]
    [Tooltip("정지 중 상하로 흔들리는 속도.")]
    float bobbingSpeed = 2.0f;

    [SerializeField]
    [Tooltip("정지 중 상하 흔들림 진폭(m).")]
    float bobbingAmount = 0.12f;

    [Header("관성 목뼈")]
    [SerializeField]
    [Tooltip("목뼈 트랜스폼. 비우면 이름(neck·collar)으로 자동 탐색한다.")]
    Transform neckBone;

    [SerializeField]
    [Tooltip("가감속에 목이 쏠리는 민감도.")]
    float neckTiltSensitivity = 2.5f;

    [SerializeField]
    [Tooltip("목이 꺾일 수 있는 한계각(도).")]
    float maxNeckAngle = 15.0f;

    [SerializeField]
    [Tooltip("목 기울임 보간 속도.")]
    float boneLerpSpeed = 5.0f;

    protected override float MoveSpeed => moveSpeed;
    protected override float RotationSpeed => rotationSpeed;

    bool _isDoingHappyBackflip;
    bool _isMoving;
    float _bobbingTime;

    // 부유 오프셋을 상태로 들고 다닌다. transform.position에서 이 값을 빼면 봅을 걷어낸 "본체
    // 기준 위치"가 나오고, 이동·홈 판정과 추종 SmoothDamp는 전부 그쪽으로만 돈다.
    Vector3 _bobOffset;

    Vector3 _currentMoveTargetPos;
    Vector3 _moveVelocity;

    // 자기 속도·가속도 추적: 목 관성 틸트의 입력이다.
    Vector3 _lastPosition;
    Vector3 _currentVelocity;
    Vector3 _lastVelocity;
    Vector3 _currentAcceleration;
    Quaternion _currentNeckOffset = Quaternion.identity;

    // GazeTracker 머리 본 보정에서 확보한 얼굴 트랜스폼. 외부(발화자 주시)가 조준점으로 쓴다.
    Transform _faceBone;

    // 플레이어의 실제 이동 방향 추적: 왼쪽 스팟 계산의 기준이다.
    Vector3 _lastPlayerMoveDirection = Vector3.forward;
    Vector3 _lastPlayerPos;
    Vector3 _playerVelocity;

    // 추종 평활: 원시 타깃을 그대로 쫓으면 방향 전환마다 스팟이 순간이동해 움직임이 홱홱 꺾인다.
    // 타깃 자체를 한 번 감쇠시킨 뒤 그쪽으로 비행해 이단 완충을 만든다.
    Vector3 _smoothedTargetPos;
    Vector3 _targetSmoothVelocity;
    Vector3 _smoothedPlayerVelocity;

    int _idleReactionIndex;

    // AI 대화 상태 피드백(EeumStateFeedback)이 밀어 넣는 절차적 모션 훅. 내부 계산에 곱·가산만
    // 하는 얕은 값이라 기본값(1·1·0)이면 종전 동작과 완전히 같다.
    float _bobSpeedMultiplier = 1f;
    float _bobAmountMultiplier = 1f;
    float _followDistanceOffset;

    // 추종 거리에 상태 피드백 가산치를 얹은 실효값. 0 근처로 붙으면 왼쪽 스팟·밀려남 계산이
    // 무의미해지므로 하한을 둔다.
    float EffectiveFollowDistance => Mathf.Max(1f, followDistance + _followDistanceOffset);

    protected override void Start()
    {
        // 이음이는 스폰 즉시 홈 취급이라 대기 릴레이가 바로 돈다. 노장(false)과 다른 초기값이다.
        isAtHomeBase = true;

        base.Start();

        _lastPosition = transform.position;
        _currentMoveTargetPos = transform.position;
        _smoothedTargetPos = transform.position;

        var playerTrans = FindPlayerTransform();
        if (playerTrans != null)
        {
            _lastPlayerPos = playerTrans.position;
            _lastPlayerMoveDirection = playerTrans.forward;
            _lastPlayerMoveDirection.y = 0;
            _lastPlayerMoveDirection.Normalize();
        }
        else
        {
            _lastPlayerPos = transform.position;
        }
    }

    /// <summary>
    /// 이음이는 FBX 인스턴스에 스크립트만 얹는 구성이라, 시선 스택이 없으면 스스로 붙여 자립한다.
    /// </summary>
    protected override bool AcquireGazeComponents()
    {
        gazeTracker = GetComponent<GazeTracker>();
        if (gazeTracker == null) gazeTracker = gameObject.AddComponent<GazeTracker>();

        gazeController = GetComponent<NPCGazeController>();
        if (gazeController == null) gazeController = gameObject.AddComponent<NPCGazeController>();

        if (GetComponent<NPCGazeDirector>() == null) gameObject.AddComponent<NPCGazeDirector>();
        if (GetComponent<SequenceGazeSelectionPolicy>() == null)
            gameObject.AddComponent<SequenceGazeSelectionPolicy>();

        FindBones();
        FixUpGazeTrackerHead();
        return true;
    }

    void Update()
    {
        var playerTrans = FindPlayerTransform();

        // 플레이어의 실제 이동 방향 캐싱 (WASD든 VR 이동이든 위치 변화로만 판정).
        if (playerTrans != null && Time.deltaTime > 0f)
        {
            _playerVelocity = (playerTrans.position - _lastPlayerPos) / Time.deltaTime;
            _lastPlayerPos = playerTrans.position;

            // 프레임 델타로 얻는 속도는 잘게 떨린다. 지수 평활로 걸러야 정지·이동 판정과
            // 스팟 방향이 문턱값 근처에서 널뛰지 않는다.
            _smoothedPlayerVelocity = Vector3.Lerp(
                _smoothedPlayerVelocity, _playerVelocity, 1f - Mathf.Exp(-8f * Time.deltaTime));

            var horizontalVelocity = _smoothedPlayerVelocity;
            horizontalVelocity.y = 0;
            if (horizontalVelocity.magnitude > 0.15f)
            {
                // 방향을 즉시 스냅하면 좌우 이동 전환마다 왼쪽 스팟이 수 미터 순간이동한다.
                // 회전 자체를 감아 스팟이 플레이어 주위를 도는 궤적으로 이동하게 한다.
                _lastPlayerMoveDirection = Vector3.Slerp(
                    _lastPlayerMoveDirection, horizontalVelocity.normalized,
                    1f - Mathf.Exp(-4f * Time.deltaTime)).normalized;
            }
        }

        // 자기 가속도 추적 (목 관성 입력). 부유 오프셋을 뺀 본체 위치로 계산한다 — 봅은 연출이지
        // 이동이 아니므로, 그대로 두면 상하 흔들림의 가속도(진폭×속도²)가 목뼈 틸트로 새어 들어간다.
        if (Time.deltaTime > 0f)
        {
            var basePos = transform.position - _bobOffset;
            _currentVelocity = (basePos - _lastPosition) / Time.deltaTime;
            _currentAcceleration = (_currentVelocity - _lastVelocity) / Time.deltaTime;
            _lastPosition = basePos;
            _lastVelocity = _currentVelocity;
        }

        TickWatchSync();

        // 백덤블링 중에는 시선을 통째로 놓는다 — 공중제비 도중 고개가 대상을 물면 목이 꺾인다.
        if (_isDoingHappyBackflip)
        {
            if (gazeTracker != null) gazeTracker.ClearTarget();
        }
        else
        {
            TickGazeOverride();
        }

        TickFlight(playerTrans);
        CommitGazeStates();
    }

    /// <summary>비행 위치 제어. 경로 이동 중에는 그쪽 코루틴이 위치를 소유한다.</summary>
    void TickFlight(Transform playerTrans)
    {
        // 봅을 걷어낸 본체 기준 위치. 거리 판정과 추종 SmoothDamp는 전부 이 값으로 돈다.
        // transform.position을 그대로 쓰면 부유 오프셋이 이동·홈 판정에 섞여 들어간다.
        var basePos = transform.position - _bobOffset;

        if (routeQueue.Count != 0)
        {
            // 경로 이동 코루틴이 transform.position을 직접 소유하는 구간이다. 평활 타깃을 현재
            // 위치에 붙여 두지 않으면 추종 재개 순간 낡은 타깃을 향해 한 번 홱 꺾인다.
            _smoothedTargetPos = basePos;
            _targetSmoothVelocity = Vector3.zero;

            // 남은 봅 오프셋을 즉시 0으로 지우면 이동 시작 프레임에 최대 bobbingAmount만큼 튄다.
            // 코루틴이 쓴 위치는 건드리지 않고 오프셋의 변화분만 얹어 서서히 흡수한다.
            var previousOffset = _bobOffset;
            transform.position += TickBobOffset(false) - previousOffset;
            return;
        }

        if (isPerformingAction)
        {
            // 대기 리액션 중에는 추종(SmoothDamp)을 멈추고 부유만 유지한다 — R의 결정이다.
            // 리액션 코루틴은 애니메이터와 시선만 건드려 위치를 소유하지 않지만, 홈에서 리액션을
            // 도는 동안 플레이어를 계속 쫓아다니지는 않게 한다. 위치를 동결한 이상 이동 중이
            // 아니므로 플래그도 내려야 부유가 리액션 내내 유지된다.
            _smoothedTargetPos = basePos;
            _targetSmoothVelocity = Vector3.zero;
            _isMoving = false;
            ApplyBobbing(basePos);
            return;
        }

        if (isFlyingNPC)
        {
            var targetFlightPos = startPosition;

            // 따라잡기 모드 여부. 아래 추종 계산과 완충 파라미터가 함께 봐야 해서 여기서 선언한다.
            var isCatchingUp = false;

            if (testAllAnimations && playerTrans != null)
            {
                isAtHomeBase = true;
                var leftDirection = Quaternion.Euler(0, -90f, 0) * _lastPlayerMoveDirection;
                targetFlightPos = playerTrans.position + leftDirection * EffectiveFollowDistance + Vector3.up * hoverHeight;
            }
            else if (homeAnchor != null && playerTrans != null &&
                     Vector3.Distance(playerTrans.position, homeAnchor.position) <= anchorTriggerRange)
            {
                isAtHomeBase = true;
                targetFlightPos = homeAnchor.position;
            }
            else if (playerTrans != null)
            {
                var leftDirection = Quaternion.Euler(0, -90f, 0) * _lastPlayerMoveDirection;
                var defaultTargetPos = playerTrans.position + leftDirection * EffectiveFollowDistance + Vector3.up * hoverHeight;

                // 절대거리 사수: 플레이어가 다가오면 지금 방향 그대로 실효 추종 거리 지점까지 밀려난다.
                var toEeum2D = basePos - playerTrans.position;
                toEeum2D.y = 0;
                var rawDist = toEeum2D.magnitude;
                if (rawDist > 0.05f)
                {
                    toEeum2D.Normalize();
                    targetFlightPos = playerTrans.position + toEeum2D * EffectiveFollowDistance + Vector3.up * hoverHeight;
                }
                else
                {
                    targetFlightPos = defaultTargetPos;
                }

                // 플레이어가 실제 이동 중이면 기본 스팟(왼쪽)으로 서서히 수렴시킨다.
                var horizontalVelocity = _smoothedPlayerVelocity;
                horizontalVelocity.y = 0;
                if (horizontalVelocity.magnitude > 0.15f)
                    targetFlightPos = Vector3.Lerp(targetFlightPos, defaultTargetPos, Time.deltaTime * 5.0f);

                // 따라잡기 판정: 거리가 급격히 벌어지면 순간이동으로 붙이지 않고 고속 비행으로 좁힌다.
                // 플레이어 시야 안에서 사라졌다 나타나는 그림이 순간이동의 이질감으로 남기 때문이다.
                // 전력 질주 정도로는 걸리지 않도록 문턱을 여유 있게 둔다.
                var distToPlayer = Vector3.Distance(basePos, playerTrans.position);
                isCatchingUp = distToPlayer > EffectiveFollowDistance * 3.5f;

                // 스팟에 안착했고 플레이어도 멈췄으면 홈(대기 릴레이 가능) 상태다. 여기에
                // 봅 오프셋이 섞이면 진폭이 0.35m 마진을 갉아먹어 홈 판정이 채터링한다.
                var distToTargetSpace = Vector3.Distance(basePos, targetFlightPos);
                isAtHomeBase = distToTargetSpace <= 0.35f && _smoothedPlayerVelocity.magnitude <= 0.15f;
            }

            // 1차 완충: 원시 타깃이 방향 전환으로 튀어도 실제 쫓는 지점은 곡선으로 흐른다.
            // 따라잡기 중에는 완충을 거의 풀어 타깃이 스팟에 바로 붙게 한다 — 안 그러면 쫓아야 할
            // 지점 자체가 뒤처져 아무리 속도를 올려도 거리가 안 좁혀진다.
            var targetSmoothTime = isCatchingUp ? 0.05f : 0.3f;
            _smoothedTargetPos = Vector3.SmoothDamp(
                _smoothedTargetPos, targetFlightPos, ref _targetSmoothVelocity, targetSmoothTime);

            var distToTarget = Vector3.Distance(basePos, _smoothedTargetPos);
            _isMoving = distToTarget > 0.05f;

            // 부유는 여기서 목표점에 얹지 않는다. 얹으면 봅이 만든 변위를 바로 위 _isMoving 판정이
            // 되먹임해 "이동 중"으로 읽고, 진폭이 판정 문턱(0.05m)을 넘는 순간 보빙이 스스로 꺼진다.
            // 그래서 bobbingAmount를 키울수록 오히려 흔들림이 사라졌다. 추종 SmoothDamp를 통과시킨
            // 뒤 최종 위치에 가산하는 것이 이 되먹임을 끊는 유일한 지점이다(아래 ApplyBobbing).
            _currentMoveTargetPos = _smoothedTargetPos;

            // 멀수록 빨리, 가까울수록 부드럽게 — 속도와 감쇠를 거리에 비례해 함께 조인다.
            // 평상시에는 감쇠 하한을 너무 조이면 원거리 복귀가 뻣뻣한 직선 돌진이 되므로 0.3초를 지킨다.
            // 따라잡기 중에만 거리 계수와 하한을 풀어 실제로 좁혀지는 속도를 낸다. 문턱 아래로
            // 들어오는 순간 계수가 원래대로 돌아가므로 복귀는 별도 처리 없이 자연스럽게 이어진다.
            var distanceGain = isCatchingUp ? 1.2f : 0.3f;
            var minSmoothTime = isCatchingUp ? 0.15f : 0.3f;
            var activeSpeed = moveSpeed * (1.0f + distToTarget * distanceGain);
            var activeSmoothTime = Mathf.Clamp(
                followSmoothTime / (1.0f + distToTarget * distanceGain), minSmoothTime, followSmoothTime);

            basePos = Vector3.SmoothDamp(
                basePos, _currentMoveTargetPos, ref _moveVelocity, activeSmoothTime, activeSpeed);
        }
        else
        {
            _isMoving = false;
            isAtHomeBase = true;
        }

        ApplyBobbing(basePos);

        // 정지 상태에서도 몸은 항상 플레이어 쪽을 지긋이 향한다.
        if (!_isDoingHappyBackflip && playerTrans != null)
        {
            var toPlayer = playerTrans.position - transform.position;
            toPlayer.y = 0;
            if (toPlayer.magnitude > 0.05f)
            {
                var targetRot = Quaternion.LookRotation(toPlayer);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
        }
    }

    /// <summary>
    /// 부유 오프셋을 갱신해 본체 기준 위치 위에 얹는다. 추종 SmoothDamp "이후"에 부르는 것이
    /// 핵심이다 — 목표점에 얹으면 봅이 만든 변위를 이동 판정이 되먹임해 보빙이 스스로 꺼진다.
    /// </summary>
    void ApplyBobbing(Vector3 basePos)
    {
        // 지상 NPC(isFlyingNPC=false)는 떠 있지 않으므로 오프셋을 0으로 흘려보낸다.
        var wantBob = isFlyingNPC && !_isMoving && !_isDoingHappyBackflip;
        transform.position = basePos + TickBobOffset(wantBob);
    }

    /// <summary>
    /// 부유 오프셋만 갱신해 돌려준다. 게이트가 꺼질 때 오프셋을 즉시 0으로 지우면 최대
    /// bobbingAmount만큼 위치가 튀므로, 오프셋 자체를 지수 감쇠시켜 흡수한다. _bobbingTime은
    /// 리셋하지 않는다 — 리셋하면 사인이 매번 위상 0(상승 반주기)에서 다시 시작해 아래 반주기에
    /// 영영 도달하지 못하고, 상하 흔들림이 아니라 위로 뜬 채 떠는 모양이 된다.
    /// </summary>
    Vector3 TickBobOffset(bool wantBob)
    {
        if (wantBob) _bobbingTime += Time.deltaTime * bobbingSpeed * _bobSpeedMultiplier;

        var targetOffsetY = wantBob ? Mathf.Sin(_bobbingTime) * bobbingAmount * _bobAmountMultiplier : 0f;
        _bobOffset.y = Mathf.Lerp(_bobOffset.y, targetOffsetY, 1f - Mathf.Exp(-8f * Time.deltaTime));
        return _bobOffset;
    }

    void LateUpdate()
    {
        // 목 관성: 애니메이션이 쓴 목 회전 위에 가감속 쏠림을 곱해 얹는다.
        if (!isFlyingNPC || neckBone == null || Time.deltaTime <= 0f) return;

        var localAccel = transform.InverseTransformDirection(_currentAcceleration);
        var targetNeckX = Mathf.Clamp(localAccel.z * neckTiltSensitivity, -maxNeckAngle, maxNeckAngle);

        var targetNeckRot = Quaternion.Euler(targetNeckX, 0f, 0f);
        _currentNeckOffset = Quaternion.Slerp(_currentNeckOffset, targetNeckRot, Time.deltaTime * boneLerpSpeed);

        neckBone.localRotation *= _currentNeckOffset;
    }

    /// <summary>
    /// 얼굴(머리 본) 트랜스폼. 이음이 FBX는 Generic 리그라 휴머노이드 머리 본 조회가 불가능해,
    /// GazeTracker 보정(<see cref="FixUpGazeTrackerHead"/>)이 찾아 둔 머리 트랜스폼을 재사용한다.
    /// 확보 전이거나 실패하면 루트를 돌려준다 — 부유체라 루트가 이미 눈높이 근처다.
    /// </summary>
    public Transform FaceTransform => _faceBone != null ? _faceBone : transform;

    #region ================= 이음이 고유 행동 등록 API =================

    /// <summary>
    /// 켜져 있는 동안 홈 대기 릴레이(인사→끄덕→절레→백덤블링 순환)를 멈춘다. AI 대화 상태
    /// 피드백(EeumStateFeedback)이 Idle·Unavailable 밖 상태에서 켠다 — 듣는 중·생각 중에
    /// 맥락 없는 절레·백덤블링이 상태 발광과 모순된 그림을 만들지 않게 한다. 외부 Play* API로
    /// 등록된 리액션 태스크는 억제 대상이 아니다.
    /// </summary>
    public bool SuppressIdleRelay { get; set; }

    /// <summary>
    /// AI 대화 상태에 따른 절차적 모션 훅. 부유 속도·진폭 배율과 추종 거리 가산치(m)를 받아
    /// 내부 계산에 곱·가산으로만 얹는다. (1, 1, 0)이 복원값이다. 급변 완충은 호출자 몫이다 —
    /// 여기서는 받은 값을 그대로 쓴다.
    /// </summary>
    public void SetMotionFeedback(float bobSpeedMultiplier, float bobAmountMultiplier, float followDistanceOffset)
    {
        _bobSpeedMultiplier = bobSpeedMultiplier;
        _bobAmountMultiplier = bobAmountMultiplier;
        _followDistanceOffset = followDistanceOffset;
    }

    /// <summary>기쁨 백덤블링. 재생 중에는 시선을 완전히 놓고, 착지 후 2초 더 시선을 쉰다.</summary>
    public void PlayHappyReaction(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.Crouch, duration = duration });

    /// <summary>기웃거리기. PlayWatchAnim과 같은 태스크로 들어간다 (원본 매핑 유지).</summary>
    public void PlayWonderAnim(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.WatchAnim, duration = duration });

    /// <summary>인사. PlayFaceGaze와 같은 태스크로 들어간다 (원본 매핑 유지).</summary>
    public void PlayGreetAnim(float duration) =>
        actionQueue.Enqueue(new ActionTask { type = ActionType.FaceGaze, duration = duration });

    #endregion

    protected override IEnumerator RunAction(ActionTask task)
    {
        // 원본 이음이의 큐 매핑 그대로: Crouch=백덤블링, WatchAnim=기웃, FaceGaze=인사.
        switch (task.type)
        {
            case ActionType.Crouch:
                yield return StartCoroutine(CoPlayHappyReactionInternal(task.duration));
                break;
            case ActionType.Nod:
                yield return StartCoroutine(CoPlayNodReactionInternal(task.duration));
                break;
            case ActionType.Shake:
                yield return StartCoroutine(CoPlayShakeReactionInternal(task.duration));
                break;
            case ActionType.WatchAnim:
                yield return StartCoroutine(CoPlayWonderAnimInternal(task.duration));
                break;
            case ActionType.FaceGaze:
                yield return StartCoroutine(CoPlayGreetAnimInternal(task.duration));
                break;
            case ActionType.AutomaticGaze:
                yield return StartCoroutine(CoPlayAutomaticGazeInternal(task.duration));
                break;
        }
    }

    #region ================= 이음이 고유 연출 코루틴 =================

    IEnumerator CoPlayNodReactionInternal(float duration)
    {
        isPerformingAction = true;
        if (gazeTracker != null) gazeTracker.TriggerNod(duration, 13f, 16f);
        yield return new WaitForSeconds(duration);

        ResumeOrDisableGaze();
        yield return new WaitForSeconds(2.0f);
        isPerformingAction = false;
    }

    IEnumerator CoPlayShakeReactionInternal(float duration)
    {
        isPerformingAction = true;
        if (gazeTracker != null) gazeTracker.TriggerShake(duration, 10f, 12f);
        yield return new WaitForSeconds(duration);

        ResumeOrDisableGaze();
        yield return new WaitForSeconds(2.0f);
        isPerformingAction = false;
    }

    IEnumerator CoPlayFaceGazeInternal(float duration)
    {
        isPerformingAction = true;
        yield return StartCoroutine(CoFaceGazeCore(duration));
        isPerformingAction = false;
    }

    IEnumerator CoPlayAutomaticGazeInternal(float duration)
    {
        isPerformingAction = true;
        yield return StartCoroutine(CoAutomaticGazeCore(duration));
        isPerformingAction = false;
    }

    IEnumerator CoPlayHappyReactionInternal(float duration)
    {
        _isDoingHappyBackflip = true;
        isPerformingAction = true;

        if (gazeTracker != null) gazeTracker.ClearTarget();
        if (gazeController != null) gazeController.DisableGaze();

        if (animator != null && HasParameter(animator, "happy")) animator.SetTrigger("happy");

        yield return new WaitForSeconds(duration);

        // 착지 후 2초 더 시선 잠금을 유지해 공중제비 여운 동안 고개가 홱 돌지 않게 한다.
        yield return new WaitForSeconds(2.0f);

        _isDoingHappyBackflip = false;
        isPerformingAction = false;

        if (gazeController != null && isAtHomeBase) gazeController.ResumeAutomaticGaze();
    }

    IEnumerator CoPlayWonderAnimInternal(float duration)
    {
        isPerformingAction = true;
        if (animator != null && HasParameter(animator, "wonder")) animator.SetBool("wonder", true);
        yield return new WaitForSeconds(duration);
        if (animator != null && HasParameter(animator, "wonder")) animator.SetBool("wonder", false);
        yield return new WaitForSeconds(2.0f);
        isPerformingAction = false;
    }

    IEnumerator CoPlayGreetAnimInternal(float duration)
    {
        isPerformingAction = true;
        if (animator != null && HasParameter(animator, "greet"))
        {
            // greet가 Bool인 컨트롤러에서는 끝나고 확실히 꺾어야 인사가 무한 반복되지 않는다.
            if (GetParameterType(animator, "greet") == AnimatorControllerParameterType.Bool)
                animator.SetBool("greet", true);
            else
                animator.SetTrigger("greet");
        }

        yield return new WaitForSeconds(duration);

        if (animator != null && HasParameter(animator, "greet") &&
            GetParameterType(animator, "greet") == AnimatorControllerParameterType.Bool)
            animator.SetBool("greet", false);

        yield return new WaitForSeconds(2.0f);
        isPerformingAction = false;
    }

    #endregion

    protected override void ResetAllAnimatorActions()
    {
        SetGazeLock(false);
        SetWatchAnim(false);
        if (animator == null) return;

        if (HasParameter(animator, "wonder")) animator.SetBool("wonder", false);
        if (HasParameter(animator, "greet") &&
            GetParameterType(animator, "greet") == AnimatorControllerParameterType.Bool)
            animator.SetBool("greet", false);
    }

    /// <summary>이음이는 언제나 플레이어 얼굴로 시선을 되돌린다.</summary>
    protected override void RestoreSequenceGazeTarget()
    {
        if (gazeController != null) gazeController.ForceGazeToType(GazeTargetType.Face);
    }

    protected override IEnumerator CoSequenceLoop()
    {
        yield return new WaitForSeconds(1.5f);

        while (true)
        {
            if (testAllAnimations && FindPlayerTransform() != null)
            {
                yield return StartCoroutine(CoRunTestRelay());
                continue;
            }

            if (routeQueue.Count > 0)
            {
                isAtHomeBase = false;
                yield return StartCoroutine(CoProcessRoute(routeQueue.Dequeue()));
            }
            else if (actionQueue.Count > 0)
            {
                // 외부(튜토리얼 연출 등)가 Play* 공개 API로 등록한 태스크를 즉시 소비한다. 이음이는
                // 경로 도착 이벤트(onArrived) 없이도 태스크가 들어오므로, 여기서 소비하지 않으면
                // 큐가 영영 비워지지 않는다. 홈 릴레이보다 먼저 처리해 대사에 맞춘 리액션이 밀리지
                // 않게 한다.
                isPerformingAction = true;
                yield return StartCoroutine(CoExecuteActionQueue());
            }
            else if (isAtHomeBase)
            {
                if (IsDialogueSpeaking() || SuppressIdleRelay)
                {
                    // 대사 재생 중에는 대기 릴레이를 억제한다 — 릴레이가 isPerformingAction을
                    // 점유하면 대사에 맞춰 들어오는 외부 리액션이 릴레이 꼬리(리액션+2초 여운)만큼
                    // 밀리고, 말하는 동안 맥락 없는 절레·백덤블링이 재생된다. AI 대화 진행 중
                    // (SuppressIdleRelay)에도 같은 이유로 멈춘다.
                    yield return null;
                }
                else
                {
                    // 홈에서는 대기 릴레이를 순환 재생한다. 자동 시선도 함께 살린다.
                    if (gazeController != null) gazeController.ResumeAutomaticGaze();
                    yield return StartCoroutine(CoPlayIdleReaction(_idleReactionIndex));
                    _idleReactionIndex = (_idleReactionIndex + 1) % 6;
                    yield return new WaitForSeconds(0.5f);
                }
            }
            else
            {
                _idleReactionIndex = 0;
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    /// <summary>인게임 대사가 재생 중인지. 노장(<see cref="NojangBehaviour"/>)과 같은 판정이다.</summary>
    static bool IsDialogueSpeaking() =>
        InGameDialogue.TryGetInstance(out var dialogue) && dialogue.IsSpeaking;

    IEnumerator CoPlayIdleReaction(int index)
    {
        switch (index)
        {
            case 0: yield return StartCoroutine(CoPlayGreetAnimInternal(2.0f)); break;
            case 1: yield return StartCoroutine(CoPlayNodReactionInternal(2.0f)); break;
            case 2: yield return StartCoroutine(CoPlayShakeReactionInternal(2.0f)); break;
            case 3: yield return StartCoroutine(CoPlayHappyReactionInternal(1.5f)); break;
            case 4: yield return StartCoroutine(CoPlayWonderAnimInternal(2.5f)); break;
            case 5: yield return StartCoroutine(CoPlayAutomaticGazeInternal(3.0f)); break;
        }
    }

    /// <summary>개발 테스트 모드: 제자리에서 전체 리액션을 순서대로 한 바퀴 돈다.</summary>
    IEnumerator CoRunTestRelay()
    {
        isAtHomeBase = true;
        yield return StartCoroutine(CoPlayGreetAnimInternal(2.0f));
        yield return StartCoroutine(CoPlayNodReactionInternal(2.0f));
        yield return StartCoroutine(CoPlayShakeReactionInternal(2.0f));
        yield return StartCoroutine(CoPlayHappyReactionInternal(1.5f));
        yield return StartCoroutine(CoPlayWonderAnimInternal(2.5f));
        yield return StartCoroutine(CoPlayFaceGazeInternal(2.0f));
        yield return StartCoroutine(CoPlayAutomaticGazeInternal(3.0f));
        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>비행 이동: 지면 목표 위 hoverHeight 지점까지 날아간 뒤 대상 쪽으로 몸을 돌린다.</summary>
    protected override IEnumerator CoWalkToPosition(
        Vector3 targetPos, bool isAtWaypoint, Transform customLookTarget = null)
    {
        _isMoving = true;

        if (animator != null && HasParameter(animator, "walk")) animator.SetBool("walk", false);
        if (gazeController != null) gazeController.DisableGaze();

        const float threshold = 0.25f;
        const float timeout = 4.0f;
        var elapsed = 0f;

        var flightTargetPos = new Vector3(targetPos.x, targetPos.y + hoverHeight, targetPos.z);

        while (Vector3.Distance(transform.position, flightTargetPos) > threshold)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout) break;

            var targetDir = flightTargetPos - transform.position;
            targetDir.y = 0;
            if (targetDir.magnitude > 0.01f)
            {
                var targetRot = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
                transform.position = Vector3.MoveTowards(
                    transform.position, flightTargetPos, moveSpeed * Time.deltaTime);
            }

            yield return null;
        }

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

        _isMoving = false;
    }

    void FindBones()
    {
        foreach (var child in GetComponentsInChildren<Transform>())
        {
            var nameLower = child.name.ToLower();
            if (neckBone == null && (nameLower.Contains("neck") || nameLower.Contains("collar")))
                neckBone = child;
        }
    }

    /// <summary>
    /// GazeTracker의 머리 본이 비어 있으면 이름으로 찾아 채운다. 해당 필드가 라이브러리
    /// 내부(private)라 원본과 같은 리플렉션 경로를 쓴다. 목뼈가 머리 본과 같은 것으로 잡혔으면
    /// 부모(neck·back·spine) 쪽으로 올려 관성 틸트가 머리 조준과 싸우지 않게 한다.
    /// </summary>
    void FixUpGazeTrackerHead()
    {
        if (gazeTracker == null) return;

        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        var headTransField = typeof(GazeTracker).GetField("headTransform", flags);
        if (headTransField != null)
        {
            var headBone = headTransField.GetValue(gazeTracker) as Transform;
            if (headBone == null)
            {
                var foundHead = FindHeadBoneFallback(transform);
                headBone = foundHead != null ? foundHead : transform;
                headTransField.SetValue(gazeTracker, headBone);
            }

            // 외부 조준용 얼굴 트랜스폼으로도 공유한다 (FaceTransform).
            _faceBone = headBone;

            if (neckBone == headBone && neckBone != null && neckBone.parent != null)
            {
                var parentName = neckBone.parent.name.ToLower();
                if (parentName.Contains("neck") || parentName.Contains("back") || parentName.Contains("spine"))
                    neckBone = neckBone.parent;
            }
        }

        var offsetField = typeof(GazeTracker).GetField("headRotationOffset", flags);
        if (offsetField != null)
        {
            var currentOffset = (Vector3)offsetField.GetValue(gazeTracker);
            currentOffset.y = 0f;
            offsetField.SetValue(gazeTracker, currentOffset);
        }
    }

    static Transform FindHeadBoneFallback(Transform current)
    {
        if (current.name.ToLower().Contains("head")) return current;

        for (var i = 0; i < current.childCount; i++)
        {
            var found = FindHeadBoneFallback(current.GetChild(i));
            if (found != null) return found;
        }

        return null;
    }
}
