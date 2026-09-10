using UnityEngine;

/// <summary>
/// 단계 하나의 판정. <see cref="ProcessRunner"/>가 현재 단계에 대해서만 <see cref="Tick"/>을
/// 부르고, 그것도 <see cref="ProcessGate"/>가 열려 있을 때만 부른다 — 노장의 설명이 끝나기 전에는
/// 판정하지 않는다는 규칙(F-014 5.3)이 게이트 하나로 성립한다.
///
/// 조건을 이벤트가 아니라 폴링으로 본다. 어차피 매 프레임 도는 상태 기계라 얻는 것이 없고,
/// 구독·해제 수명을 관리하지 않아도 되며, "1초 이상 유지" 같은 조건은 폴링이 더 자연스럽다.
/// </summary>
public sealed class ProcessStep
{
    readonly Player _player;

    ProcessTarget _target;
    float _accumulated;
    int _count;
    float _heldSeconds;

    /// <summary>직전 프레임의 시선 방향. VR에서 <see cref="StepCondition.Look"/>을 재는 기준이다.</summary>
    Vector3 _lastHeadForward;
    bool _hasLastHeadForward;

    public ProcessStep(ProcessStepData data, Player player)
    {
        Data = data;
        _player = player;
    }

    public ProcessStepData Data { get; }

    public bool IsSatisfied { get; private set; }

    /// <summary>직전 <see cref="Tick"/>에서 조건 쪽으로 나아갔는지. 재안내 타이머를 되돌리는 데 쓴다.</summary>
    public bool MadeProgress { get; private set; }

    /// <summary>진행률 0~1. 오버레이 표시용이며 판정에는 쓰지 않는다.</summary>
    public float Progress { get; private set; }

    /// <summary>단계를 시작한다. 대상을 찾지 못하면 경고만 남기고 통과 불가 상태로 둔다.</summary>
    public void Arm()
    {
        IsSatisfied = false;
        MadeProgress = false;
        Progress = 0f;
        _accumulated = 0f;
        _count = 0;
        _heldSeconds = 0f;
        _target = null;
        _hasLastHeadForward = false;

        if (!NeedsTarget(Data.Condition)) return;

        if (ProcessTarget.TryGet(Data.Target, out var target))
        {
            _target = target;
            return;
        }

        Debug.LogWarning(
            $"[Process] 단계 '{Data.Id}'의 대상 '{Data.Target}'을 씬에서 찾지 못했습니다. " +
            "ProcessTarget의 키를 확인하십시오.");
    }

    public void Tick(float delta)
    {
        if (IsSatisfied) return;

        var before = Progress;
        Evaluate(delta);
        MadeProgress = Progress > before + 0.0001f;

        if (Progress >= 1f) IsSatisfied = true;
    }

    /// <summary>디버그 강제 통과. 마이크가 없는 환경에서 PTT 단계를 넘기는 경로이기도 하다.</summary>
    public void ForceSatisfy()
    {
        Progress = 1f;
        IsSatisfied = true;
    }

    static bool NeedsTarget(StepCondition condition) =>
        condition is StepCondition.Point or StepCondition.Grab or StepCondition.Place;

    void Evaluate(float delta)
    {
        var commands = _player != null && _player.Input != null
            ? _player.Input.Commands
            : default;

        switch (Data.Condition)
        {
            case StepCondition.None:
                Progress = 1f;
                break;

            case StepCondition.Look:
                _accumulated += LookDegreesThisFrame(commands);
                Progress = Ratio(_accumulated, Data.Amount);
                break;

            case StepCondition.Move:
                _accumulated += commands.Move.magnitude * delta;
                Progress = Ratio(_accumulated, Data.Amount);
                break;

            case StepCondition.SnapTurn:
                if (!Mathf.Approximately(commands.SnapTurn, 0f)) _count++;
                Progress = Ratio(_count, Data.Amount);
                break;

            case StepCondition.Point:
                Progress = Hold(IsPointingAtTarget(), delta);
                break;

            case StepCondition.Grab:
                Progress = Hold(_target != null && _target.TargetGrabbable != null &&
                                _target.TargetGrabbable.IsHeld, delta);
                break;

            case StepCondition.Place:
                // 안착해 있는 동안만 참이다. 다시 집어 들면 게이지가 아니라 조건 자체가 풀린다.
                Progress = _target != null && _target.TargetSocket != null &&
                           _target.TargetSocket.Placement is { IsOccupied: true }
                    ? 1f
                    : 0f;
                break;

            case StepCondition.PushToTalk:
                Progress = Hold(commands.PushToTalk, delta);
                break;

            case StepCondition.Signal:
                Progress = Ratio(ProcessSignalBus.Read(Data.Target), Data.Amount);
                break;
        }
    }

    /// <summary>
    /// 이번 프레임에 둘러본 각도(도). 장치에 따라 재는 곳이 다르다.
    ///
    /// 데스크톱은 <see cref="PlayerCommands.Look"/>이 곧 시점 회전 요청량이지만, VR에서는 그 값이
    /// 항상 0이다 — 머리는 HMD가 직접 몰고 입력 커맨드를 거치지 않기 때문이다(PlayerCommands.Look
    /// 주석). 그래서 HMD 경로에서는 실제 머리(<see cref="Player.Head"/>)가 돈 각도를 잰다. 두 경로
    /// 모두 단위가 도이므로 <c>amount</c>는 장치와 무관하게 같은 값을 쓴다.
    /// </summary>
    float LookDegreesThisFrame(PlayerCommands commands)
    {
        if (commands.IsDesktop) return commands.Look.magnitude;

        var head = _player != null ? _player.Head : null;
        if (head == null) return 0f;

        var forward = head.forward;
        if (!_hasLastHeadForward)
        {
            _lastHeadForward = forward;
            _hasLastHeadForward = true;
            return 0f;
        }

        var degrees = Vector3.Angle(_lastHeadForward, forward);
        _lastHeadForward = forward;
        return degrees;
    }

    /// <summary>
    /// 어느 손으로 가리켜도 통과시킨다. 데스크톱은 화면 중앙 레이저 하나뿐이지만 VR은 양손이고,
    /// 튜토리얼에서 손을 지정할 이유가 없다.
    /// </summary>
    bool IsPointingAtTarget()
    {
        if (_player == null || _target == null || _target.TargetGrabbable == null) return false;

        return IsHovering(XRHandSide.Left) || IsHovering(XRHandSide.Right);
    }

    bool IsHovering(XRHandSide side)
    {
        var hand = _player.GetHand(side);
        return hand != null && hand.Hovered == _target.TargetGrabbable;
    }

    /// <summary>유지 시간 기반 조건. 조건이 끊기면 누적을 되돌린다.</summary>
    float Hold(bool active, float delta)
    {
        if (!active)
        {
            _heldSeconds = 0f;
            return 0f;
        }

        _heldSeconds += delta;

        // 유지 시간이 0이면 닿는 즉시 통과한다.
        return Data.HoldSeconds <= 0f ? 1f : Ratio(_heldSeconds, Data.HoldSeconds);
    }

    static float Ratio(float value, float target) =>
        target <= 0f ? 1f : Mathf.Clamp01(value / target);
}
