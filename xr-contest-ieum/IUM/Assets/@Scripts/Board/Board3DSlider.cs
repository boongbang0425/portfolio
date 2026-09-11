using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 보드 3D 슬라이더. 기존 색 전이·10단계 스냅을 유지하면서 <see cref="IWorldMenuTarget"/>을 구현해
/// <see cref="WorldMenuPointer"/>(VR 레이 + 데스크톱 마우스)로 잡고 끌 수 있다.
///
/// 이동 구간은 직렬화된 travelDistance보다 트랙·손잡이 콜라이더 실측을 우선한다. 프리팹의
/// travelDistance(0.02175, 월드 약 0.0935m)는 트랙 유효폭(실측 약 0.085m)보다 길어 손잡이가
/// 트랙 끝을 넘는데, 두 콜라이더에서 유효 구간을 계산하면 프리팹 값을 고치지 않아도 맞는다.
/// 콜라이더가 없으면 종전 travelDistance 경로로 동작한다.
/// </summary>
public class Board3DSlider : MonoBehaviour, IWorldMenuTarget
{
	[Header("구성")]
	[SerializeField] Transform handle;
	[SerializeField] Renderer handleRenderer;

	[Header("이동")]
	[SerializeField] float travelDistance = 0.02175f;

	[Header("값")]
	[SerializeField, Range(0f, 1f)] float value = 0.8f;
	[SerializeField] int steps = 10;

	[Header("색")]
	[SerializeField] Color normalColor = new Color(0.85f, 0.82f, 0.75f);
	[SerializeField] Color hoveredColor = new Color(1f, 0.97f, 0.90f);
	[SerializeField] Color grabbedColor = new Color(0.49f, 0.78f, 1f);

	[Header("햅틱")]
	[SerializeField, Range(0f, 1f)] float grabHaptic = 0.25f;
	[SerializeField, Range(0f, 0.3f)] float grabHapticSeconds = 0.05f;

	public Action<float> onValueChanged;

	Material mat;
	bool isGrabbed;

	Vector3 handleBaseLocal;
	bool captured;

	// ---- 실측 이동 구간 (이 컴포넌트의 로컬 공간, 이동축은 X) ----
	// value 0일 때 손잡이 중심의 x가 value0X, value 1일 때 value1X다. 원본과 같이 -X로 증가한다.
	float value0X;
	float value1X;
	Vector3 handleCentreBase;
	bool geometryReady;

	static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

	public bool IsInteractable => isActiveAndEnabled && handle != null;

	void OnEnable()
	{
		if (handleRenderer != null && mat == null)
			mat = handleRenderer.materials[0];

		StartCoroutine(InitNextFrame());
	}

	void OnDisable()
	{
		// 잡힌 채 판이 꺼지면(보드 전환·메뉴 닫힘) 잡기 상태만 푼다. 값은 유지된다.
		isGrabbed = false;
	}

	IEnumerator InitNextFrame()
	{
		yield return null;
		Capture();
		ApplyVisual();
	}

	void Capture()
	{
		if (captured) return;
		if (handle != null) handleBaseLocal = handle.localPosition;
		captured = true;
		MeasureGeometry();
	}

	/// <summary>
	/// 트랙·손잡이 콜라이더로 유효 이동 구간을 잰다. 반드시 손잡이가 기준 자세(handleBaseLocal)일 때
	/// 불러야 한다 — 손잡이 콜라이더 중심을 기준 좌표로 삼기 때문이다.
	/// </summary>
	void MeasureGeometry()
	{
		if (geometryReady) return;

		BoxCollider handleCollider = handle != null ? handle.GetComponent<BoxCollider>() : null;
		BoxCollider trackCollider = FindTrackCollider();

		if (handleCollider != null)
			handleCentreBase = transform.InverseTransformPoint(
				handleCollider.transform.TransformPoint(handleCollider.center));
		else if (handle != null)
			handleCentreBase = handleBaseLocal;

		if (handleCollider != null && trackCollider != null)
		{
			// 콜라이더 로컬 → 이 컴포넌트 로컬로 환산한다. 트랙·손잡이가 X축 기준으로 기울어 있어도
			// (프리팹은 5.7도) x 성분만 취하면 이동축과 일치한다.
			Vector3 trackCentre = transform.InverseTransformPoint(
				trackCollider.transform.TransformPoint(trackCollider.center));
			float trackHalf = Mathf.Abs(transform.InverseTransformVector(
				trackCollider.transform.TransformVector(new Vector3(trackCollider.size.x * 0.5f, 0f, 0f))).x);
			float handleHalf = Mathf.Abs(transform.InverseTransformVector(
				handleCollider.transform.TransformVector(new Vector3(handleCollider.size.x * 0.5f, 0f, 0f))).x);

			// 원본 규약대로 value 0이 +X 끝, value 1이 -X 끝이다.
			value0X = trackCentre.x + trackHalf - handleHalf;
			value1X = trackCentre.x - trackHalf + handleHalf;
		}
		else
		{
			// 실측 실패 시 종전 travelDistance 경로. 기준 자세가 value 0이다.
			value0X = handleCentreBase.x;
			value1X = value0X - travelDistance;
		}

		geometryReady = true;
	}

