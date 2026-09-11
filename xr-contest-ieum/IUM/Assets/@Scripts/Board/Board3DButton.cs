using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 보드 3D 버튼. 색·발광 전이에 더해 <see cref="IWorldMenuTarget"/>을 구현해
/// <see cref="WorldMenuPointer"/>(VR 레이 + 데스크톱 마우스)로 조작할 수 있다.
///
/// 기존 공개 메서드(OnHoverEnter/Exit, OnSelectEnter/Exit)는 그대로 유지한다. 단 포인터 경로에서는
/// OnSelectExit의 무조건 onClick 발화를 우회하고, 같은 버튼 위에서 떼었을 때만 클릭으로 인정한다.
///
/// 눌림 모션은 WorldMenuButton과 같은 방식이다: 눌림 깊이는 판 두께에 대한 비율이라 루트가
/// 런타임에 균일 스케일돼도 비례가 유지되고, 보간은 unscaledDeltaTime을 쓴다 — 메뉴는
/// timeScale 0에서만 존재하기 때문이다.
/// </summary>
public class Board3DButton : MonoBehaviour, IWorldMenuTarget
{
	public enum State { Disabled, Normal, Hovered, Pressed }

	[Header("대상")]
	[SerializeField] Renderer targetRenderer;
	[SerializeField] Transform glyphParent;

	[Header("색")]
	[SerializeField] Color normalColor = new Color(0.85f, 0.82f, 0.75f);
	[SerializeField] Color hoveredColor = new Color(1.00f, 0.97f, 0.90f);
	[SerializeField] Color pressedColor = new Color(0.49f, 0.78f, 1.00f);
	[SerializeField] Color disabledColor = new Color(0.45f, 0.43f, 0.40f);

	[Header("발광")]
	[SerializeField] float hoverEmission = 0.35f;
	[SerializeField] float pressEmission = 1.2f;
	[SerializeField] float glyphMultiplier = 2f;

	[Header("모션")]
	[SerializeField] float speed = 12f;

	[Tooltip("눌렸을 때 판이 들어가는 깊이. 판 두께에 대한 비율이다.")]
	[SerializeField, Range(0.05f, 1f)] float pressDepthRatio = 0.55f;

	[Tooltip("눌림·복귀 위치 보간 속도.")]
	[SerializeField, Min(0.1f)] float motionSpeed = 18f;

	[Header("햅틱")]
	[SerializeField, Range(0f, 1f)] float pressHaptic = 0.35f;
	[SerializeField, Range(0f, 0.3f)] float pressHapticSeconds = 0.06f;

	[Header("이벤트")]
	public UnityEvent onClick;

	[Header("상태")]
	[SerializeField] bool interactable = true;

	Material mat;
	Material[] glyphMats;
	State state = State.Normal;

	Color targetColor;
	float targetEmission;

	// ---- 눌림 모션 ----
	Vector3 restLocalPosition;
	Vector3 pressedLocalPosition;
	bool pressConfigured;
	bool pointerPressed;

	static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
	static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

	public bool IsInteractable => interactable && isActiveAndEnabled;

	void Awake()
	{
		// Configure가 먼저 불렸으면 덮어쓰지 않는다. 비활성 상태로 배선되는 프리팹에서는
		// 소유 메뉴의 Configure가 이 컴포넌트의 Awake보다 앞설 수 있다.
		if (!pressConfigured)
		{
			restLocalPosition = transform.localPosition;
			pressedLocalPosition = restLocalPosition;
		}

		if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();

		if (targetRenderer != null)
		{
			mat = targetRenderer.materials[0];
			mat.EnableKeyword("_EMISSION");
		}

		if (glyphParent != null)
		{
			var rs = glyphParent.GetComponentsInChildren<Renderer>();
			glyphMats = new Material[rs.Length];
			for (int i = 0; i < rs.Length; i++)
			{
				glyphMats[i] = rs[i].materials[0];
				glyphMats[i].EnableKeyword("_EMISSION");
			}
		}

		SetState(interactable ? State.Normal : State.Disabled, true);
	}

	void OnDisable()
	{
		// 눌리거나 호버된 채 판이 꺼지면(보드 전환·메뉴 닫힘) 원위치·기본색으로 스냅한다.
		pointerPressed = false;
		transform.localPosition = restLocalPosition;
		SetState(interactable ? State.Normal : State.Disabled, true);
	}

	void OnDestroy()
	{
		// Awake의 renderer.materials[0]는 머티리얼 인스턴스를 만든다. 파괴하지 않으면 버튼이
		// 생겼다 사라질 때마다 인스턴스가 누적된다.
		if (mat != null) Destroy(mat);
		if (glyphMats != null)
			foreach (var gm in glyphMats)
				if (gm != null) Destroy(gm);
	}

