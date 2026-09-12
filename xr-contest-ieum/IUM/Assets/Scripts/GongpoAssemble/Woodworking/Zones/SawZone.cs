using UnityEngine;

public class SawZone : WorkZone
{
    [Header("톱질 설정")]
    [Tooltip("톱질 1회(스트로크)로 인정되기 위한 최소 이동 거리 (미터)")]
    public float minStrokeDistance = 0.2f;
    
    [Tooltip("작업을 완료하기 위해 필요한 총 톱질 횟수")]
    public int requiredStrokes = 5;
    
    [Tooltip("톱질이 허용되는 정방향 (로컬 축 기준)")]
    public Vector3 sawDirection = Vector3.forward;

    [Header("톱질 완료 후 처리")]
    [Tooltip("이 구역의 톱질이 완료되면 떨어져 나갈 Waste 오브젝트들")]
    public GameObject[] wasteObjects;

    [Tooltip("물리적으로 떨어진 뒤 완전히 사라지기까지의 시간 (초)")]
    public float wasteDisappearDelay = 3.0f;

    [Header("시각적 피드백 (Gap)")]
    [Tooltip("톱질 진행에 따라 조각이 서서히 벌어지는 최대 거리 (미터)")]
    public float maxVisualGap = 0.002f; // 2mm
    [Tooltip("벌어지는 월드 공간 기준 방향 (기본값: 중력 방향인 아래로 처짐)")]
    public Vector3 gapDirection = Vector3.down;

    private float totalQuality = 0f;
    private int strokeCount = 0;
    
    private System.Collections.Generic.Dictionary<GameObject, Vector3> initialWastePositions = new System.Collections.Generic.Dictionary<GameObject, Vector3>();

    protected override void Start()
    {
        base.Start();
        requiredToolType = ToolType.Saw;

        // Waste 파트들의 원래 위치 저장
        if (wasteObjects != null)
        {
            foreach (var obj in wasteObjects)
            {
                if (obj != null)
                {
                    initialWastePositions[obj] = obj.transform.position;
                }
            }
        }
    }

    public bool IsStrokeSuccessful(float distance)
    {
        return distance >= minStrokeDistance;
    }

    public void AddStrokeProgress(float speed, float angleTolerance)
    {
        // 톱질 품질 계산 (임시 로직: 속도가 빠를수록, 각도 오차가 적을수록 고득점)
        float speedScore = Mathf.Clamp01(speed / 1.5f); // 1.5m/s 이상이면 만점
        float angleScore = Mathf.Clamp01(1.0f - (angleTolerance / 30f)); // 30도 이상 벗어나면 0점
        
        float strokeQuality = (speedScore * 0.4f) + (angleScore * 0.6f);
        
        totalQuality += strokeQuality;
        strokeCount++;

        float progressAmount = 1.0f / requiredStrokes;
        AddProgress(progressAmount);
        Debug.Log($"[SawZone] 🪚 톱질 스트로크 성공! 현재 진행도: {progress * 100f:F1}% (품질: {strokeQuality:F2})");
    }

    protected override void OnProgressUpdated(float currentProgress)
    {
        base.OnProgressUpdated(currentProgress);

        if (wasteObjects != null)
        {
            foreach (var obj in wasteObjects)
            {
                if (obj != null && initialWastePositions.TryGetValue(obj, out Vector3 initialPos))
                {
                    // 진행도에 따라 미세하게 틈을 벌림 (시각적 피드백)
                    obj.transform.position = initialPos + (gapDirection.normalized * (maxVisualGap * currentProgress));
                }
            }
        }
    }

    protected override void CompleteWork()
    {
        // 1. 해당 톱질 구역(SawZone)에 직접 할당된 Waste 파트가 있다면 비활성화
        bool hasLocalWaste = false;
        if (wasteObjects != null && wasteObjects.Length > 0)
        {
            foreach (var obj in wasteObjects)
            {
                if (obj != null)
                {
                    StartCoroutine(DropAndDisableRoutine(obj));
                    hasLocalWaste = true;
                }
            }
        }

        // 2. (하위 호환성) 만약 직접 할당된 파트가 없고, 기존처럼 VisualWoodModifier에 할당되어 있다면 그걸 끕니다.
        if (!hasLocalWaste && woodModifier != null)
        {
            woodModifier.HideWaste();
        }

        float finalQuality = strokeCount > 0 ? (totalQuality / strokeCount) : 0f;
        
        // 부모의 오버로딩된 CompleteWork(WorkResult) 호출
        base.CompleteWork(new WorkResult { qualityScore = finalQuality, toolName = requiredToolType.ToString() });
    }

    private System.Collections.IEnumerator DropAndDisableRoutine(GameObject obj)
    {
        // 1. 부모 오브젝트와 분리
        obj.transform.SetParent(null);

        // 2. 물리(Rigidbody)가 있다면 중력 적용시켜서 떨어지게 함
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // 3. 지정된 시간 대기
        yield return new WaitForSeconds(wasteDisappearDelay);

        // 4. 비활성화 (메모리 최적화 등을 위해)
        if (obj != null)
        {
            obj.SetActive(false);
        }
    }
}
