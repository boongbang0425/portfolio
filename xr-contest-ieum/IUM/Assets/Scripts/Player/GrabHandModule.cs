using UnityEngine;

/// <summary>
/// One hand. Finds a nearby <see cref="Grabbable"/>, highlights it, and attaches or releases it.
/// Two instances live on the player so both hands can hold a tool at once (F-005 1.6).
/// </summary>
public sealed class GrabHandModule : Module
{
    const int MaxCandidates = 16;

    readonly Player _player;
    readonly XRHandSide _hand;
    readonly Collider[] _candidates = new Collider[MaxCandidates];

    Grabbable _hovered;
    float _trackingLostAt = -1f;
    bool _isDistanceGrab;
    int _lockedAxis = 0; // 0 = None, 1 = Rotate, 2 = PushPull

    public GrabHandModule(Player player, XRHandSide hand) : base(player)
    {
        _player = player;
        _hand = hand;
    }

    public XRHandSide Hand => _hand;
    public Transform Anchor => _player.GetHandAnchor(_hand);
    public Grabbable Held { get; private set; }
    public Grabbable Hovered => _hovered;

    public override void OnUpdate()
    {
        var anchor = Anchor;
        if (anchor == null) return;
        if (!UpdateTracking()) return;

        var phase = _player.Input.Commands.GetGrab(_hand);

        if (Held != null)
        {
            UpdateLaser(anchor); // Turn off laser when holding
            UpdatePushPullAndRotate();

            if (_player.Input.Commands.GetInteract(_hand))
            {
                Held.Activate();
            }

            // Grip is hold-to-keep, so anything other than an active press drops the object.
            if (phase is GrabPhase.Released or GrabPhase.None) Release();
            return;
        }

        UpdateHover(anchor);
        UpdateLaser(anchor);

        if (phase == GrabPhase.Pressed && _hovered != null) Grab(_hovered, _isDistanceGrab);
    }

    public override void OnRemoved()
    {
        Release();
        SetHovered(null);
    }

    public void Release()
    {
        if (Held == null) return;

        var released = Held;
        Held = null;
        released.Detach(this);
    }

    /// <summary>
    /// F-005 1.7: a held object keeps its last valid pose during a short tracking dropout and is
    /// only dropped once the grace period expires.
    /// </summary>
    bool UpdateTracking()
    {
        if (_player.Input.IsHandTracked(_hand))
        {
            _trackingLostAt = -1f;
            return true;
        }

        if (Held == null)
        {
            SetHovered(null);
            return false;
        }

        if (_trackingLostAt < 0f) _trackingLostAt = Time.time;
        if (Time.time - _trackingLostAt < _player.TrackingLossGrace) return false;

        _trackingLostAt = -1f;
        Release();
        return false;
    }

    void UpdateHover(Transform anchor) => SetHovered(FindClosest(anchor));

    Grabbable FindClosest(Transform anchor)
    {
        // 1. 구체 범위 탐색 (근거리 잡기)
        _isDistanceGrab = false;
        var count = Physics.OverlapSphereNonAlloc(
            anchor.position, _player.GrabRadius, _candidates, _player.GrabLayers, QueryTriggerInteraction.Ignore);

        Grabbable closest = null;
        var closestDistance = float.MaxValue;

        for (var i = 0; i < count; i++)
        {
            var collider = _candidates[i];
            if (collider == null) continue;

            var candidate = collider.GetComponentInParent<Grabbable>();
            if (candidate == null || !candidate.CanGrab) continue;

            // Measured against the surface, not the origin, so a long member can be taken
            // anywhere along its body instead of only near its pivot.
            var distance = Vector3.Distance(anchor.position, collider.ClosestPoint(anchor.position));
            if (distance >= closestDistance || !candidate.IsWithinGrabDistance(distance)) continue;

            closest = candidate;
            closestDistance = distance;
        }

        // 2. 근거리(구체) 안에 잡을 물체가 없으면, 전방으로 레이저(Raycast)를 쏴서 원거리 잡기 탐색
        if (closest == null)
        {
            Vector3 rayOrigin = anchor.position;
            Vector3 rayDir = anchor.forward;

            if (_player.Input.Commands.IsDesktop)
            {
                rayOrigin = _player.Head.position;
                rayDir = _player.Head.forward;
            }
            else
            {
                rayDir = anchor.rotation * Quaternion.Euler(40f, 0f, 0f) * Vector3.forward;
            }

            if (RaycastGrabbable(rayOrigin, rayDir, out var hit, out var candidate))
            {
                closest = candidate;
                _isDistanceGrab = true;
            }
        }

        return closest;
    }

