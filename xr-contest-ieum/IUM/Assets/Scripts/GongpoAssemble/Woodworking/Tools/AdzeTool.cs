using UnityEngine;

public class AdzeTool : MonoBehaviour, IWoodTool
{
    public ToolType GetToolType() => ToolType.Adze;
    public bool IsActive() => true;

    [Header("Adze Settings")]
    [Tooltip("유효한 타격으로 인정될 최소 속도 (m/s)")]
    public float minHitSpeed = 1.0f;
    
    [Tooltip("타격 쿨타임 (초)")]
    public float hitCooldown = 0.5f;

    // ×20 월드 스케일 보정: 기존 하드코딩 0.05f는 인체감 3.5mm라 스윙 스윕이 존을 놓쳤다.
    // 1.5유닛 = 인체감 약 10cm (자귀 날 폭 수준).
    [Tooltip("스윙 스윕(터널링 방지) 판정 반경. ×20 월드 기준 1.5유닛 = 인체감 약 10cm.")]
    [SerializeField] private float tipCheckRadius = 1.5f;

    [Header("Feedback")]
    public ParticleSystem woodShavingParticles;
    [Range(0f, 1f)] public float hapticIntensity = 0.5f;
    public float hapticDuration = 0.1f;
    [Tooltip("자귀질 타격 시 재생할 소리")]
    public AudioClip hitSound;

    private Vector3 lastPosition;
    private float currentSpeed;
    private float lastHitTime = 0f;
    private Grabbable customGrabbable;
    private AudioSource audioSource;

    private void Start()
    {
        lastPosition = transform.position;
        customGrabbable = GetComponent<Grabbable>();
        
        if (hitSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = hitSound;
            audioSource.spatialBlend = 1.0f; // 3D Sound
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            // 기본 감쇠(min 1 / max 500 / Logarithmic)는 미터 전제라 ×12 씬에서 8cm부터 줄어든다.
            SceneWorldScale.Configure3D(audioSource);
        }
    }

    private void FixedUpdate()
    {
        // 프레임 간 이동 거리를 통해 현재 속력 계산
        Vector3 delta = transform.position - lastPosition;
        float distance = delta.magnitude;
        currentSpeed = distance / Time.fixedDeltaTime;

        // 터널링 방지 (빠른 스윙 시 콜라이더 통과 문제 해결)
        if (distance > 0.01f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(lastPosition, tipCheckRadius, delta.normalized, distance, Physics.AllLayers, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                var zone = hit.collider.GetComponent<AdzeZone>();
                if (zone != null)
                {
                    ProcessHit(zone);
                }
            }
        }

        lastPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        var zone = other.GetComponent<AdzeZone>();
        if (zone != null)
        {
            ProcessHit(zone);
        }
    }

    private void ProcessHit(AdzeZone zone)
    {
        if (Time.time - lastHitTime < hitCooldown) return;
        if (zone.IsCompleted) return;

        if (currentSpeed >= minHitSpeed)
        {
            // 유효 타격
            zone.AddHitProgress();
            lastHitTime = Time.time;
            PlayFeedback();
        }
        else
        {
            Debug.Log($"[AdzeTool] 타격 속도 부족: {currentSpeed:F2}m/s (필요 속도: {minHitSpeed:F2}m/s)");
        }
    }

    private void PlayFeedback()
    {
        if (woodShavingParticles != null)
        {
            woodShavingParticles.Emit(5);
        }

        if (customGrabbable != null && customGrabbable.IsHeld)
        {
            UserInput.Instance.SendHapticImpulse(customGrabbable.Holder.Hand, hapticIntensity, hapticDuration);
        }
            
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }
}
