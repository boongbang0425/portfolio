using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// 컷씬 카메라에 HMD 추적을 되살린다 (C-1).
///
/// <see cref="CutsceneDirector.TakeOverCamera"/>가 주 씬의 Head 카메라를 끄고 컷씬 씬의 카메라를
/// 켜는데, 그 카메라에는 추적이 붙어 있지 않다. HMD를 쓴 채로 고개를 돌려도 화면이 따라오지 않는
/// 상태는 VR에서 가장 강한 멀미 유발 조건이므로 장치가 살아 있을 때만 이 컴포넌트를 얹는다.
/// 데스크톱에서는 아예 붙지 않아 기존 연출이 한 프레임도 달라지지 않는다.
///
/// <b>연출을 건드리지 않는 방법</b>: <c>EndingStage.DriveOrbit</c>과
/// <c>SungnyemunBuildStage.DriveCamera</c>는 매 <c>Update</c>에 카메라 트랜스폼을
/// <b>월드 절대 포즈</b>로 덮어쓴다. 그래서 카메라를 자식으로 넣는 피벗 방식은 통하지 않는다 —
/// 부모가 무엇이든 <c>SetPositionAndRotation</c>은 월드 포즈를 그대로 박는다. 대신
/// <see cref="LateUpdate"/>(모든 Update 뒤)에서 그 시점의 포즈를 <b>연출 포즈</b>로 캐시하고,
/// 그 위에 HMD 포즈를 합성해 다시 쓴다. 연출 코드는 손대지 않는다.
///
/// 캐시 갱신 판정은 "지금 트랜스폼이 내가 마지막으로 쓴 포즈와 다른가"이다. 연출이 이번 프레임에
/// 카메라를 옮겼으면 다르고, 옮기지 않았으면 같다. 무조건 캐시하면 연출이 멈춘 구간에서 합성 결과가
/// 다시 기준이 돼 오프셋이 매 프레임 누적된다.
///
/// <b>알려진 한계</b>: 연출 포즈에 내려보기 각도가 있으면 그만큼 지평선이 기울어진 채로 합성된다.
/// 두 스테이지 모두 <c>LookRotation(dir, Vector3.up)</c>이라 롤은 0이고 피치도 완만해 실사용에서는
/// 작지만, 급한 부감 연출을 새로 넣으면 재검토 대상이다 (ISSUE-007과 같은 실기기 검증 항목).
/// </summary>
[DisallowMultipleComponent]
public sealed class CutsceneHeadTracking : MonoBehaviour
{
    /// <summary>연출이 카메라를 옮겼다고 볼 최소 이동량(미터). 부동소수 잡음보다 두 자릿수 크다.</summary>
    const float MoveToleranceMetres = 0.001f;

    /// <summary>연출이 카메라를 돌렸다고 볼 최소 회전량(도).</summary>
    const float TurnToleranceDegrees = 0.01f;

    float _scale = 1f;

    bool _hasBase;
    Vector3 _basePosition;
    Quaternion _baseRotation;

    Vector3 _appliedPosition;
    Quaternion _appliedRotation;

    bool _hasReference;
    Vector3 _referencePosition;
    Quaternion _referenceYawInverse = Quaternion.identity;

    /// <summary>
    /// HMD 세션일 때만 붙인다. 장치가 없으면 null을 돌려주므로 호출자는 결과를 그대로 보관하면 된다.
    /// </summary>
    public static CutsceneHeadTracking AttachIfDeviceActive(Camera camera)
    {
        if (camera == null || !XRSettings.isDeviceActive) return null;

        var existing = camera.GetComponent<CutsceneHeadTracking>();
        if (existing != null) return existing;

        return camera.gameObject.AddComponent<CutsceneHeadTracking>();
    }

    void OnEnable()
    {
        // 미터 저작인 HMD 위치를 이 씬의 단위로 옮기는 배율. 컷씬 씬 자체에는 플레이어가 없고
        // 주 씬은 로드된 채로 남아 있으므로 주 씬의 Player가 잡힌다.
        _scale = SceneWorldScale.Current;

        // 렌더 직전에 한 번 더 합성해 지연을 줄인다. ViewModule이 HMD 머리에 쓰는 것과 같은 훅이다.
        Application.onBeforeRender += ApplyTracking;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= ApplyTracking;

        // 연출 포즈로 되돌려 놓는다. 카메라는 보통 씬과 함께 파괴되지만, 컷씬 카메라를 재사용하는
        // 경로가 생기면 합성된 포즈가 남아 다음 재생의 첫 프레임이 어긋난다.
        if (_hasBase) transform.SetPositionAndRotation(_basePosition, _baseRotation);

        _hasBase = false;
        _hasReference = false;
    }

    void LateUpdate()
    {
        CaptureDirectedPose();
        ApplyTracking();
    }

    void CaptureDirectedPose()
    {
        var position = transform.position;
        var rotation = transform.rotation;

        if (_hasBase
            && (position - _appliedPosition).sqrMagnitude <= MoveTolerance() * MoveTolerance()
            && Quaternion.Angle(rotation, _appliedRotation) <= TurnToleranceDegrees)
            return;

        _basePosition = position;
        _baseRotation = rotation;
        _hasBase = true;
    }

    float MoveTolerance() => MoveToleranceMetres * _scale;

    void ApplyTracking()
    {
        if (!_hasBase || !TryReadHeadPose(out var headPosition, out var headRotation)) return;

        if (!_hasReference)
        {
            // 컷씬이 시작될 때 쓴 사람이 어느 쪽을 보고 있었든 연출이 잡은 구도가 정면이어야 한다.
            // 요(yaw)만 되돌리는 이유는 피치·롤까지 상쇄하면 고개를 숙인 채 시작했을 때 지평선이
            // 기울어진 상태로 고정되기 때문이다.
            _referencePosition = headPosition;
            _referenceYawInverse = Quaternion.Euler(0f, -headRotation.eulerAngles.y, 0f);
            _hasReference = true;
        }

        var localRotation = _referenceYawInverse * headRotation;

        // 장치 포즈는 항상 미터다. 위치 오프셋만 배율을 곱하고 회전은 단위가 없어 그대로 쓴다.
        var localOffset = _referenceYawInverse * (headPosition - _referencePosition) * _scale;

        var rotation = _baseRotation * localRotation;
        var position = _basePosition + _baseRotation * localOffset;

        transform.SetPositionAndRotation(position, rotation);

        _appliedPosition = position;
        _appliedRotation = rotation;
    }

    static bool TryReadHeadPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        if (!device.isValid) return false;

        var gotPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
        var gotRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);

        // 회전만 있어도 멀미 방지의 대부분을 가져간다. 위치는 없으면 0 오프셋으로 둔다.
        return gotRotation || gotPosition;
    }
}
