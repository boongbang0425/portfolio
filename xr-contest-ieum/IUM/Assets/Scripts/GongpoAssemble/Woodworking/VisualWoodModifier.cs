using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// WorkZone에서 발생하는 이벤트(먹줄, 대패질, 톱질 등)를 받아
/// 실제 Mesh 교체나 BlendShape, 데칼 등을 처리하는 클래스
/// </summary>
public class VisualWoodModifier : MonoBehaviour
{
    [Header("시각적 에셋 세팅")]
    [Tooltip("메쉬 렌더러가 포함된 원본 모델")]
    public SkinnedMeshRenderer targetSkinnedMesh;
    public MeshRenderer targetMeshRenderer;

    [Tooltip("톱질 완료 시 사라질 Waste 오브젝트")]
    public GameObject wasteObject;

    [Tooltip("추가로 사라질 Waste 오브젝트들 (2개 이상일 경우 사용)")]
    public GameObject[] additionalWasteObjects;

    [Header("먹줄 데칼 설정")]
    public GameObject inkLineDecalPrefab;

    public void ApplyBlendShape(string blendShapeName, float weight)
    {
        if (targetSkinnedMesh != null)
        {
            int index = targetSkinnedMesh.sharedMesh.GetBlendShapeIndex(blendShapeName);
            if (index != -1)
            {
                targetSkinnedMesh.SetBlendShapeWeight(index, weight);
            }
            else
            {
                Debug.LogError($"[VisualWoodModifier] 에러! '{blendShapeName}'라는 이름의 BlendShape를 SkinnedMesh에서 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogError("[VisualWoodModifier] 에러! Target Skinned Mesh가 할당되지 않았습니다!");
        }
    }

    public Vector3 GetClosestSurfacePoint(Vector3 point)
    {
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        Vector3 closestPoint = point;
        float minDistance = float.MaxValue;

        foreach (var col in allColliders)
        {
            // 부모(자신)의 콜라이더나 Trigger는 실제 충돌 표면이 아니므로 제외합니다.
            if (col.isTrigger || col.gameObject == gameObject) continue; 

            Vector3 cp = col.ClosestPoint(point);
            float dist = Vector3.Distance(point, cp);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestPoint = cp;
            }
        }

        return minDistance < float.MaxValue ? closestPoint : point;
    }

    public void SwapMesh(Mesh newMesh)
    {
        if (targetMeshRenderer != null && targetMeshRenderer.GetComponent<MeshFilter>() != null)
        {
            targetMeshRenderer.GetComponent<MeshFilter>().mesh = newMesh;
        }
        else if (targetSkinnedMesh != null)
        {
            targetSkinnedMesh.sharedMesh = newMesh;
        }
    }

    public void CreateInkLine(Vector3 startPoint, Vector3 endPoint)
    {
        if (inkLineDecalPrefab == null)
        {
            Debug.LogError("[VisualWoodModifier] 먹줄 프리팹(Ink Line Decal Prefab) 누락!");
            return;
        }

        // 1. 유효한 모든 자식 콜라이더 수집 (Trigger 제외)
        Collider[] allCols = GetComponentsInChildren<Collider>();
        List<Collider> validCols = new List<Collider>();
        foreach (var col in allCols)
        {
            // 원래는 자식(실제 모델)만 수집하기 위해 gameObject(부모)를 제외합니다.
            if (col.gameObject != gameObject && !col.isTrigger)
            {
                validCols.Add(col);
            }
        }

        // 만약 자식 콜라이더가 단 하나도 없다면 (단일 오브젝트 구조인 경우)
        // 자기 자신의 콜라이더를 폴백(Fallback)으로 사용합니다.
        if (validCols.Count == 0)
        {
            Collider myCol = GetComponent<Collider>();
            if (myCol != null && !myCol.isTrigger)
            {
                validCols.Add(myCol);
            }
        }

        if (validCols.Count == 0)
        {
            Debug.LogWarning("[VisualWoodModifier] 먹선을 그릴 유효한 콜라이더(표면)가 없습니다. Collider가 부착되어 있는지 확인해주세요.");
            return;
        }

        // 2. 점들을 가장 가까운 콜라이더 기준으로 연속된 세그먼트로 분할
        int segments = 100;
        Vector3 step = (endPoint - startPoint) / segments;
        
        List<List<Vector3>> segmentPoints = new List<List<Vector3>>();
        List<Collider> segmentTargets = new List<Collider>();

        Collider lastClosestCol = null;
        List<Vector3> currentPoints = null;

        for (int i = 0; i <= segments; i++)
        {
            Vector3 pointOnLine = startPoint + (step * i);
            Collider closestCol = null;
            float minDistance = float.MaxValue;

            // 현재 점과 가장 가까운 콜라이더 찾기
            foreach (var col in validCols)
            {
                float dist = Vector3.Distance(pointOnLine, col.ClosestPoint(pointOnLine));
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestCol = col;
                }
            }

            if (closestCol != null)
            {
                // 소속 콜라이더가 변경되었을 때 새로운 세그먼트 생성
                if (closestCol != lastClosestCol)
                {
                    if (currentPoints != null && currentPoints.Count > 0)
                    {
                        // 끊김 방지를 위해 이전 세그먼트에 현재 점 추가
                        currentPoints.Add(pointOnLine);
                    }

                    currentPoints = new List<Vector3>();
                    segmentPoints.Add(currentPoints);
                    segmentTargets.Add(closestCol);

                    if (i > 0)
                    {
                        // 끊김 방지를 위해 새 세그먼트에 이전 점 추가
                        currentPoints.Add(startPoint + (step * (i - 1)));
                    }
                }
                
                currentPoints.Add(pointOnLine);
                lastClosestCol = closestCol;
            }
        }

        // 3. 분할된 세그먼트별로 데칼 생성 및 각각의 타겟(Collider)에 종속
        bool isLineRenderer = inkLineDecalPrefab.GetComponent<LineRenderer>() != null;
        for (int i = 0; i < segmentPoints.Count; i++)
        {
            if (isLineRenderer)
            {
                CreateLineRendererDecal(segmentPoints[i], segmentTargets[i].transform);
            }
            else
            {
                CreateQuadDecal(segmentPoints[i], segmentTargets[i].transform);
            }
        }
    }

