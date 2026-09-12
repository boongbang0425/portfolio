using UnityEngine;

/// <summary>
/// What a <see cref="WorldMenuPointer"/> can aim at. Deliberately not
/// <see cref="UnityEngine.EventSystems.IPointerClickHandler"/>: uGUI's event system needs a Canvas
/// and a raycaster, and these targets are plain meshes imported from an FBX.
/// </summary>
public interface IWorldMenuTarget
{
    bool IsInteractable { get; }

    void OnPointerEnter(WorldMenuPointer pointer);
    void OnPointerExit(WorldMenuPointer pointer);
    void OnPointerDown(WorldMenuPointer pointer, RaycastHit hit);

    /// <summary>Called every frame between down and up, whether or not the ray is still on target.</summary>
    void OnPointerDrag(WorldMenuPointer pointer);

    void OnPointerUp(WorldMenuPointer pointer, bool onTarget);
}

/// <summary>
/// Bounds and collider helpers for menu meshes.
///
/// The Pause/Option prefabs come out of an FBX where every child sits at localPosition zero with
/// identity rotation and all shape is baked into the vertices, and none of them carry a collider.
/// So sizes and axes have to be read back from the mesh rather than assumed, and hit volumes are
/// generated at runtime instead of authored.
/// </summary>
public static class WorldMenuGeometry
{
    /// <summary>Combined bounds of every mesh under <paramref name="target"/>, in its local space.</summary>
    public static bool TryLocalBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        if (target == null) return false;

        var filters = target.GetComponentsInChildren<MeshFilter>(true);
        var found = false;

        foreach (var filter in filters)
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;

            var local = mesh.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f);

                var world = filter.transform.TransformPoint(local.center + Vector3.Scale(local.extents, sign));
                var point = target.InverseTransformPoint(world);

                if (!found)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Fits a BoxCollider to the mesh, never shrinking below <paramref name="minimumSize"/>. Existing
    /// colliders are reused and refitted so running this twice cannot stack duplicates.
    /// </summary>
    public static BoxCollider FitBoxCollider(Transform target, Vector3 minimumSize)
    {
        if (target == null) return null;

        var collider = target.GetComponent<BoxCollider>();
        if (collider == null) collider = target.gameObject.AddComponent<BoxCollider>();

        if (TryLocalBounds(target, out var bounds))
        {
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size, minimumSize);
        }

        return collider;
    }

    public static int LongestAxis(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z) return 0;
        return size.y >= size.z ? 1 : 2;
    }

    public static int ShortestAxis(Vector3 size)
    {
        if (size.x <= size.y && size.x <= size.z) return 0;
        return size.y <= size.z ? 1 : 2;
    }

    public static Vector3 Axis(int index) => index switch
    {
        0 => Vector3.right,
        1 => Vector3.up,
        _ => Vector3.forward
    };

    /// <summary>Nearest signed axis to <paramref name="direction"/>, as a unit vector.</summary>
    public static Vector3 SnapToAxis(Vector3 direction)
    {
        var absolute = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
        var index = LongestAxis(absolute);
        return Axis(index) * Mathf.Sign(direction[index]);
    }

    /// <summary>
    /// Distance along a line, measured from <paramref name="origin"/>, at the point closest to a ray.
    /// Used to turn "where the pointer is aiming" into a slider position without needing the ray to
    /// actually touch the handle.
    /// </summary>
    public static float ClosestPointOnLine(Ray ray, Vector3 origin, Vector3 direction)
    {
        var w = origin - ray.origin;
        var a = Vector3.Dot(direction, direction);
        var b = Vector3.Dot(direction, ray.direction);
        var c = Vector3.Dot(ray.direction, ray.direction);
        var d = Vector3.Dot(direction, w);
        var e = Vector3.Dot(ray.direction, w);

        var denominator = a * c - b * b;

        // Parallel: any point is as close as any other, so fall back to projecting the ray origin.
        if (Mathf.Abs(denominator) < 1e-6f)
            return -d / Mathf.Max(a, 1e-6f);

        return (b * e - c * d) / denominator;
    }
}
