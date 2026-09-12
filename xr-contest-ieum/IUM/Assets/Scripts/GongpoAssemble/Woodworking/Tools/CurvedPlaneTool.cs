using UnityEngine;

public class CurvedPlaneTool : MonoBehaviour, IWoodTool
{
    public ToolType GetToolType() => ToolType.CurvedPlane;
    public bool IsActive() => true;

    [Header("Planing Settings")]
    [Tooltip("Minimum speed required to register a valid cut (m/s).")]
    public float minCutSpeed = 0.1f;
    [Tooltip("Local direction along which the plane cuts.")]
    public Vector3 cuttingDirection = Vector3.forward;
    [Tooltip("Maximum angle deviation between movement and cutting direction (degrees).")]
    public float maxAngleTolerance = 30f;

    [Header("Feedback")]
    public ParticleSystem woodShavingParticles;
    [Range(0f, 1f)] public float hapticIntensity = 0.3f;
    public float hapticDuration = 0.05f;
    [Tooltip("대패질 시 재생할 소리")]
    public AudioClip planeSound;

    // ── Private state ──────────────────────────────────────────────────────
    private PlaneZone currentZone;
    private System.Collections.Generic.List<PlaneZone> overlappingZones = new System.Collections.Generic.List<PlaneZone>();
    private Vector3   lastPosition;
    private Grabbable customGrabbable;
    private AudioSource audioSource;
    private bool  isCutting             = false;
    private float currentStrokeDistance = 0f;
    private float cutGraceTimer         = 0f;

