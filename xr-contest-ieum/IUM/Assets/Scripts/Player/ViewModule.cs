using UnityEngine;

/// <summary>
/// Desktop-only head aiming. In XR the HMD owns the head pose, so this module idles instead of
/// fighting the tracked camera.
/// </summary>
public sealed class ViewModule : Module
{
    readonly Player _player;
    float _pitch;

    public ViewModule(Player player) : base(player)
    {
        _player = player;
        Application.onBeforeRender += UpdateVRTracking;
    }

    public override void OnRemoved()
    {
        Application.onBeforeRender -= UpdateVRTracking;
    }

    void UpdateVRTracking()
    {
        var commands = _player.Input.Commands;
        var head = _player.Head;
        if (commands.IsDesktop || IsLocked || head == _player.transform) return;

        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
        if (device.isValid)
        {
            // 장치 포즈는 항상 미터다. 월드가 ×20으로 저작된 씬에서는 그대로 넣으면 눈높이 1.6m가
            // 1.6유닛(=8cm)이 돼 바닥에 붙는다. 회전은 단위가 없어 그대로 쓴다.
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var pos))
                head.localPosition = pos * _player.WorldUnitsPerMetre;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out var rot))
                head.localRotation = rot;
        }
    }

    public bool IsLocked { get; set; }

    public override void OnUpdate()
    {
        var commands = _player.Input.Commands;
        var head = _player.Head;
        if (head == _player.transform) return;

        if (!commands.IsDesktop || IsLocked) return;

        var look = commands.Look * _player.LookSensitivity;
        if (look.sqrMagnitude > 0f)
        {
            // Yaw turns the whole body so movement direction follows the view.
            _player.transform.Rotate(Vector3.up, look.x, Space.World);
            _pitch = Mathf.Clamp(_pitch - look.y, -_player.PitchLimit, _player.PitchLimit);
        }

        head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    /// <summary>
    /// 외부 연출이 시선을 지정한다(튜토리얼의 첫 발화 주시 등). 입력 경로와 같은 규칙을 지켜
    /// yaw는 몸(플레이어 루트), pitch는 머리의 로컬 회전에 넣고 <see cref="Player.PitchLimit"/>로
    /// 자른다. 그래서 회전이 끝난 뒤 마우스를 움직여도 시점이 튀지 않는다.
    ///
    /// 데스크톱 전용이다. XR에서는 HMD가 머리 포즈를 소유하므로 여기서 돌려도 다음 프레임에
    /// 덮어써지고, 강제 시점 회전 자체가 멀미 요인이라 호출하는 쪽이 걸러야 한다.
    /// </summary>
    public void SetLookDirection(Vector3 worldDirection)
    {
        var head = _player.Head;
        if (head == _player.transform || worldDirection.sqrMagnitude < 0.0001f) return;

        var flat = Vector3.ProjectOnPlane(worldDirection, Vector3.up);

        // 바로 위나 아래를 향하면 yaw를 정할 수 없다. 그때는 현재 yaw를 유지하고 pitch만 바꾼다.
        if (flat.sqrMagnitude > 0.0001f)
            _player.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

        // 위를 보는 방향이 음의 pitch다 — OnUpdate가 마우스 상향 이동에서 _pitch를 빼는 것과 같다.
        var pitch = -Mathf.Atan2(worldDirection.y, flat.magnitude) * Mathf.Rad2Deg;
        _pitch = Mathf.Clamp(pitch, -_player.PitchLimit, _player.PitchLimit);
        head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}
