using System;
using UnityEngine;

/// <summary>
/// One 3D button plate of the world-space menu (F-019 2.2).
///
/// Everything moves on <see cref="Time.unscaledDeltaTime"/> because the menu only ever exists while
/// <see cref="PauseService"/> holds timeScale at zero.
///
/// The press depth is a ratio of the plate's own thickness rather than a distance in metres: the
/// prefab is uniformly scaled at runtime to fit the player's view, so any fixed offset would drift
/// out of proportion with it.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldMenuButton : MonoBehaviour, IWorldMenuTarget
{
    [Tooltip("눌렸을 때 판이 들어가는 깊이. 판 두께에 대한 비율이다.")]
    [SerializeField, Range(0.05f, 1f)] float pressDepthRatio = 0.55f;

    [Tooltip("눌림·복귀 보간 속도.")]
    [SerializeField, Min(0.1f)] float motionSpeed = 18f;

    [Header("강조")]
    [Tooltip("포인터가 올라갔을 때 판 색에 곱할 밝기.")]
    [SerializeField, Range(1f, 2f)] float hoverBrightness = 1.25f;
    [SerializeField, Range(0.3f, 1f)] float pressBrightness = 0.8f;

    [Header("햅틱")]
    [SerializeField, Range(0f, 1f)] float pressHaptic = 0.35f;
    [SerializeField, Range(0f, 0.3f)] float pressHapticSeconds = 0.06f;

    [SerializeField] bool interactable = true;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>Raised on release, only when the pointer is still on this button.</summary>
    public event Action Clicked;

    Renderer[] _renderers;
    MaterialPropertyBlock _block;
    int _colorId;
    Color _restColor;
    bool _hasColor;

    Vector3 _restLocalPosition;
    Vector3 _pressedLocalPosition;
    bool _configured;
    bool _hovered;
    bool _pressed;

    public bool IsInteractable => interactable && isActiveAndEnabled;

    public bool Interactable
    {
        get => interactable;
        set
        {
            if (interactable == value) return;
            interactable = value;
            if (!value) ResetState();
        }
    }

    void Awake()
    {
        // Not overwritten when Configure already ran: component Awake order is undefined, so the
        // owning menu may have configured this button before it woke up.
        if (!_configured)
        {
            _restLocalPosition = transform.localPosition;
            _pressedLocalPosition = _restLocalPosition;
        }

        _renderers = GetComponentsInChildren<Renderer>(true);
        _block = new MaterialPropertyBlock();
        CacheRestColor();

        // The FBX ships without colliders, so the plate gets its hit volume from its own mesh.
        WorldMenuGeometry.FitBoxCollider(transform, Vector3.zero);
    }

    void OnDisable() => ResetState();

    /// <summary>
    /// Tells the button which way is "into the panel". The caller owns the panel's orientation, so
    /// deriving the direction here from the mesh alone would only be a guess at its sign.
    /// </summary>
    public void Configure(Vector3 intoPanelWorldDirection)
    {
        if (intoPanelWorldDirection.sqrMagnitude < 1e-6f) return;

        var local = WorldMenuGeometry.SnapToAxis(
            transform.InverseTransformDirection(intoPanelWorldDirection.normalized));

        var thickness = 0f;
        if (WorldMenuGeometry.TryLocalBounds(transform, out var bounds))
        {
            var size = bounds.size;
            thickness = size[WorldMenuGeometry.ShortestAxis(size)];
        }

        _restLocalPosition = transform.localPosition;
        _pressedLocalPosition = _restLocalPosition + local * (thickness * pressDepthRatio);
        _configured = true;
    }

    void Update()
    {
        var target = _pressed ? _pressedLocalPosition : _restLocalPosition;
        transform.localPosition = Vector3.Lerp(
            transform.localPosition, target, 1f - Mathf.Exp(-motionSpeed * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(WorldMenuPointer pointer)
    {
        if (!IsInteractable) return;
        _hovered = true;
        ApplyTint();
    }

    public void OnPointerExit(WorldMenuPointer pointer)
    {
        _hovered = false;
        ApplyTint();
    }

    public void OnPointerDown(WorldMenuPointer pointer, RaycastHit hit)
    {
        if (!IsInteractable) return;

        if (!_configured)
            Debug.LogWarning($"[WorldMenu] '{name}'의 눌림 방향이 설정되지 않아 움직이지 않습니다.", this);

        _pressed = true;
        ApplyTint();
        pointer?.Pulse(pressHaptic, pressHapticSeconds);
    }

    public void OnPointerDrag(WorldMenuPointer pointer) { }

    public void OnPointerUp(WorldMenuPointer pointer, bool onTarget)
    {
        var wasPressed = _pressed;
        _pressed = false;
        _hovered = onTarget;
        ApplyTint();

        // 클릭은 같은 버튼 위에서 떼었을 때만 성립한다. Dragging off a button is how a player cancels.
        if (wasPressed && onTarget && IsInteractable) Clicked?.Invoke();
    }

    void ResetState()
    {
        _pressed = false;
        _hovered = false;
        transform.localPosition = _restLocalPosition;
        ApplyTint();
    }

    void CacheRestColor()
    {
        foreach (var renderer in _renderers)
        {
            var material = renderer.sharedMaterial;
            if (material == null) continue;

            if (material.HasProperty(BaseColorId)) _colorId = BaseColorId;
            else if (material.HasProperty(ColorId)) _colorId = ColorId;
            else continue;

            _restColor = material.GetColor(_colorId);
            _hasColor = true;
            return;
        }
    }

    /// <summary>
    /// Brightness is pushed through a property block so the shared wood material — used by every
    /// plate in both panels — is never written to.
    /// </summary>
    void ApplyTint()
    {
        if (!_hasColor || _renderers == null) return;

        var multiplier = 1f;
        if (_pressed) multiplier = pressBrightness;
        else if (_hovered) multiplier = hoverBrightness;

        var color = _restColor * multiplier;
        color.a = _restColor.a;

        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(_block);
            _block.SetColor(_colorId, color);
            renderer.SetPropertyBlock(_block);
        }
    }
}
