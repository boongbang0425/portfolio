using System;
using UnityEngine;

/// <summary>
/// One volume slider of the 옵션 panel (F-002 2.2). Sits on the track mesh and drives a separate
/// handle mesh along it.
///
/// Travel is derived from the two meshes rather than authored: the track's longest local axis is the
/// direction, and the reachable centres are the track ends inset by half the handle. That keeps the
/// component correct if the FBX is reimported or the panel is rescaled.
///
/// 조작은 클릭 후 드래그다. The whole track is clickable, so pressing anywhere on it jumps the handle
/// there and then continues as a drag.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldMenuSlider : MonoBehaviour, IWorldMenuTarget
{
    [Tooltip("트랙을 따라 움직이는 손잡이 메시.")]
    [SerializeField] Transform handle;

    [Tooltip("트랙 히트 영역의 두께 배수. 트랙 메시는 겨누기에 너무 얇다.")]
    [SerializeField, Range(1f, 8f)] float hitThicknessScale = 4f;

    [Header("햅틱")]
    [SerializeField, Range(0f, 1f)] float grabHaptic = 0.25f;
    [SerializeField, Range(0f, 0.3f)] float grabHapticSeconds = 0.05f;

    [SerializeField] bool interactable = true;

    /// <summary>Raised on every change the player makes, not on <see cref="SetValueWithoutNotify"/>.</summary>
    public event Action<float> ValueChanged;

    int _axis;
    float _minCentre;
    float _maxCentre;
    float _handleMeshOffset;
    Vector3 _trackCentre;
    bool _ready;
    bool _dragging;

    public float Value { get; private set; }

    public bool IsInteractable => interactable && _ready && isActiveAndEnabled;

    void Awake() => Prepare();

    void OnDisable() => _dragging = false;

    void Prepare()
    {
        if (_ready) return;

        if (handle == null)
        {
            Debug.LogError($"[WorldMenu] '{name}' 슬라이더에 손잡이가 지정되지 않았습니다.", this);
            return;
        }

        if (!WorldMenuGeometry.TryLocalBounds(transform, out var track) ||
            !WorldMenuGeometry.TryLocalBounds(handle, out var knob))
        {
            Debug.LogError($"[WorldMenu] '{name}' 슬라이더의 메시 경계를 읽지 못했습니다.", this);
            return;
        }

        _axis = WorldMenuGeometry.LongestAxis(track.size);
        _trackCentre = track.center;

        // Handle and track are siblings under the same identity-rotation parent and this component
        // sits on the track at localPosition zero, so track-local space is also parent space and the
        // two sets of bounds are directly comparable.
        //
        // The offset is taken from the handle's mesh, never from its current localPosition: the
        // prefab may have been saved with the handle already moved.
        _handleMeshOffset = knob.center[_axis];
        var handleHalf = knob.extents[_axis];

        _minCentre = track.center[_axis] - track.extents[_axis] + handleHalf;
        _maxCentre = track.center[_axis] + track.extents[_axis] - handleHalf;

        if (_maxCentre - _minCentre < 1e-5f)
        {
            Debug.LogError($"[WorldMenu] '{name}' 슬라이더의 이동 구간이 0입니다.", this);
            return;
        }

        BuildHitVolume(track, knob);
        _ready = true;
        ApplyHandle();
    }

    /// <summary>
    /// The track mesh is a shallow groove roughly a third of a millimetre deep at authored scale.
    /// A collider fitted to it exactly would be almost impossible to hit with a ray, so the two
    /// non-travel axes are padded out to a multiple of the handle.
    /// </summary>
    void BuildHitVolume(Bounds track, Bounds knob)
    {
        var minimum = Vector3.zero;
        for (var axis = 0; axis < 3; axis++)
            minimum[axis] = axis == _axis ? 0f : knob.size[axis] * hitThicknessScale;

        WorldMenuGeometry.FitBoxCollider(transform, minimum);
    }

    /// <summary>Moves the handle without raising <see cref="ValueChanged"/>. For pulling saved values in.</summary>
    public void SetValueWithoutNotify(float value)
    {
        Prepare();
        Value = Mathf.Clamp01(value);
        ApplyHandle();
    }

    public void OnPointerEnter(WorldMenuPointer pointer) { }

    public void OnPointerExit(WorldMenuPointer pointer) { }

    public void OnPointerDown(WorldMenuPointer pointer, RaycastHit hit)
    {
        if (!IsInteractable) return;

        _dragging = true;
        pointer?.Pulse(grabHaptic, grabHapticSeconds);
        Apply(ValueAt(hit.point));
    }

    public void OnPointerDrag(WorldMenuPointer pointer)
    {
        if (!_dragging || pointer == null) return;

        // Tracked against the travel line rather than against the collider: once the drag starts the
        // player should be able to keep pulling past the end of the track without losing the handle.
        var start = _trackCentre;
        start[_axis] = _minCentre;

        var origin = transform.TransformPoint(start);
        var direction = transform.TransformDirection(WorldMenuGeometry.Axis(_axis)) *
                        (_maxCentre - _minCentre);

        Apply(WorldMenuGeometry.ClosestPointOnLine(pointer.CurrentRay, origin, direction));
    }

    public void OnPointerUp(WorldMenuPointer pointer, bool onTarget) => _dragging = false;

    float ValueAt(Vector3 worldPoint)
    {
        var local = transform.InverseTransformPoint(worldPoint);
        return Mathf.InverseLerp(_minCentre, _maxCentre, local[_axis]);
    }

    void Apply(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, Value)) return;

        Value = value;
        ApplyHandle();
        ValueChanged?.Invoke(Value);
    }

    void ApplyHandle()
    {
        if (handle == null || _maxCentre - _minCentre < 1e-5f) return;

        var position = handle.localPosition;
        position[_axis] = Mathf.Lerp(_minCentre, _maxCentre, Value) - _handleMeshOffset;
        handle.localPosition = position;
    }
}
