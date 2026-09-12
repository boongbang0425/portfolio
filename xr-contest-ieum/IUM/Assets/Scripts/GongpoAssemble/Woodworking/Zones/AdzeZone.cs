using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class AdzeZone : WorkZone
{
    [Header("자귀질 설정")]
    [Tooltip("작업 완료에 필요한 유효 타격 횟수")]
    public int requiredHits = 3;
    
    [Tooltip("수정할 BlendShape 이름")]
    public string blendShapeName = "Adzed";

    private int currentHits = 0;

    protected override void Start()
    {
        base.Start();
        requiredToolType = ToolType.Adze;
    }

    public void AddHitProgress()
    {
        if (isCompleted) return;

        currentHits++;
        float progressAmount = 1.0f / requiredHits;
        AddProgress(progressAmount);
        
        Debug.Log($"[AdzeZone] 🪓 유효 타격! 누적 횟수: {currentHits}/{requiredHits}");
    }

    protected override void OnProgressUpdated(float currentProgress)
    {
        if (woodModifier != null && !string.IsNullOrEmpty(blendShapeName))
        {
            woodModifier.ApplyBlendShape(blendShapeName, currentProgress * 100f);
        }
    }
}