    private void CreateLineRendererDecal(List<Vector3> points, Transform parentTarget)
    {
        if (points.Count == 0 || parentTarget == null) return;

        GameObject lineObj = Instantiate(inkLineDecalPrefab, parentTarget);
        LineRenderer lr = lineObj.GetComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lineObj.transform.localPosition = Vector3.zero;
        lineObj.transform.localRotation = Quaternion.identity;

        lr.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            lr.SetPosition(i, parentTarget.InverseTransformPoint(points[i]));
        }
    }

    private void CreateQuadDecal(List<Vector3> points, Transform parentTarget)
    {
        if (points.Count <= 1 || parentTarget == null) return;

        Vector3 start = points[0];
        Vector3 end = points[points.Count - 1];
        
        GameObject lineObj = Instantiate(inkLineDecalPrefab);
        lineObj.transform.position = (start + end) / 2f;
        
        if (end != start) 
            lineObj.transform.rotation = Quaternion.LookRotation(end - start, transform.up);
            
        float distance = Vector3.Distance(start, end);
        lineObj.transform.localScale = new Vector3(0.01f, 0.01f, distance);
        lineObj.transform.SetParent(parentTarget, true);
     }

    /// <summary>
    /// 톱질 완료 시 Waste(버려지는 부분)를 보이지 않게 처리
    /// </summary>
    public void HideWaste()
    {
        bool hidAnything = false;

        if (wasteObject != null)
        {
            wasteObject.SetActive(false);
            hidAnything = true;
        }

        if (additionalWasteObjects != null)
        {
            foreach (var obj in additionalWasteObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    hidAnything = true;
                }
            }
        }

        if (hidAnything)
        {
            Debug.Log("[VisualWoodModifier] Waste 부분을 성공적으로 숨겼습니다.");
        }
    }
}
