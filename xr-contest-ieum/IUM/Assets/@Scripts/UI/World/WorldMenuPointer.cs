using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Ray pointer for the world-space menu. One per hand in VR; the desktop path uses the mouse cursor
/// through the main camera.
///
/// Reads <see cref="UserInput"/> directly rather than <see cref="PlayerCommands"/>. An open menu holds
/// <see cref="InputLockFlags.Menu"/>, which zeroes Interact for every consumer downstream — a menu
/// driven through that path would lock out its own buttons. <see cref="PauseController"/> reads its
/// own input for the same reason.
///
/// 데스크톱 조작: 마우스로 겨누고 좌클릭. VR: 손에서 나가는 레이를 겨누고 트리거.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldMenuPointer : MonoBehaviour
{
    [Tooltip("VR에서 이 포인터가 따라갈 손.")]
    [SerializeField] XRHandSide hand = XRHandSide.Right;

    [Tooltip("이 루트 아래의 콜라이더만 겨눈다. 정지 중 주변 지형이 메뉴를 가리지 않게 한다.")]
    [SerializeField] Transform menuRoot;

    [SerializeField, Min(0.1f)] float maxDistance = 8f;

    [Header("표시")]
    [SerializeField] Material lineMaterial;
    [SerializeField] Color lineColor = new(0.98f, 0.86f, 0.55f, 0.9f);
    [SerializeField, Min(0.0005f)] float lineWidth = 0.004f;
    [Tooltip("아무것도 겨누지 않았을 때 그리는 레이 길이.")]
    [SerializeField, Min(0.1f)] float idleLength = 1.5f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    LineRenderer _line;
    Material _ownedMaterial;
    Player _player;
    IWorldMenuTarget _hovered;
    IWorldMenuTarget _captured;
    float _nextPlayerLookup;

    /// <summary>Where the pointer is aiming this frame, in world space.</summary>
    public Ray CurrentRay { get; private set; }

    /// <summary>True while no HMD is driving the session, which also means the mouse is the pointer.</summary>
    public bool IsDesktop => !XRSettings.isDeviceActive;

    public XRHandSide Hand => hand;

    public void SetMenuRoot(Transform root) => menuRoot = root;

    void Awake() => BuildLine();

    void OnDisable()
    {
        // Releasing rather than dropping: a slider captured mid-drag would otherwise stay captured
        // and resume the drag the next time the menu opens.
        ReleaseCapture(false);
        SetHovered(null);
        if (_line != null) _line.enabled = false;
    }

    void Update()
    {
        if (menuRoot == null) return;

        if (!TryBuildRay(out var ray))
        {
            SetHovered(null);
            if (_line != null) _line.enabled = false;
            return;
        }

        CurrentRay = ray;

        var target = Raycast(ray, out var hit);
        var pressed = ReadPressed();

        if (_captured != null)
        {
            _captured.OnPointerDrag(this);
            if (!pressed) ReleaseCapture(ReferenceEquals(target, _captured));
        }
        else
        {
            SetHovered(target);
            if (pressed && ReadPressedThisFrame() && target != null && target.IsInteractable)
            {
                _captured = target;
                target.OnPointerDown(this, hit);
            }
        }

        DrawLine(ray, hit, target != null);
    }

    /// <summary>Sends a controller haptic pulse if the device supports one. Silent when it does not.</summary>
    public void Pulse(float amplitude, float seconds)
    {
        if (IsDesktop || amplitude <= 0f || seconds <= 0f) return;
        if (UserInput.HasInstance) UserInput.Instance.SendHapticImpulse(hand, amplitude, seconds);
    }

    bool TryBuildRay(out Ray ray)
    {
        ray = default;

        if (IsDesktop)
        {
            var camera = Camera.main;
            if (camera == null || !UserInput.HasInstance) return false;

            ray = camera.ScreenPointToRay(UserInput.Instance.MousePosition);
            return true;
        }

        var anchor = ResolveHandAnchor();
        if (anchor == null) return false;

        ray = new Ray(anchor.position, anchor.forward);
        return true;
    }

    Transform ResolveHandAnchor()
    {
        if (_player == null && Time.unscaledTime >= _nextPlayerLookup)
        {
            _nextPlayerLookup = Time.unscaledTime + 0.5f;
            _player = FindAnyObjectByType<Player>();
        }

        return _player != null ? _player.GetHandAnchor(hand) : null;
    }

    bool ReadPressed()
    {
        if (!UserInput.HasInstance) return false;
        var input = UserInput.Instance;
        return IsDesktop
            ? input.GetMouseButton(MouseButton.Left)
            : input.GetXRButton(hand, XRInputButton.Select);
    }

    bool ReadPressedThisFrame()
    {
        if (!UserInput.HasInstance) return false;
        var input = UserInput.Instance;
        return IsDesktop
            ? input.GetMouseButtonDown(MouseButton.Left)
            : input.GetXRButtonDown(hand, XRInputButton.Select);
    }

    /// <summary>
    /// Nearest menu target along the ray. Colliders outside <see cref="menuRoot"/> are skipped rather
    /// than treated as blockers, so world geometry standing between the player and the panel cannot
    /// make the menu unusable.
    /// </summary>
    IWorldMenuTarget Raycast(Ray ray, out RaycastHit hit)
    {
        hit = default;

        var hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Collide);

        IWorldMenuTarget best = null;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < hits.Length; i++)
        {
            var candidate = hits[i];
            if (candidate.distance >= bestDistance) continue;
            if (!candidate.transform.IsChildOf(menuRoot)) continue;

            var target = candidate.transform.GetComponentInParent<IWorldMenuTarget>();
            if (target == null) continue;

            best = target;
            bestDistance = candidate.distance;
            hit = candidate;
        }

        return best;
    }

    void SetHovered(IWorldMenuTarget target)
    {
        if (ReferenceEquals(_hovered, target)) return;

        _hovered?.OnPointerExit(this);
        _hovered = target;
        _hovered?.OnPointerEnter(this);
    }

    void ReleaseCapture(bool onTarget)
    {
        if (_captured == null) return;

        var captured = _captured;
        _captured = null;
        captured.OnPointerUp(this, onTarget);
    }

    void BuildLine()
    {
        _line = GetComponent<LineRenderer>();
        if (_line == null) _line = gameObject.AddComponent<LineRenderer>();

        _line.useWorldSpace = true;
        _line.positionCount = 2;
        _line.widthMultiplier = lineWidth;
        _line.textureMode = LineTextureMode.Stretch;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.startColor = lineColor;
        _line.endColor = lineColor;

        _line.sharedMaterial = lineMaterial != null ? lineMaterial : BuildFallbackMaterial();
    }

    /// <summary>
    /// A LineRenderer with no material renders magenta, which would be the first thing the player
    /// sees on pausing. Built here rather than left to an inspector reference so the prefab works
    /// straight out of the generator.
    /// </summary>
    Material BuildFallbackMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader == null) return null;

        _ownedMaterial = new Material(shader) { name = "WorldMenuPointer Ray" };

        if (_ownedMaterial.HasProperty(BaseColorId)) _ownedMaterial.SetColor(BaseColorId, lineColor);
        else if (_ownedMaterial.HasProperty(ColorId)) _ownedMaterial.SetColor(ColorId, lineColor);

        return _ownedMaterial;
    }

    void OnDestroy()
    {
        if (_ownedMaterial != null) Destroy(_ownedMaterial);
    }

    void DrawLine(Ray ray, RaycastHit hit, bool hasTarget)
    {
        if (_line == null) return;

        _line.enabled = true;

        // LineRenderer width follows the transform's scale even in world space, and the menu root is
        // rescaled at runtime to fit the player's view. Dividing it back out keeps the ray the same
        // thickness whatever the panel ends up sized at.
        _line.widthMultiplier = lineWidth / Mathf.Max(Mathf.Abs(transform.lossyScale.x), 1e-4f);

        var end = hasTarget ? hit.point : ray.origin + ray.direction * idleLength;
        _line.SetPosition(0, ray.origin);
        _line.SetPosition(1, end);
    }
}