	/// <summary>손잡이 하위가 아닌 자식 BoxCollider를 트랙으로 본다.</summary>
	BoxCollider FindTrackCollider()
	{
		foreach (var candidate in GetComponentsInChildren<BoxCollider>(true))
		{
			if (handle != null && candidate.transform.IsChildOf(handle)) continue;
			return candidate;
		}

		return null;
	}

	void ApplyVisual()
	{
		if (!captured || handle == null || !geometryReady) return;
		float centreX = Mathf.Lerp(value0X, value1X, value);
		handle.localPosition = handleBaseLocal + new Vector3(centreX - handleCentreBase.x, 0f, 0f);
	}

	// ---------- 값 ----------

	public void SetValue(float v)
	{
		value = Mathf.Clamp01(v);
		ApplyVisual();
	}

	public float GetValue() => value;

	public void SetValueFromDrag(float t)
	{
		t = Mathf.Clamp01(t);
		if (steps > 0) t = Mathf.Round(t * steps) / steps;
		if (Mathf.Approximately(t, value)) return;

		value = t;
		ApplyVisual();
		onValueChanged?.Invoke(value);
	}

	// ---------- 드래그용 좌표 ----------

	Vector3 TravelWorld()
	{
		if (handle == null || handle.parent == null) return Vector3.zero;
		return handle.parent.TransformVector(new Vector3(-travelDistance, 0f, 0f));
	}

	public Vector3 GetMinPos()
	{
		if (handleRenderer == null) return Vector3.zero;
		return handleRenderer.bounds.center - TravelWorld() * value;
	}

	public Vector3 GetMaxPos()
	{
		return GetMinPos() + TravelWorld();
	}

	// ---------- 상태 ----------

	void SetColor(Color c)
	{
		if (mat != null) mat.SetColor(BaseColorID, c);
	}

	public void OnHoverEnter() { if (!isGrabbed) SetColor(hoveredColor); }
	public void OnHoverExit() { if (!isGrabbed) SetColor(normalColor); }

	public void OnGrabStart()
	{
		isGrabbed = true;
		SetColor(grabbedColor);
	}

	public void OnGrabEnd()
	{
		isGrabbed = false;
		SetColor(normalColor);
	}

	// ---- IWorldMenuTarget (WorldMenuPointer 경로) ----

	public void OnPointerEnter(WorldMenuPointer pointer) => OnHoverEnter();

	public void OnPointerExit(WorldMenuPointer pointer) => OnHoverExit();

	public void OnPointerDown(WorldMenuPointer pointer, RaycastHit hit)
	{
		if (!IsInteractable) return;

		// 활성 후 첫 프레임에 잡으면 코루틴 초기화가 아직이라 여기서 보장한다.
		// 이 시점에는 아직 드래그로 움직인 적이 없어 기준 자세 그대로다.
		Capture();
		if (!geometryReady) return;

		OnGrabStart();
		pointer?.Pulse(grabHaptic, grabHapticSeconds);

		// 트랙 위 히트 지점으로 즉시 점프한 뒤 드래그로 이어진다.
		float x = transform.InverseTransformPoint(hit.point).x;
		SetValueFromDrag(Mathf.InverseLerp(value0X, value1X, x));
	}

	public void OnPointerDrag(WorldMenuPointer pointer)
	{
		if (!isGrabbed || pointer == null || !geometryReady) return;

		// 콜라이더가 아니라 이동선 기준으로 추적한다. 드래그를 시작하면 레이가 트랙을 벗어나도
		// 손잡이를 놓치지 않는다.
		Vector3 p0 = handleCentreBase; p0.x = value0X;
		Vector3 p1 = handleCentreBase; p1.x = value1X;

		Vector3 origin = transform.TransformPoint(p0);
		Vector3 direction = transform.TransformPoint(p1) - origin;

		SetValueFromDrag(WorldMenuGeometry.ClosestPointOnLine(pointer.CurrentRay, origin, direction));
	}

	public void OnPointerUp(WorldMenuPointer pointer, bool onTarget) => OnGrabEnd();
}
