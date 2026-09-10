using UnityEngine;
using UnityEditor;

public class MeasureBounds
{
	[MenuItem("Tools/Measure Selected Bounds")]
	static void Measure()
	{
		foreach (GameObject go in Selection.gameObjects)
		{
			var renderers = go.GetComponentsInChildren<Renderer>();
			if (renderers.Length == 0) continue;

			Bounds b = renderers[0].bounds;
			foreach (var r in renderers) b.Encapsulate(r.bounds);

			Debug.Log($"[{go.name}]\n" +
					  $"  Size    : {b.size.ToString("F5")}\n" +
					  $"  Center  : {b.center.ToString("F5")}\n" +
					  $"  로컬 중심 오프셋: {go.transform.InverseTransformPoint(b.center).ToString("F5")}");
		}
	}
}