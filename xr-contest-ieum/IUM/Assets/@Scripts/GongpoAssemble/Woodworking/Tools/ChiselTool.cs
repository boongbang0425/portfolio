using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ChiselTool : MonoBehaviour, IWoodTool
{
    public ToolType GetToolType() => ToolType.Chisel;
    public bool IsActive() => true;

    [Header("Chisel Settings")]
    [Tooltip("끌의 끝부분 Transform (이 위치가 ChiselZone 안에 있어야 진행도가 전달됩니다)")]
    public Transform chiselTip;
    
    // ×20 월드 스케일 보정: Play 씬은 1유닛이 인체감 약 7cm라 기존 0.02f는 인체감 1.4mm였다.
    // 2유닛 = 인체감 약 14cm. (씬에 이미 직렬화된 값이 있으면 인스펙터 값이 우선한다)
    [Tooltip("끌 끝 위치를 검사할 반경. ×20 월드 기준 2유닛 = 인체감 약 14cm.")]
    public float tipCheckRadius = 2f;

    [Tooltip("기본 타격 시 전달되는 깊이")]
    public float baseHitDepth = 0.5f;

    [Tooltip("한 번 타격 시 깎일 수 있는 최대 깊이 (너무 세게 쳐도 이 값까지만 깎임)")]
    public float maxHitDepth = 1.5f;

    [Tooltip("타격 쿨타임 (초) - 다중 충돌로 인한 중복 타격 방지")]
    public float hitCooldown = 0.5f;

    [Header("Feedback")]
    public ParticleSystem woodShavingParticles;
    [Range(0f, 1f)] public float hapticIntensity = 0.5f;
    public float hapticDuration = 0.1f;
    [Tooltip("끌을 망치로 쳤을 때 재생할 소리")]
    public AudioClip hitSound;

    private float lastHitTime = 0f;
    private Grabbable customGrabbable;

    private void Start()
    {
        customGrabbable = GetComponent<Grabbable>();
    }

    // 일반 충돌 시
    private void OnCollisionEnter(Collision collision)
    {
        HammerTool hammer = collision.gameObject.GetComponentInParent<HammerTool>();
        if (hammer != null)
        {
            ProcessHit(hammer);
        }
    }

    // 트리거 충돌 시 (VR 환경에서는 트리거를 사용할 수도 있으므로 대비)
    private void OnTriggerEnter(Collider other)
    {
        HammerTool hammer = other.GetComponentInParent<HammerTool>();
        if (hammer != null)
        {
            ProcessHit(hammer);
        }
    }

    public void ProcessHit(HammerTool hammer)
    {
        // 쿨타임 체크 (너무 짧은 시간에 여러 번 충돌 이벤트가 발생하는 것 방지)
        if (Time.time - lastHitTime < hitCooldown) return;

        float speed = hammer.GetCurrentSpeed();
        
        // 망치의 속도가 일정 이상인지 확인
        if (speed >= hammer.minHitSpeed)
        {
            if (chiselTip == null)
            {
                Debug.LogWarning("[ChiselTool] chiselTip이 할당되지 않았습니다!");
                return;
            }

            // 끌 끝이 ChiselZone 내부에 있는지 확인 (OverlapSphere 활용)
            // 반경을 스케일에 맞춰 키운 만큼 여러 존이 동시에 걸릴 수 있으므로, 최근접 미완료 존 하나만 고른다.
            ChiselZone targetZone = null;
            float minSqrDistance = float.MaxValue;

            Collider[] colliders = Physics.OverlapSphere(chiselTip.position, tipCheckRadius);
            foreach (var col in colliders)
            {
                ChiselZone zone = col.GetComponent<ChiselZone>();
                if (zone == null || zone.IsCompleted) continue;

                float sqrDistance = (col.ClosestPoint(chiselTip.position) - chiselTip.position).sqrMagnitude;
                if (sqrDistance < minSqrDistance)
                {
                    minSqrDistance = sqrDistance;
                    targetZone     = zone;
                }
            }

            if (targetZone != null)
            {
                // 망치 속도에 비례해서 깊이를 전달하되, 최대치(maxHitDepth)로 제한
                float depth = baseHitDepth * (speed / hammer.minHitSpeed);
                depth = Mathf.Min(depth, maxHitDepth);

                targetZone.AddHitProgress(depth);
                lastHitTime = Time.time; // 타격 시간 기록

                // 피드백 재생
                if (woodShavingParticles != null) woodShavingParticles.Emit(5);
                // AudioSource.PlayClipAtPoint는 min 1 / max 500 / Logarithmic을 박아 두므로 ×12
                // 씬에서 타격음이 두 걸음만 떨어져도 사라진다. 씬 배율을 반영하는 경로로 바꾼다.
                if (hitSound != null) SceneWorldScale.PlayClipAtPoint(hitSound, chiselTip.position);

                // 햅틱은 여기서 끝난 타격 판정에 영향을 주면 안 된다. UserInput 싱글턴이 아직 없는
                // 씬(데스크톱 단독 테스트 등)에서 NRE가 나면 아래 로그까지 통째로 사라져 "타격이
                // 먹히지 않는다"로 보인다. 인스턴스 유무를 먼저 확인한다.
                UserInput input = UserInput.Instance;
                if (input != null)
                {
                    // 끌을 잡고 있는 손에 햅틱
                    if (customGrabbable != null && customGrabbable.IsHeld)
                    {
                        input.SendHapticImpulse(customGrabbable.Holder.Hand, hapticIntensity, hapticDuration);
                    }

                    // 망치를 잡고 있는 손에도 약한 햅틱 전달 (타격감 향상)
                    if (hammer != null)
                    {
                        Grabbable hammerGrab = hammer.GetComponent<Grabbable>();
                        if (hammerGrab != null && hammerGrab.IsHeld)
                        {
                            input.SendHapticImpulse(hammerGrab.Holder.Hand, hapticIntensity * 0.8f, hapticDuration);
                        }
                    }
                }

                Debug.Log($"[ChiselTool] 🔨 타격 성공! 속도: {speed:F2}m/s, 전달된 깊이: {depth:F2}");
            }
            else
            {
                Debug.Log($"[ChiselTool] 망치 타격은 감지되었으나, 끌 끝(chiselTip)이 유효한 ChiselZone 내부에 없습니다! (반경: {tipCheckRadius}m)");
            }
        }
        else
        {
            Debug.Log($"[ChiselTool] 망치 타격 속도 부족: {speed:F2}m/s (필요 속도: {hammer.minHitSpeed:F2}m/s)");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (chiselTip != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(chiselTip.position, tipCheckRadius);
        }
    }
}
