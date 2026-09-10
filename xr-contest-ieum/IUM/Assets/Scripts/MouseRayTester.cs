using UnityEngine;

public class MouseRayTester : MonoBehaviour
{
	[SerializeField] Camera cam;

	Board3DButton hoveredButton;
	Board3DSlider hoveredSlider;
	Board3DSlider grabbedSlider;

	void Awake() { if (cam == null) cam = Camera.main; }

	void Update()
	{
		if (grabbedSlider != null)
		{
			Vector3 minScreen = cam.WorldToScreenPoint(grabbedSlider.GetMinPos());
			Vector3 maxScreen = cam.WorldToScreenPoint(grabbedSlider.GetMaxPos());

			Vector2 axis = (Vector2)(maxScreen - minScreen);
			if (axis.sqrMagnitude > 0.0001f)
			{
				Vector2 offset = (Vector2)Input.mousePosition - (Vector2)minScreen;
				float t = Mathf.Clamp01(Vector2.Dot(offset, axis.normalized) / axis.magnitude);
				grabbedSlider.SetValueFromDrag(t);
			}

			if (Input.GetMouseButtonUp(0))
			{
				grabbedSlider.OnGrabEnd();
				grabbedSlider = null;
			}
			return;
		}

		Ray ray = cam.ScreenPointToRay(Input.mousePosition);

		Board3DButton btn = null;
		Board3DSlider sld = null;

		if (Physics.Raycast(ray, out RaycastHit info, 100f))
		{
			btn = info.collider.GetComponentInParent<Board3DButton>();
			sld = info.collider.GetComponentInParent<Board3DSlider>();
		}

		if (btn != hoveredButton)
		{
			if (hoveredButton != null) hoveredButton.OnHoverExit();
			if (btn != null) btn.OnHoverEnter();
			hoveredButton = btn;
		}

		if (sld != hoveredSlider)
		{
			if (hoveredSlider != null) hoveredSlider.OnHoverExit();
			if (sld != null) sld.OnHoverEnter();
			hoveredSlider = sld;
		}

		if (hoveredButton != null)
		{
			if (Input.GetMouseButtonDown(0)) hoveredButton.OnSelectEnter();
			if (Input.GetMouseButtonUp(0)) hoveredButton.OnSelectExit();
		}

		if (hoveredSlider != null && Input.GetMouseButtonDown(0))
		{
			grabbedSlider = hoveredSlider;
			grabbedSlider.OnGrabStart();
		}
	}
}