    public Quaternion GetRayRotation()
    {
        if (_player.Input.Commands.IsDesktop)
            return _player.Head.rotation;
        return Anchor.rotation * Quaternion.Euler(40f, 0f, 0f);
    }

    bool RaycastGrabbable(Vector3 origin, Vector3 dir, out RaycastHit bestHit, out Grabbable candidate)
    {
        var hits = Physics.RaycastAll(
            origin, dir, _player.DistanceGrabMaxDistance, _player.GrabLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        foreach (var hit in hits)
        {
            var c = hit.collider.GetComponentInParent<Grabbable>();
            if (c != null && c.CanGrab)
            {
                if (c.Holder == this) continue; // Skip what THIS hand is already holding
                bestHit = hit;
                candidate = c;
                return true;
            }

            if (hit.collider.transform.IsChildOf(_player.transform)) continue;
            
            bestHit = hit;
            candidate = null;
            return false;
        }
        
        bestHit = default;
        candidate = null;
        return false;
    }

    void UpdatePushPullAndRotate()
    {
        if (Held == null) return;

        var commands = _player.Input.Commands;

        var rotate = _hand == XRHandSide.Left ? commands.RotateLeft : commands.RotateRight;
        var pushPull = _hand == XRHandSide.Left ? commands.PushPullLeft : commands.PushPullRight;

        if (Mathf.Abs(rotate) < 0.05f && Mathf.Abs(pushPull) < 0.05f)
        {
            _lockedAxis = 0;
        }

        if (_lockedAxis == 0)
        {
            if (Mathf.Abs(rotate) > 0.05f || Mathf.Abs(pushPull) > 0.05f)
            {
                if (Mathf.Abs(rotate) > Mathf.Abs(pushPull)) _lockedAxis = 1;
                else _lockedAxis = 2;
            }
        }

        if (!Held.UseDynamicAttach) rotate = 0f;

        if (_lockedAxis == 1) pushPull = 0f;
        else if (_lockedAxis == 2) rotate = 0f;
        else { rotate = 0f; pushPull = 0f; }

        if (Mathf.Abs(rotate) > 0.05f)
        {
            Held.transform.Rotate(Vector3.up, rotate * -135f * Time.deltaTime, Space.World);
        }

        if (!_isDistanceGrab) return;

        if (Mathf.Abs(pushPull) > 0.05f)
        {
            // 속도와 사거리는 미터 저작값이다. ×20 씬에서 배율을 빼먹으면 0.2~5유닛(1~25cm)에
            // 갇혀 원거리로 잡은 물체가 얼굴 앞으로 붙어 버린다.
            var scale = _player.WorldUnitsPerMetre;
            var offset = Held.transform.localPosition;
            var dist = offset.magnitude;
            dist += pushPull * 2f * scale * Time.deltaTime; // 초당 2m 속도
            Held.transform.localPosition = offset.normalized * Mathf.Clamp(dist, 0.2f * scale, 5f * scale);
        }
    }

    void UpdateLaser(Transform anchor)
    {
        var line = EnsureLaser(anchor);

        if (Held != null)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        line.useWorldSpace = true;

        if (_player.LaserMaterial != null && line.sharedMaterial != _player.LaserMaterial)
            line.sharedMaterial = _player.LaserMaterial;

        Vector3 rayOrigin = anchor.position;
        Vector3 rayDir = anchor.forward;

        if (_player.Input.Commands.IsDesktop)
        {
            rayOrigin = _player.Head.position;
            rayDir = _player.Head.forward;
        }
        else
        {
            rayDir = anchor.rotation * Quaternion.Euler(40f, 0f, 0f) * Vector3.forward;
        }

        line.SetPosition(0, rayOrigin);

        if (RaycastGrabbable(rayOrigin, rayDir, out var hit, out var candidate))
        {
            line.SetPosition(1, hit.point);
            line.startColor = Color.red;
            line.endColor = Color.red;
        }
        else if (hit.collider != null) // Hit a non-grabbable object like a wall
        {
            line.SetPosition(1, hit.point);
            line.startColor = new Color(1f, 0f, 0f, 1f);
            line.endColor = new Color(1f, 0f, 0f, 0f);
        }
        else
        {
            // 아무것도 맞지 않았을 때의 허공 길이도 미터 저작값이다.
            line.SetPosition(1, rayOrigin + rayDir * (5f * _player.WorldUnitsPerMetre));
            line.startColor = new Color(1f, 0f, 0f, 1f);
            line.endColor = new Color(1f, 0f, 0f, 0f);
        }
    }

    /// <summary>
    /// 손 앵커의 레이저를 확보한다. Play 씬처럼 앵커가 빈 Transform이면 손이 화면에 전혀 나오지
    /// 않아 VR에서 무엇을 겨누는지 알 수 없었다. 씬을 고치는 대신 없을 때만 런타임에 만든다 —
    /// 이미 LineRenderer가 배치된 씬(GongpoScene)은 그 직렬화 값을 그대로 쓴다.
    ///
    /// 폭은 GongpoScene의 0.005m를 기준으로 <see cref="Player.WorldUnitsPerMetre"/>를 곱해 어느
    /// 배율의 씬에서도 같은 굵기로 보이게 한다.
    /// </summary>
    LineRenderer EnsureLaser(Transform anchor)
    {
        var line = anchor.GetComponent<LineRenderer>();
        if (line != null) return line;

        line = anchor.gameObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.widthMultiplier = LaserWidthMetres * _player.WorldUnitsPerMetre;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        line.sharedMaterial = _player.LaserMaterial != null ? _player.LaserMaterial : SharedLaserMaterial;
        return line;
    }

    /// <summary>GongpoScene에 직렬화된 레이저 폭(미터).</summary>
    const float LaserWidthMetres = 0.005f;

    static Material _sharedLaserMaterial;

    /// <summary>
    /// 씬이 머티리얼을 주지 않을 때 쓰는 공용 폴백. 손마다 새로 만들면 누수라 정적으로 한 벌만
    /// 둔다. 셰이더는 정점 색을 곱하는 것을 골라야 <see cref="LineRenderer.startColor"/>의
    /// 붉은 그라데이션이 살아난다 — URP의 Unlit은 정점 색을 무시해서 쓸 수 없다.
    /// GongpoScene에 배치된 것과 같은 내장 Sprites/Default가 URP에서도 그대로 렌더된다.
    /// </summary>
    static Material SharedLaserMaterial
    {
        get
        {
            if (_sharedLaserMaterial != null) return _sharedLaserMaterial;

            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogWarning("[GrabHand] 레이저용 셰이더를 찾지 못했습니다. 손 레이저가 보이지 않습니다.");
                return null;
            }

            _sharedLaserMaterial = new Material(shader) { name = "GrabLaser (Runtime)" };
            return _sharedLaserMaterial;
        }
    }

    void SetHovered(Grabbable target)
    {
        if (ReferenceEquals(_hovered, target)) return;

        if (_hovered != null) _hovered.SetHighlighted(false);
        _hovered = target;
        if (_hovered != null) _hovered.SetHighlighted(true);
    }

    void Grab(Grabbable target, bool distanceGrab)
    {
        if (!target.Attach(this, Anchor, distanceGrab)) return;

        Held = target;
        SetHovered(null);
        UserInput.Instance.SendHapticImpulse(_hand, 0.3f, 0.05f);
    }
}
