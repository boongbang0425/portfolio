using UnityEngine;

public class SawTool : MonoBehaviour, IWoodTool
{
    public ToolType GetToolType() => ToolType.Saw;
    public bool IsActive() => true;

    [Header("Saw Settings")]
    [Tooltip("Minimum speed required to register a valid cut (m/s).")]
    public float minCutSpeed = 0.05f;
    [Tooltip("Local direction along which the saw cuts.")]
    public Vector3 cuttingDirection = Vector3.forward;
    [Tooltip("Maximum angle deviation between movement and cutting direction (degrees).")]
    public float maxAngleTolerance = 30f;
    [Tooltip("If true, sawing works in both push and pull directions.")]
    public bool allowBiDirectionalCut = false;

    [Header("Feedback")]
    public ParticleSystem sawdustParticles;
    [Range(0f, 1f)] public float hapticIntensity = 0.4f;
    public float hapticDuration = 0.05f;
    [Tooltip("톱질 시 재생할 소리")]
    public AudioClip sawSound;

    // ── Private state ──────────────────────────────────────────────────────
    private SawZone currentZone;
    private System.Collections.Generic.List<SawZone> overlappingZones = new System.Collections.Generic.List<SawZone>();
    private Vector3 lastPosition;
    private Grabbable customGrabbable;
    private AudioSource audioSource;
    private bool isCutting = false;
    private float currentStrokeDistance = 0f;
    private float cutGraceTimer = 0f;

    
    // 평균 품질 계산용
    private float strokeSpeedAccum = 0f;
    private float strokeAngleAccum = 0f;
    private int frameCount = 0;

    private void Start()
    {
        customGrabbable = GetComponent<Grabbable>();
        
        if (sawSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = sawSound;
            audioSource.spatialBlend = 1.0f; // 3D Sound
            audioSource.loop = true;
            // 기본 감쇠(min 1 / max 500 / Logarithmic)는 미터 전제라 ×12 씬에서 8cm부터 줄어든다.
            SceneWorldScale.Configure3D(audioSource);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var zone = other.GetComponent<SawZone>();
        if (zone != null && !overlappingZones.Contains(zone))
        {
            overlappingZones.Add(zone);
        }
    }

    private void Update()
    {
        // 완료되었거나 범위를 벗어난 존 해제
        if (currentZone != null && currentZone.IsCompleted)
        {
            if (isCutting) FinishStroke();
            currentZone = null;
        }

        // 새 존 찾기 (아직 완료되지 않은 존으로)
        if (currentZone == null && overlappingZones.Count > 0)
        {
            foreach (var z in overlappingZones)
            {
                if (!z.IsCompleted)
                {
                    currentZone = z;
                    lastPosition = transform.position;
                    break;
                }
            }
        }

        if (currentZone == null) return;

        Vector3 currentPos = transform.position;
        Vector3 moveDelta = currentPos - lastPosition;
        float distance = moveDelta.magnitude;
        bool validCut = false;
        
        float currentSpeed = 0f;
        float minAngleToZone = 0f;

        if (Time.deltaTime > 0f && distance > 0.0001f)
        {
            currentSpeed = distance / Time.deltaTime;
            if (currentSpeed >= minCutSpeed)
            {
                Vector3 moveDir = moveDelta.normalized;
                Vector3 localCutDir = transform.TransformDirection(cuttingDirection);
                Vector3 zoneRequired = currentZone.transform.TransformDirection(currentZone.sawDirection);

                // 톱질은 밀고 당기는 왕복이다(퀘스트 힌트도 "밀고 당겨"라 안내). 단방향 판정이면
                // 당기는 스트로크가 전부 무효가 되어 실사용에서 인식 불가로 나타나므로(이동↔톱날
                // 158° 실측), 씬의 allowBiDirectionalCut 값과 무관하게 항상 양방향으로 판정한다.
                float angleToTool = Vector3.Angle(moveDir, localCutDir);
                float minAngleToTool = Mathf.Min(angleToTool, 180f - angleToTool);

                float angleToZone = Vector3.Angle(moveDir, zoneRequired);
                minAngleToZone = Mathf.Min(angleToZone, 180f - angleToZone);

                // 리드 결정(2026-08-27): 톱도 대패와 같이 방향 무관. 각도는 품질 점수용으로만 남긴다.
                validCut = true;
            }
        }

        if (validCut)
        {
            isCutting = true;
            currentStrokeDistance += distance;
            cutGraceTimer = 0.2f;
            
            strokeSpeedAccum += currentSpeed;
            strokeAngleAccum += minAngleToZone;
            frameCount++;
            
            // 너무 짧은 거리에선 햅틱/이펙트를 무시하여 잔떨림 방지
            if (currentStrokeDistance > 0.02f)
            {
                PlayFeedback();
            }
        }
        else 
        {
            if (isCutting)
            {
                cutGraceTimer -= Time.deltaTime;
                if (cutGraceTimer <= 0f)
                {
                    FinishStroke(); // 여기서 햅틱/파티클이 정지됨
                }
            }
            else
            {
                StopFeedback();
            }
        }

        lastPosition = currentPos;
    }

    private void OnTriggerExit(Collider other)
    {
        var zone = other.GetComponent<SawZone>();
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
                float avgSpeed = frameCount > 0 ? strokeSpeedAccum / frameCount : 0f;
                float avgAngle = frameCount > 0 ? strokeAngleAccum / frameCount : 0f;
                currentZone.AddStrokeProgress(avgSpeed, avgAngle);
            }
            else
            {
                Debug.Log($"[SawTool] Stroke failed - distance too short: {currentStrokeDistance:F2}m");
            }
        }
        
        isCutting = false;
        currentStrokeDistance = 0f;
        strokeSpeedAccum = 0f;
        strokeAngleAccum = 0f;
        frameCount = 0;
        StopFeedback();
    }

    // ── Feedback ───────────────────────────────────────────────────────────
    private void PlayFeedback()
    {
        float progress = currentZone != null ? currentZone.progress : 0f;

        if (sawdustParticles != null)
        {
            if (!sawdustParticles.isPlaying) sawdustParticles.Play();
            var emission = sawdustParticles.emission;
            emission.rateOverTimeMultiplier = Mathf.Lerp(10f, 60f, progress);
        }

        if (customGrabbable != null && customGrabbable.IsHeld)
        {
            float currentHaptic = Mathf.Lerp(hapticIntensity * 0.5f, hapticIntensity * 1.5f, progress);
            UserInput.Instance.SendHapticImpulse(customGrabbable.Holder.Hand, currentHaptic, hapticDuration);
        }

        if (audioSource != null)
        {
            if (!audioSource.isPlaying) audioSource.Play();
            audioSource.volume = Mathf.Lerp(0.4f, 1.0f, progress);
            audioSource.pitch = Mathf.Lerp(0.85f, 1.15f, progress);
        }
    }

    private void StopFeedback()
    {
        if (sawdustParticles != null && sawdustParticles.isPlaying)
            sawdustParticles.Stop();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }
}
