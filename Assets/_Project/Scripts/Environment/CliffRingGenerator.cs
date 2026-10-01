using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Tall blocky cliff ring around the terrain floor, with climbable ledge
/// outcrops jutting FROM the cliff face on one side (the future ascent route).
/// Ledges are attached to the wall, not floating. Editor-time.
/// </summary>
public class CliffRingGenerator : MonoBehaviour
{
    [Header("Link to terrain")]
    [SerializeField] private EditableTerrain terrain;
    [SerializeField] private float fallbackRadius = 64f;

    [Header("Seed")]
    [SerializeField] private bool randomizeEachTime = false;
    [SerializeField] private int seed = 555;

    [Header("Cliff ring")]
    [SerializeField] private float cliffHeight = 40f;
    [SerializeField] private float cliffFootDepth = 5f;
    [SerializeField] private float cliffThickness = 8f;
    [SerializeField] private int segments = 48;
    [SerializeField] private float topJagged = 6f;
    [SerializeField] private float radialJitter = 3f;

    [Header("Ledges (climbable outcrops on one side)")]
    [Tooltip("Angle (deg) where the ledge route climbs — match river entry.")]
    [SerializeField] private float ledgeAngle = 200f;
    [Tooltip("Number of stacked ledges from floor toward the rim.")]
    [SerializeField, Range(2, 8)] private int ledgeCount = 5;
    [Tooltip("Vertical gap between ledges (a jump/climb apart).")]
    [SerializeField] private float ledgeStep = 5f;
    [Tooltip("How far each ledge juts IN from the cliff face.")]
    [SerializeField] private float ledgeJut = 6f;
    [Tooltip("Width of each ledge along the wall.")]
    [SerializeField] private float ledgeWidth = 9f;
    [Tooltip("Thickness of the ledge slab.")]
    [SerializeField] private float ledgeThickness = 1.2f;
    [Tooltip("Random spread in the angle of each ledge, so the route zig-zags.")]
    [SerializeField] private float ledgeAngleSpread = 30f;
    [Tooltip("Vary how far each juts in, for a natural look.")]
    [SerializeField] private float ledgeJutVariance = 2f;

    [Header("Materials & layers")]
    [SerializeField] private Material cliffMaterial;
    [SerializeField] private Material ledgeMaterial;
    [SerializeField] private string cliffLayer = "Wall";
    [Tooltip("Ledge tops are walkable/farmable ground.")]
    [SerializeField] private string ledgeLayer = "Ground";

    private Transform root;
    private System.Random rng;

    private float Radius => terrain != null ? terrain.Radius : fallbackRadius;

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (randomizeEachTime) seed = Random.Range(int.MinValue, int.MaxValue);
        rng = new System.Random(seed);

        Clear();

        var rootGO = new GameObject("CliffRing");
        RegisterCreated(rootGO);
        rootGO.transform.SetParent(transform, false);
        root = rootGO.transform;

        BuildCliffRing();
        BuildLedges();

        MarkDirty();
        Debug.Log($"Cliff ring built. {root.childCount} objects.");
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (c.name != "CliffRing") continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Undo.DestroyObjectImmediate(c); continue; }
#endif
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        MarkDirty();
    }

    // ---------------------------------------------------------------

    private void BuildCliffRing()
    {
        float ringR = Radius + cliffThickness * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float ang = (float)i / segments * Mathf.PI * 2f;
            float jitter = (float)(rng.NextDouble() - 0.5) * 2f * radialJitter;
            float r = ringR + jitter;

            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;

            float topExtra = (float)(rng.NextDouble() - 0.5) * 2f * topJagged;
            float height = cliffHeight + cliffFootDepth + topExtra;
            float centreY = -cliffFootDepth + height * 0.5f;

            var block = Make("Cliff",
                new Vector3(x, centreY, z),
                new Vector3(cliffThickness * 1.15f, height, cliffThickness * 1.15f),
                cliffMaterial, cliffLayer);

            block.transform.rotation = Quaternion.LookRotation(new Vector3(-x, 0f, -z).normalized, Vector3.up);
        }
    }

    /// <summary>
    /// Climbable ledges jutting inward from the cliff face, stacked upward and
    /// zig-zagging in angle so they form an ascent route you could path between.
    /// Each ledge's OUTER edge sits at the cliff wall (Radius), so it's attached.
    /// </summary>
    private void BuildLedges()
    {
        float baseAng = ledgeAngle * Mathf.Deg2Rad;

        for (int s = 0; s < ledgeCount; s++)
        {
            // Zig-zag the angle left/right as we climb, so the route isn't a straight tower
            float spread = (float)(rng.NextDouble() - 0.5) * 2f * ledgeAngleSpread * Mathf.Deg2Rad;
            float ang = baseAng + spread;

            float jut = ledgeJut + (float)(rng.NextDouble() - 0.5) * 2f * ledgeJutVariance;

            // Ledge centre sits so its OUTER edge meets the cliff wall (attached)
            float ledgeCentreR = Radius - jut * 0.5f;
            float y = ledgeStep * (s + 1);

            float x = Mathf.Cos(ang) * ledgeCentreR;
            float z = Mathf.Sin(ang) * ledgeCentreR;

            var ledge = Make($"Ledge_{s}",
                new Vector3(x, y, z),
                new Vector3(ledgeWidth, ledgeThickness, jut),
                ledgeMaterial != null ? ledgeMaterial : cliffMaterial, ledgeLayer);

            // Orient so the ledge's depth points inward from the wall
            ledge.transform.rotation = Quaternion.LookRotation(new Vector3(-x, 0f, -z).normalized, Vector3.up);

            // A small support strut down to visually connect it to the wall below
            float strutH = ledgeStep + ledgeThickness;
            var strut = Make($"LedgeSupport_{s}",
                new Vector3(Mathf.Cos(ang) * (Radius - ledgeThickness),
                            y - strutH * 0.5f,
                            Mathf.Sin(ang) * (Radius - ledgeThickness)),
                new Vector3(ledgeWidth * 0.7f, strutH, ledgeThickness * 1.5f),
                cliffMaterial, cliffLayer);
            strut.transform.rotation = ledge.transform.rotation;
        }
    }

    // ---------------------------------------------------------------

    private GameObject Make(string goName, Vector3 pos, Vector3 scale, Material mat, string layerName)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        RegisterCreated(go);
        go.name = goName;
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;

        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) go.layer = layer;
        else Debug.LogWarning($"Layer '{layerName}' doesn't exist.");
        return go;
    }

    private void RegisterCreated(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, "Generate Cliffs");
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
        Gizmos.color = new Color(0.6f, 0.5f, 0.4f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, Radius);
        Gizmos.color = new Color(0.4f, 0.7f, 0.9f, 0.6f);
        float a = ledgeAngle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radius;
        Gizmos.DrawLine(transform.position, transform.position + dir);
    }
}