	/// <summary>
	/// 판이 눌릴 때 들어가는 방향(월드)을 주입받는다. 패널 방향은 소유 메뉴만 알고 있으므로
	/// 메시만으로는 부호를 정할 수 없다. WorldMenuButton.Configure와 같은 계약이다.
	/// </summary>
	public void Configure(Vector3 intoPanelWorldDirection)
	{
		if (intoPanelWorldDirection.sqrMagnitude < 1e-6f) return;

		var local = WorldMenuGeometry.SnapToAxis(
			transform.InverseTransformDirection(intoPanelWorldDirection.normalized));

		float thickness = 0f;
		if (WorldMenuGeometry.TryLocalBounds(transform, out var bounds))
		{
			var size = bounds.size;
			thickness = size[WorldMenuGeometry.ShortestAxis(size)];
		}

		// localPosition은 부모 공간 값이다. 이 버튼은 자기 로컬 스케일(예: 0.2325)을 가질 수 있으므로
		// 로컬 두께를 부모 단위로 환산하고, 축도 부모 공간으로 돌려서 더한다.
		int axisIndex = WorldMenuGeometry.LongestAxis(
			new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)));
		float depth = thickness * Mathf.Abs(transform.localScale[axisIndex]) * pressDepthRatio;

		restLocalPosition = transform.localPosition;
		pressedLocalPosition = restLocalPosition + transform.localRotation * local * depth;
		pressConfigured = true;
	}

	void Update()
	{
		float t = Time.unscaledDeltaTime * speed;

		if (mat != null)
		{
			mat.SetColor(BaseColorID, Color.Lerp(mat.GetColor(BaseColorID), targetColor, t));
			mat.SetColor(EmissionID,
				Color.Lerp(mat.GetColor(EmissionID), targetColor * targetEmission, t));
		}

		if (glyphMats != null)
		{
			Color glyphTarget = Color.white * (targetEmission * glyphMultiplier);
			foreach (var gm in glyphMats)
			{
				if (gm == null) continue;
				gm.SetColor(EmissionID, Color.Lerp(gm.GetColor(EmissionID), glyphTarget, t));
			}
		}

		// 눌림 모션. 지수 감쇠 보간이라 프레임률과 무관하게 같은 속도로 수렴한다.
		var targetPosition = pointerPressed ? pressedLocalPosition : restLocalPosition;
		transform.localPosition = Vector3.Lerp(
			transform.localPosition, targetPosition,
			1f - Mathf.Exp(-motionSpeed * Time.unscaledDeltaTime));
	}

	void SetState(State s, bool instant = false)
	{
		state = s;
		switch (s)
		{
			case State.Disabled: targetColor = disabledColor; targetEmission = 0f; break;
			case State.Normal: targetColor = normalColor; targetEmission = 0f; break;
			case State.Hovered: targetColor = hoveredColor; targetEmission = hoverEmission; break;
			case State.Pressed: targetColor = pressedColor; targetEmission = pressEmission; break;
		}

		if (instant)
		{
			if (mat != null)
			{
				mat.SetColor(BaseColorID, targetColor);
				mat.SetColor(EmissionID, targetColor * targetEmission);
			}
			if (glyphMats != null)
			{
				Color glyphTarget = Color.white * (targetEmission * glyphMultiplier);
				foreach (var gm in glyphMats)
					if (gm != null) gm.SetColor(EmissionID, glyphTarget);
			}
		}
	}

	public void SetInteractable(bool value)
	{
		interactable = value;
		SetState(value ? State.Normal : State.Disabled);
	}

	public void OnHoverEnter()
	{
		if (!interactable) return;
		SetState(State.Hovered);
	}

	public void OnHoverExit()
	{
		if (!interactable) return;
		SetState(State.Normal);
	}

	public void OnSelectEnter()
	{
		if (!interactable) return;
		SetState(State.Pressed);
	}

	public void OnSelectExit()
	{
		if (!interactable) return;
		SetState(State.Hovered);
		onClick?.Invoke();
	}

	// ---- IWorldMenuTarget (WorldMenuPointer 경로) ----

	public void OnPointerEnter(WorldMenuPointer pointer) => OnHoverEnter();

	public void OnPointerExit(WorldMenuPointer pointer) => OnHoverExit();

	public void OnPointerDown(WorldMenuPointer pointer, RaycastHit hit)
	{
		if (!IsInteractable) return;

		if (!pressConfigured)
			Debug.LogWarning($"[Board] '{name}'의 눌림 방향이 설정되지 않아 움직이지 않습니다.", this);

		pointerPressed = true;
		OnSelectEnter();
		pointer?.Pulse(pressHaptic, pressHapticSeconds);
	}

	public void OnPointerDrag(WorldMenuPointer pointer) { }

	public void OnPointerUp(WorldMenuPointer pointer, bool onTarget)
	{
		bool wasPressed = pointerPressed;
		pointerPressed = false;

		// OnSelectExit는 무조건 onClick을 발화하므로 여기서는 쓰지 않는다.
		// 클릭은 같은 버튼 위에서 떼었을 때만 성립한다 — 버튼 밖으로 끌어내는 것이 취소다.
		SetState(interactable ? (onTarget ? State.Hovered : State.Normal) : State.Disabled);
		if (wasPressed && onTarget && IsInteractable) onClick?.Invoke();
	}
}
