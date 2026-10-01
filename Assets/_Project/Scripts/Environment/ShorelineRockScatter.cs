using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Scatters rock prefabs around the water: in the shallows, along the edge, and
/// on nearby land. Samples the terrain to find where the shore is, so it adapts
/// to whatever the generator produced. Editor-time.
/// </summary>
public class ShorelineRockScatter : MonoBehaviour
{
    [Header("Terrain & water")]
    [SerializeField] private EditableTerrain terrain;
    [Tooltip("Water Y — usually taken from the terrain automatically.")]
    [SerializeField] private float waterLevelOverride = float.NaN;

    [Header("Rock prefabs")]
    [Tooltip("Any prefabs — they'll be picked at random. Cubes/spheres fine for greybox.")]
    [SerializeField] private GameObject[] rockPrefabs;

    [Header("Zone bands (Y relative to water level)")]
    [Tooltip("Rocks whose base sits below water by up to this = 'in the shallows'.")]
    [SerializeField] private float shallowDepth = 0.4f;
    [Tooltip("Height above water still counted as 'edge'.")]
    [SerializeField] private float edgeHeight = 0.5f;
    [Tooltip("Height above water counted as 'nearby land'.")]
    [SerializeField] private float landHeight = 2.5f;

    [Header("Counts")]
    [SerializeField] private int shallowRocks = 40;
    [SerializeField] private int edgeRocks = 80;
    [SerializeField] private int landRocks = 50;
    [Tooltip("Sampling attempts per rock before giving up on a spot.")]
    [SerializeField] private int attemptsPerRock = 20;

    [Header("Placement")]
    [SerializeField] private float sampleRadius = 80f;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.4f, 1.6f);
    [Tooltip("Sink rocks into the ground a bit so they don't float.")]
    [SerializeField] private float sinkAmount = 0.15f;
    [Tooltip("Minimum gap between placed rocks.")]
    [SerializeField] private float minSpacing = 0.6f;
    [SerializeField] private int seed = 999;

    private System.Random rng;
    private readonly List<Vector2> placed = new();

    private float WaterLevel =>
        !float.IsNaN(waterLevelOverride) ? waterLevelOverride
        : (terrain != null ? terrain.WaterLevel : 0f);

    [ContextMenu("Scatter")]
    public void Scatter()
    {
        if (terrain == null) { Debug.LogWarning("Assign the terrain."); return; }
        if (rockPrefabs == null || rockPrefabs.Length == 0)
        { Debug.LogWarning("Assign at least one rock prefab."); return; }

        Clear();
        rng = new System.Random(seed);
        placed.Clear();

        var root = new GameObject("ShorelineRocks");
        RegisterCreated(root);
        root.transform.SetParent(transform, false);

        PlaceZone(root.transform, shallowRocks, -shallowDepth, 0f, "Shallow");
        PlaceZone(root.transform, edgeRocks, 0f, edgeHeight, "Edge");
        PlaceZone(root.transform, landRocks, edgeHeight, landHeight, "Land");

        MarkDirty();
        Debug.Log($"Scattered {root.transform.childCount} rocks.");
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (c.name != "ShorelineRocks") continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Undo.DestroyObjectImmediate(c); continue; }
#endif
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        placed.Clear();
        MarkDirty();
    }

    /// <summary>Place rocks whose ground height sits within [minRel, maxRel] of water level.</summary>
    private void PlaceZone(Transform parent, int count, float minRel, float maxRel, string label)
    {
        float wl = WaterLevel;
        int placedCount = 0;

        for (int i = 0; i < count; i++)
        {
            bool found = false;

            for (int a = 0; a < attemptsPerRock && !found; a++)
            {
                // Random point in the sample disc
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rad = Mathf.Sqrt((float)rng.NextDouble()) * sampleRadius;
                float x = Mathf.Cos(ang) * rad;
                float z = Mathf.Sin(ang) * rad;

                float groundY = terrain.SampleHeight(x, z);
                float rel = groundY - wl;

                if (rel < minRel || rel > maxRel) continue;         // wrong zone
                if (TooClose(new Vector2(x, z))) continue;           // spacing

                PlaceRock(parent, new Vector3(x, groundY, z), label);
                placed.Add(new Vector2(x, z));
                placedCount++;
                found = true;
            }
        }
    }

    private bool TooClose(Vector2 p)
    {
        float sq = minSpacing * minSpacing;
        foreach (var o in placed)
            if ((o - p).sqrMagnitude < sq) return true;
        return false;
    }

    private void PlaceRock(Transform parent, Vector3 pos, string label)
    {
        var prefab = rockPrefabs[rng.Next(rockPrefabs.Length)];
        var go = Instantiate(prefab, parent);
        RegisterCreated(go);

        go.name = $"Rock_{label}";
        float s = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
        go.transform.position = pos - Vector3.up * (sinkAmount * s);
        go.transform.rotation = Quaternion.Euler(
            (float)rng.NextDouble() * 12f - 6f,          // slight random tilt
            (float)rng.NextDouble() * 360f,
            (float)rng.NextDouble() * 12f - 6f);
        go.transform.localScale = go.transform.localScale * s;
    }

    private void RegisterCreated(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, "Scatter Rocks");
#endif
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, sampleRadius);
    }
}