    // ── Unity lifecycle ────────────────────────────────────────────────────
    private void Start()
    {
        customGrabbable = GetComponent<Grabbable>();
        
        if (planeSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = planeSound;
            audioSource.spatialBlend = 1.0f; // 3D Sound
            audioSource.loop = true;
            // 기본 감쇠(min 1 / max 500 / Logarithmic)는 미터 전제라 ×12 씬에서 8cm부터 줄어든다.
            SceneWorldScale.Configure3D(audioSource);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var zone = other.GetComponent<PlaneZone>();
        if (zone != null && zone.requiredToolType == GetToolType() && !overlappingZones.Contains(zone))
        {
            overlappingZones.Add(zone);
        }
    }

    /// <summary>
    /// 이동 방향과 기준 축의 각도를 <b>축</b> 기준으로 접는다(0~90°).
    ///
    /// 대패질은 밀고 당기는 왕복이고, <see cref="ProcessGuideService"/>가 그리는 안내선도
    /// <c>planeDirection</c> 양쪽으로 뻗은 선분이라 "정방향"이라는 것이 화면에 드러나지 않는다.
    /// 게다가 씬의 <c>planeDirection</c>은 부재 로컬 ±X로 저작돼 있어 월드에서는 서로 반대 방향을
    /// 요구하는 존이 한 부재에 섞여 있다(Play 씬 MediumPart 실측: CurvedPlaneZoneLeft가 월드
    /// +Z에서 14.5°, Right가 165.3°). 단방향 판정이면 그중 절반은 도구를 180° 돌리기 전에는
    /// 어떤 스트로크로도 통과할 수 없다. 톱질(SawTool)에서 같은 이유로 양방향으로 바꾼 선례를 따른다.
    /// </summary>
    private static float FoldedAngle(Vector3 a, Vector3 b)
    {
        float angle = Vector3.Angle(a, b);
        return Mathf.Min(angle, 180f - angle);
    }

    /// <summary>
    /// 지금 겹쳐 있는 미완료 존 가운데 <paramref name="moveDir"/>에 가장 잘 맞는 것.
    ///
    /// 부재 하나에 대패 존이 여러 개 붙어 있고 서로 붙어 있어(Play 씬 MediumPart: 대패 존 7개가
    /// 지름 0.25유닛 안, 도구 콜라이더는 4유닛대) 도구 하나가 그 부재의 대패 존 전부와 동시에
    /// 접촉한다. 종전처럼 '먼저 접촉한 존'에 고정하면 그 존이 요구하는 방향과 다르게 미는 동안은
    /// 아무 일도 일어나지 않고, 겹침이 유지되는 한 다른 존으로 넘어갈 수단도 없다 — 리드가 보고한
    /// "대패 작동 안 함"의 실체다. 방향으로 고르면 안내선을 따라 민 방향의 존이 진행된다.
    /// </summary>
    private PlaneZone PickZone(Vector3 moveDir, bool hasDirection)
    {
        PlaneZone best      = null;
        float     bestAngle = float.MaxValue;

        for (int i = 0; i < overlappingZones.Count; i++)
        {
            var zone = overlappingZones[i];
            if (zone == null || zone.IsCompleted) continue;

            if (!hasDirection)
            {
                if (best == null) best = zone;
                continue;
            }

            float angle = FoldedAngle(moveDir, zone.transform.TransformDirection(zone.planeDirection));
            if (angle >= bestAngle) continue;

            bestAngle = angle;
            best      = zone;
        }

        return best;
    }

    private void Update()
    {
        // 완료된 존 해제
        if (currentZone != null && currentZone.IsCompleted)
        {
            if (isCutting) FinishStroke();
            currentZone = null;
        }

        overlappingZones.RemoveAll(zone => zone == null);

        Vector3 currentPos = transform.position;
        Vector3 moveDelta  = currentPos - lastPosition;
        float   distance   = moveDelta.magnitude;
        lastPosition       = currentPos;

        bool    hasDirection = Time.deltaTime > 0f && distance > 0.0001f;
        Vector3 moveDir      = hasDirection ? moveDelta / distance : Vector3.zero;

        // 스트로크 도중에는 존을 바꾸지 않는다. 진행 중인 한 획이 다른 존으로 넘어가면 어느 쪽도
        // 최소 이동 거리를 못 채운다.
        if (!isCutting) currentZone = PickZone(moveDir, hasDirection);

        if (currentZone == null)
        {
            StopFeedback();
            return;
        }

        bool  validCut    = false;
        float speed       = 0f;
        float angleToTool = 0f;
        float angleToZone = 0f;

        if (hasDirection)
        {
            speed = distance / Time.deltaTime;
            if (speed >= minCutSpeed)
            {
                Vector3 localCutDir  = transform.TransformDirection(cuttingDirection);
                Vector3 zoneRequired = currentZone.transform.TransformDirection(currentZone.planeDirection);

                angleToTool = FoldedAngle(moveDir, localCutDir);
                angleToZone = FoldedAngle(moveDir, zoneRequired);

                // 리드 결정(2026-08-27): 대패는 방향 무관하게 깎인다. 씬의 planeDirection 저작이
                // 면마다 제각각이라 방향 게이트가 실사용을 막았다. 각도는 품질 점수 계산용으로만 넘긴다.
                validCut = true;
            }
        }

        if (validCut)
        {
            isCutting             = true;
            currentStrokeDistance += distance;
            cutGraceTimer         = 0.2f;

            // 너무 짧은 거리에선 햅틱/이펙트를 무시하여 잔떨림 방지
            if (currentStrokeDistance > 0.02f)
            {
                PlayFeedback();
            }
        }
        else
        {
            StopFeedback(); // 정지 시 즉각 햅틱/파티클 끔

            if (isCutting)
            {
                cutGraceTimer -= Time.deltaTime;
                if (cutGraceTimer <= 0f)
                {
                    FinishStroke();
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var zone = other.GetComponent<PlaneZone>();
        if (zone != null)
        {
            overlappingZones.Remove(zone);
            if (currentZone == zone)
            {
                FinishStroke();
                currentZone = null;
            }
        }
    }

    private void FinishStroke()
    {
        if (isCutting && currentZone != null)
        {
            if (currentZone.IsStrokeSuccessful(currentStrokeDistance))
            {
                currentZone.AddStrokeProgress();
            }
            else
            {
                Debug.Log($"[CurvedPlaneTool] Stroke failed - distance too short: {currentStrokeDistance:F2}m");
            }
        }

        isCutting             = false;
        currentStrokeDistance = 0f;
        StopFeedback();
    }

    // ── Feedback ───────────────────────────────────────────────────────────
    private void PlayFeedback()
    {
        if (woodShavingParticles != null && !woodShavingParticles.isPlaying)
            woodShavingParticles.Play();

        if (customGrabbable != null && customGrabbable.IsHeld)
            UserInput.Instance.SendHapticImpulse(customGrabbable.Holder.Hand, hapticIntensity, hapticDuration);
            
        if (audioSource != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    private void StopFeedback()
    {
        if (woodShavingParticles != null && woodShavingParticles.isPlaying)
            woodShavingParticles.Stop();
            
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }
}
