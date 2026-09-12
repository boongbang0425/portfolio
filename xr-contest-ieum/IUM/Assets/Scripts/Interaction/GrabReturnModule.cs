using UnityEngine;

/// <summary>
/// Keeps F-005 1.8 true: a dropped or thrown object must never end up somewhere the player
/// cannot reach. Checked once a second, which is frequent enough for a fallen tool.
/// </summary>
public sealed class GrabReturnModule : Module
{
    /// <summary>
    /// 조립 부재 전용 회수 기준: 홈 포즈보다 이만큼(× 월드 배율) 아래로 내려가 있으면 떨어진 것으로 본다.
    /// 도구의 <see cref="FallDepth"/>보다 훨씬 민감하다 — 부재는 랙에서 꺼내 자리에 끼우는 것이 전부라
    /// "바닥에 내려놓기"가 의도된 조작인 경우가 없다.
    /// </summary>
    const float AssemblyDropDepth = 0.25f;

    /// <summary>조립 부재 전용 회수 기준: 홈에서 이만큼(× 월드 배율) 이상 떨어져 굴러간 경우.</summary>
    const float AssemblyStrayDistance = 5f;

    /// <summary>
    /// 조립 부재를 곧바로 회수하지 않고 기다리는 시간(초). 자리를 맞추려고 잠깐 내려놓은 것과
    /// 정말로 놓쳐 굴러간 것을 가른다. <see cref="OnSecond"/>가 초당 한 번이라 곧 틱 수다.
    /// 처음 3초는 체감이 너무 늦다는 실기 피드백으로 1초로 줄였다.
    /// </summary>
    const int AssemblyDropGraceSeconds = 1;

    readonly Grabbable _grabbable;

    /// <summary>
    /// 이 물체가 조립 부재이면 그 부품. 조립 부재는 회수 기준이 도구와 다르다. 안착에 성공하면
    /// <see cref="AssemblySnapModule"/>이 이 모듈을 통째로 떼므로 여기 남아 있는 것은 항상
    /// 아직 조립되지 않은 부재다(방어적으로 상태도 함께 본다).
    /// </summary>
    readonly AssemblyPart _assemblyPart;

    int _droppedSeconds;

    public GrabReturnModule(Grabbable grabbable) : base(grabbable)
    {
        _grabbable = grabbable;
        _assemblyPart = grabbable != null
            ? grabbable.GetComponent<AssemblyPart>() ?? grabbable.GetComponentInChildren<AssemblyPart>(true)
            : null;
    }

    /// <summary>Drop below this height, relative to the home pose, and the object is recovered.</summary>
    public float FallDepth { get; set; } = 3f;

    public float MaxDistance { get; set; } = 25f;

    public override void OnSecond()
    {
        if (_grabbable == null) return;

        if (_grabbable.IsHeld)
        {
            _droppedSeconds = 0;
            return;
        }

        var position = _grabbable.transform.position;
        var home = _grabbable.HomePose.position;

        // 기준 거리는 오브젝트의 월드 스케일에 비례시킨다. ×20 작업장(Play)에서는 1유닛이
        // 인체감 약 7cm라, 고정 25유닛이면 도구를 1.75m 밖에 내려놓기만 해도 회수돼 버린다.
        // 원본 스케일 씬(GongpoScene)은 lossyScale이 1이라 종전과 동일하게 동작한다.
        var lossy = _grabbable.transform.lossyScale;
        var scale = Mathf.Max(1f, Mathf.Max(lossy.x, Mathf.Max(lossy.y, lossy.z)));

        if (position.y < home.y - FallDepth * scale || Vector3.Distance(position, home) > MaxDistance * scale)
        {
            _droppedSeconds = 0;
            _grabbable.ReturnHome();
            return;
        }

        // 조립 부재의 민감 회수. 위의 도구 기준(FallDepth 3·MaxDistance 25)이 월드 배율까지
        // 곱해지면서 공포 부재가 바닥에 떨어져도 문턱에 닿지 않아 영영 회수되지 않았다(실기 증상).
        // 부재만 훨씬 얕은 문턱을 쓰고, 몇 초 유지된 경우에만 되돌린다.
        if (!IsStrayAssemblyPart(position, home, scale))
        {
            _droppedSeconds = 0;
            return;
        }

        if (++_droppedSeconds < AssemblyDropGraceSeconds) return;

        _droppedSeconds = 0;
        _grabbable.ReturnHome();
    }

    bool IsStrayAssemblyPart(Vector3 position, Vector3 home, float scale)
    {
        if (_assemblyPart == null || _assemblyPart.isAssembled) return false;

        return position.y < home.y - AssemblyDropDepth * scale ||
               Vector3.Distance(position, home) > AssemblyStrayDistance * scale;
    }
}
