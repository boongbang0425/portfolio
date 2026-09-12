using UnityEngine;

/// <summary>
/// Player root. Holds references and tuning values only; every behaviour lives in a Module.
/// Module order matters: <see cref="PlayerInputModule"/> is added first so the rest of the
/// modules read commands that were refreshed this frame.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class Player : MonoThing
{
    [Header("References")]
    [SerializeField] Transform head;
    [SerializeField] Transform leftHandAnchor;
    [SerializeField] Transform rightHandAnchor;

    [Header("World Scale")]
    [Tooltip("이 씬에서 1미터가 몇 유닛인가. XR 추적 포즈와 미터 전제 상수(손 오프셋·잡기 반경·중력)를 " +
             "월드 단위로 옮기는 배율이다. 1× 저작 씬(GongpoScene 등)은 1을 그대로 둔다. " +
             "Play 씬은 WorkshopImport 루트가 ×20이라 20이다.")]
    [SerializeField, Min(0.0001f)] float worldUnitsPerMetre = 1f;

    [Header("Locomotion")]
    [SerializeField, Min(0f)] float moveSpeed = 2.2f;
    [SerializeField, Min(0f)] float snapTurnAngle = 45f;
    [SerializeField] float gravity = -9.81f;

    [Header("View")]
    [SerializeField, Min(0f)] float lookSensitivity = 2.5f;
    [SerializeField, Range(30f, 89f)] float pitchLimit = 85f;

    [Header("Hands")]
    [Tooltip("Desktop hand anchor offset from the head. x is mirrored for the left hand.")]
    [SerializeField] Vector3 desktopHandOffset = new(0.25f, -0.3f, 0.55f);
    [SerializeField, Min(0f)] float grabRadius = 0.35f;
    [Tooltip("Maximum ray distance for distance grabbing.")]
    [SerializeField, Min(0f)] float distanceGrabMaxDistance = 5f;
    [SerializeField] LayerMask grabLayers = ~0;
    [Tooltip("How long a held object keeps its last valid pose after tracking is lost (F-005 1.7).")]
    [SerializeField, Min(0f)] float trackingLossGrace = 1f;
    [SerializeField] Material laserMaterial;

    public CharacterController Controller { get; private set; }
    public PlayerInputModule Input { get; private set; }
    public LocomotionModule Locomotion { get; private set; }
    public ViewModule View { get; private set; }

    public Transform Head => head != null ? head : transform;

    /// <summary>
    /// 1미터에 해당하는 월드 유닛 수. XR 장치 포즈(미터 고정)와 인스펙터의 미터 전제 값을
    /// 이 씬의 단위로 옮기는 데 쓴다. 루트 localScale로 대신하지 않는 이유는 CharacterController와
    /// 물리 질의까지 함께 스케일돼 이동·충돌이 어긋나기 때문이다.
    /// </summary>
    public float WorldUnitsPerMetre => worldUnitsPerMetre;

    /// <summary>moveSpeed는 씬에서 이미 월드 단위로 조정돼 있어 배율을 다시 곱하지 않는다.</summary>
    public float MoveSpeed => moveSpeed;
    public float SnapTurnAngle => snapTurnAngle;

    /// <summary>중력은 m/s²로 저작한다. 배율을 곱하지 않으면 ×20 씬에서 낙하가 20배 느려진다.</summary>
    public float Gravity => gravity * worldUnitsPerMetre;
    public Material LaserMaterial => laserMaterial;
    public float LookSensitivity => lookSensitivity;
    public float PitchLimit => pitchLimit;

    /// <summary>미터 저작값. 배율을 적용한 월드 단위 오프셋을 돌려준다.</summary>
    public Vector3 DesktopHandOffset => desktopHandOffset * worldUnitsPerMetre;

    /// <summary>미터 저작값. 배율을 적용한 월드 단위 반경을 돌려준다.</summary>
    public float GrabRadius => grabRadius * worldUnitsPerMetre;

    /// <summary>distanceGrabMaxDistance는 씬에서 이미 월드 단위로 조정돼 있어 배율을 곱하지 않는다.</summary>
    public float DistanceGrabMaxDistance => distanceGrabMaxDistance;
    public LayerMask GrabLayers => grabLayers;
    public float TrackingLossGrace => trackingLossGrace;

    protected override void Awake()
    {
        base.Awake();

        Controller = GetComponent<CharacterController>();
        if (head == null)
        {
            var camera = GetComponentInChildren<Camera>();
            head = camera != null ? camera.transform : transform;
        }

        Input = Attach(new PlayerInputModule(this));
        Locomotion = Attach(new LocomotionModule(this));
        View = Attach(new ViewModule(this));
        Attach(new GrabHandModule(this, XRHandSide.Left));
        Attach(new GrabHandModule(this, XRHandSide.Right));
        Attach(new VoiceInputModule(this));

        WarnIfScaleMismatch();
    }

    /// <summary>
    /// 배율과 캡슐 신장이 어긋나면 XR에서 머리가 캡슐 밖으로 나가거나 손이 닿지 않는다. 씬을 복제해
    /// 놓고 <see cref="worldUnitsPerMetre"/>만 안 고치는 실수가 이 경로로 드러난다. 경고만 남기고
    /// 값을 고치지는 않는다 — 앉은 자세나 어린이 시점처럼 의도된 예외가 있을 수 있다.
    /// </summary>
    void WarnIfScaleMismatch()
    {
        const float ReferenceHeightMetres = 1.7f;
        if (Controller == null || worldUnitsPerMetre <= 0f) return;

        var impliedMetres = Controller.height / worldUnitsPerMetre;
        if (impliedMetres > ReferenceHeightMetres * 0.6f && impliedMetres < ReferenceHeightMetres * 1.4f) return;

        Debug.LogWarning(
            $"[Player] worldUnitsPerMetre={worldUnitsPerMetre:0.###}에서 CharacterController.height " +
            $"{Controller.height:0.###}유닛은 신장 {impliedMetres:0.##}m에 해당합니다. " +
            "배율 또는 캡슐 치수 중 하나가 씬과 어긋났는지 확인하세요.", this);
    }

    public Transform GetHandAnchor(XRHandSide hand) =>
        hand == XRHandSide.Left ? leftHandAnchor : rightHandAnchor;

    public GrabHandModule GetHand(XRHandSide hand)
    {
        foreach (var module in GetModules<GrabHandModule>())
            if (module.Hand == hand) return module;
        return null;
    }

    // Draws the reach of both hands so an out-of-range grab is obvious in the scene view.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        DrawHandGizmo(XRHandSide.Left);
        DrawHandGizmo(XRHandSide.Right);
    }

    void DrawHandGizmo(XRHandSide hand)
    {
        var anchor = GetHandAnchor(hand);
        if (anchor != null) Gizmos.DrawWireSphere(anchor.position, grabRadius * Mathf.Max(worldUnitsPerMetre, 0.0001f));
    }

    T Attach<T>(T module) where T : Module
    {
        AddModule(module);
        module.Init();
        return module;
    }
}
