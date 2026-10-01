using UnityEngine;

/// <summary>
/// Spawns a crop prop and scales it so its RENDERED world size matches a target
/// height in world units — independent of prefab scale, child scale, or the
/// anchor's scale (e.g. a flat 3x0.1x3 mat). Measures actual geometry rather
/// than trusting scale values.
/// </summary>
public static class CropVisualUtil
{
    public static GameObject SpawnAtSize(GameObject prefab, Transform anchor,
                                         Vector3 localWorldOffset, float targetWorldHeight)
    {
        // Build the world position from the anchor's orientation in world units,
        // so the flat mat scale doesn't squash the layout spacing.
        Vector3 worldPos = anchor.position
                         + anchor.right * localWorldOffset.x
                         + anchor.up * localWorldOffset.y
                         + anchor.forward * localWorldOffset.z;

        var go = Object.Instantiate(prefab, worldPos,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        // Measure rendered height BEFORE parenting (so the mat's scale isn't
        // affecting the measurement yet), then scale to the target.
        float currentHeight = MeasureWorldHeight(go);
        if (currentHeight > 0.0001f)
            go.transform.localScale *= targetWorldHeight / currentHeight;

        // Now parent, preserving world transform. Because we scaled in world
        // space first and SetParent(true) keeps world scale, the flat mat can't
        // distort it.
        go.transform.SetParent(anchor, true);

        foreach (var col in go.GetComponentsInChildren<Collider>())
            Object.Destroy(col);

        return go;
    }

    public static float MeasureWorldHeight(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return b.size.y;
    }
}