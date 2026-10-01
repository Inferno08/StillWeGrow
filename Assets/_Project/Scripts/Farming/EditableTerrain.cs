using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Editable chunked terrain. Generates the rolling floor + river + pond, and
/// supports furrow tilling: the tilled spot becomes a RAISED ridge with lower
/// troughs on either side, running along Z, snapped to the vertex grid so
/// ridges are crisp and line up between patches. Only touched chunks rebuild.
/// Vertex colours carry tilled amount (R) and ridge height (G) so the shader
/// can colour soil and light crests vs dark troughs.
/// </summary>
public class EditableTerrain : MonoBehaviour
{
    public static EditableTerrain Instance { get; private set; }

    [Header("Seed")]
    [SerializeField] private bool randomizeEachTime = true;
    [SerializeField] private int seed = 12345;

    [Header("Size")]
    [Tooltip("Higher = crisper furrows but heavier. 320 gives a vertex every ~0.4 units.")]
    [SerializeField] private int resolution = 320;
    [SerializeField] private float radius = 64f;
    [SerializeField] private int chunksPerSide = 10;

    [Header("Character reference")]
    [SerializeField] private float characterHeight = 2f;

    [Header("Gentle undulation")]
    [SerializeField, Range(0f, 1.5f)] private float bumpHeightFraction = 0.35f;
    [SerializeField] private float bumpScale = 0.03f;
    [Range(0f, 0.3f)][SerializeField] private float detailFraction = 0.06f;
    [SerializeField] private float detailScale = 0.12f;
    [SerializeField] private float groundFloor = 0f;

    [Header("Water")]
    [SerializeField] private float waterLevel = -0.5f;

    [Header("River")]
    [SerializeField, Range(0f, 360f)] private float riverEntryAngle = 200f;
    [SerializeField, Range(0.5f, 4f)] private float riverWidthChars = 1.5f;
    [SerializeField, Range(0.3f, 2f)] private float riverDepthFraction = 0.9f;
    [SerializeField, Range(0.5f, 6f)] private float bankWidthChars = 3.5f;
    [SerializeField, Range(0f, 1f)] private float riverWind = 0.45f;
    [SerializeField, Range(0.5f, 4f)] private float riverBends = 2.5f;
    [SerializeField, Range(0.5f, 1.3f)] private float riverLength = 1.05f;

    [Header("Pond")]
    [SerializeField, Range(2f, 12f)] private float pondRadiusChars = 5f;
    [SerializeField, Range(0.05f, 0.4f)] private float pondDepthRatio = 0.18f;
    [SerializeField, Range(0f, 0.5f)] private float pondBroadWobble = 0.22f;
    [SerializeField, Range(0f, 0.3f)] private float pondEdgeDetail = 0.08f;
    [SerializeField, Range(1f, 2.2f)] private float pondStretch = 1.5f;

    [Header("Furrows")]
    [Tooltip("How high the ridge rises / troughs dip, fraction of character height.")]
    [SerializeField, Range(0.05f, 0.5f)] private float furrowDepthFraction = 0.18f;
    [Tooltip("Default ridge spacing (world units). Snaps to the vertex grid.")]
    [SerializeField] private float defaultRidgeSpacing = 1.0f;
    [Tooltip("How near crest level (0..1 of furrow depth) still counts as plantable ridge. Lower = only the tops.")]
    [SerializeField, Range(0.1f, 0.9f)] private float crestThreshold = 0.2f;

    [Header("Material & layer")]
    [SerializeField] private Material terrainMaterial;
    [SerializeField] private string groundLayer = "Ground";

    // --- Public API ---
    public float Radius => radius;
    public float WaterLevel => waterLevel;
    public float FurrowDepth => furrowDepthFraction * characterHeight;

    // --- Shared data ---
    private float[] heights;      // base terrain height per vertex
    private float[] tillOffset;   // height change from tilling
    private float[] tilled;       // 0..1 tilled amount (soil colour / plant gate)
    private int vps;
    private float step;

    private readonly List<TerrainChunk> chunks = new();

    // --- Generation state ---
    private System.Random rng;
    private List<Vector2> riverPath;
    private Vector2 pondCentre;
    private float pondAxisAngle;
    private float bumpOffX, bumpOffZ, detailOffX, detailOffZ;
    private float pondBroadOff, pondDetailOff;

    private float CharWidth => characterHeight * 0.4f;
    private float BumpHeight => bumpHeightFraction * characterHeight;
    private float DetailHeight => detailFraction * characterHeight;
    private float RiverWidth => riverWidthChars * CharWidth;
    private float RiverDepth => riverDepthFraction * characterHeight;
    private float BankWidth => bankWidthChars * CharWidth;
    private float PondRadius => pondRadiusChars * characterHeight;
    private float PondDepth => PondRadius * pondDepthRatio;

    private class TerrainChunk
    {
        public GameObject go;
        public Mesh mesh;
        public MeshCollider collider;
        public int vx0, vz0, vCount;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (heights == null && transform.childCount > 0) Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (randomizeEachTime) seed = Random.Range(int.MinValue, int.MaxValue);
        rng = new System.Random(seed);

        bumpOffX = Rand(0f, 1000f); bumpOffZ = Rand(0f, 1000f);
        detailOffX = Rand(0f, 1000f); detailOffZ = Rand(0f, 1000f);
        pondBroadOff = Rand(0f, 1000f); pondDetailOff = Rand(0f, 1000f);

        BuildRiverPath();
        pondCentre = riverPath[riverPath.Count - 1];
        Vector2 inflow = (riverPath[riverPath.Count - 1] - riverPath[riverPath.Count - 4]).normalized;
        pondAxisAngle = Mathf.Atan2(inflow.y, inflow.x);

        vps = resolution + 1;
        step = (radius * 2f) / resolution;

        heights = new float[vps * vps];
        tillOffset = new float[vps * vps];
        tilled = new float[vps * vps];
        for (int z = 0; z < vps; z++)
            for (int x = 0; x < vps; x++)
            {
                float wx = -radius + x * step;
                float wz = -radius + z * step;
                heights[z * vps + x] = HeightAt(wx, wz);
            }

        BuildChunks();
        MarkDirty();
        Debug.Log($"Editable terrain generated. {chunksPerSide}x{chunksPerSide} chunks, {vps}x{vps} verts.");
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Undo.DestroyObjectImmediate(c); continue; }
#endif
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        chunks.Clear();
        MarkDirty();
    }

    // ---------------------------------------------------------------
    // FURROW TILLING
    // ---------------------------------------------------------------

    /// <summary>
    /// Till furrows at a spot. The clicked point becomes a RAISED ridge crest,
    /// with lower troughs on either side. Furrows run along Z; grid-snapped so
    /// ridges are crisp and line up between separate tilled patches.
    /// </summary>
    public void TillFurrow(Vector3 worldPos, float worldRadius,
                           float desiredSpacing = -1f, float amount = 1f)
    {
        if (heights == null) return;
        if (desiredSpacing <= 0f) desiredSpacing = defaultRidgeSpacing;

        int stepsPerRidge = Mathf.Max(2, Mathf.RoundToInt(desiredSpacing / step));
        float snapped = stepsPerRidge * step;

        GetVertexRange(worldPos, worldRadius, out int minX, out int maxX, out int minZ, out int maxZ);
        Vector2 centreXZ = new Vector2(worldPos.x, worldPos.z);

        for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                float wx = -radius + x * step;
                float wz = -radius + z * step;
                float d = Vector2.Distance(new Vector2(wx, wz), centreXZ);
                if (d > worldRadius) continue;

                float fall = 1f - Mathf.SmoothStep(0.7f, 1f, d / worldRadius);

                float across = wx - worldPos.x;
                float phase = across / snapped;
                float raw = 0.5f + 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
                float ridge = Mathf.SmoothStep(0f, 1f, raw);   // rounder tops and troughs
                float targetOffset = -FurrowDepth * (1f - ridge);   // 0 crest, -depth trough

                int i = z * vps + x;
                float blend = Mathf.Clamp01(amount * fall);
                tillOffset[i] = Mathf.Lerp(tillOffset[i], targetOffset, blend);
                tilled[i] = Mathf.Clamp01(tilled[i] + blend);
            }

        RebuildChunksInRange(minX, maxX, minZ, maxZ);
    }

    /// <summary>Raise tilled ground back toward grass (decay / fill-in).</summary>
    public void Untill(Vector3 worldPos, float worldRadius, float amount)
    {
        if (heights == null) return;
        GetVertexRange(worldPos, worldRadius, out int minX, out int maxX, out int minZ, out int maxZ);
        Vector2 centreXZ = new Vector2(worldPos.x, worldPos.z);

        for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                float wx = -radius + x * step, wz = -radius + z * step;
                float d = Vector2.Distance(new Vector2(wx, wz), centreXZ);
                if (d > worldRadius) continue;
                int i = z * vps + x;
                tillOffset[i] = Mathf.MoveTowards(tillOffset[i], 0f, FurrowDepth * amount);
                tilled[i] = Mathf.Clamp01(tilled[i] - amount);
            }

        RebuildChunksInRange(minX, maxX, minZ, maxZ);
    }

    /// <summary>Is this world point tilled soil at all?</summary>
    public bool IsTilled(Vector3 worldPos, float threshold = 0.5f)
    {
        int i = IndexAt(worldPos);
        return i >= 0 && tilled[i] >= threshold;
    }

    /// <summary>
    /// Is this point on a plantable ridge crest? True when tilled AND the till
    /// offset is near crest level (not down in a trough).
    /// </summary>
    public bool IsOnRidgeCrest(Vector3 worldPos)
    {
        int i = IndexAt(worldPos);
        if (i < 0 || tilled[i] < 0.5f) return false;
        return tillOffset[i] > -FurrowDepth * crestThreshold;
    }

    private int IndexAt(Vector3 worldPos)
    {
        if (heights == null) return -1;
        int x = Mathf.Clamp(Mathf.RoundToInt((worldPos.x + radius) / step), 0, vps - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt((worldPos.z + radius) / step), 0, vps - 1);
        return z * vps + x;
    }

    private void GetVertexRange(Vector3 worldPos, float worldRadius,
                                out int minX, out int maxX, out int minZ, out int maxZ)
    {
        float cx = (worldPos.x + radius) / step;
        float cz = (worldPos.z + radius) / step;
        int rad = Mathf.CeilToInt(worldRadius / step) + 1;
        minX = Mathf.Clamp(Mathf.FloorToInt(cx) - rad, 0, vps - 1);
        maxX = Mathf.Clamp(Mathf.CeilToInt(cx) + rad, 0, vps - 1);
        minZ = Mathf.Clamp(Mathf.FloorToInt(cz) - rad, 0, vps - 1);
        maxZ = Mathf.Clamp(Mathf.CeilToInt(cz) + rad, 0, vps - 1);
    }

    private void RebuildChunksInRange(int minX, int maxX, int minZ, int maxZ)
    {
        foreach (var c in chunks)
        {
            int cxMax = c.vx0 + c.vCount - 1;
            int czMax = c.vz0 + c.vCount - 1;
            if (!(maxX < c.vx0 || minX > cxMax || maxZ < c.vz0 || minZ > czMax))
                RebuildChunkMesh(c);
        }
    }

    // ---------------------------------------------------------------
    // Chunk building
    // ---------------------------------------------------------------

    private void BuildChunks()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        chunks.Clear();

        int cps = Mathf.Max(1, chunksPerSide);
        int cells = resolution / cps;
        for (int cz = 0; cz < cps; cz++)
            for (int cx = 0; cx < cps; cx++)
            {
                var chunk = CreateChunk(cx, cz, cx * cells, cz * cells, cells + 1);
                chunks.Add(chunk);
                RebuildChunkMesh(chunk);
            }
    }

    private TerrainChunk CreateChunk(int cx, int cz, int vx0, int vz0, int vCount)
    {
        var go = new GameObject($"Chunk_{cx}_{cz}");
        RegisterCreated(go);
        go.transform.SetParent(transform, false);
        int layer = LayerMask.NameToLayer(groundLayer);
        if (layer >= 0) go.layer = layer;

        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        if (terrainMaterial != null) mr.sharedMaterial = terrainMaterial;
        var mc = go.AddComponent<MeshCollider>();

        var mesh = new Mesh { name = $"ChunkMesh_{cx}_{cz}" };
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        mc.sharedMesh = mesh;
        return new TerrainChunk { go = go, mesh = mesh, collider = mc, vx0 = vx0, vz0 = vz0, vCount = vCount };
    }

    private void RebuildChunkMesh(TerrainChunk chunk)
    {
        int n = chunk.vCount;
        var verts = new Vector3[n * n];
        var uvs = new Vector2[n * n];
        var colors = new Color[n * n];
        var tris = new int[(n - 1) * (n - 1) * 6];

        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int gx = Mathf.Min(chunk.vx0 + x, vps - 1);
                int gz = Mathf.Min(chunk.vz0 + z, vps - 1);
                float wx = -radius + gx * step;
                float wz = -radius + gz * step;
                int idx = z * n + x;
                verts[idx] = new Vector3(wx, EffectiveHeight(gx, gz), wz);
                uvs[idx] = new Vector2((float)gx / resolution, (float)gz / resolution);

                // R = tilled amount (soil colour). G = ridge height (crest=1, trough=0).
                int vi = gz * vps + gx;
                float tilledAmt = tilled[vi];
                float ridgeHeight = FurrowDepth > 0.0001f
                    ? Mathf.Clamp01(1f + tillOffset[vi] / FurrowDepth)
                    : 1f;
                colors[idx] = new Color(tilledAmt, ridgeHeight, 0f, 1f);
            }

        int t = 0;
        for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                int bl = z * n + x, br = bl + 1, tl = bl + n, tr = tl + 1;
                tris[t++] = bl; tris[t++] = tl; tris[t++] = br;
                tris[t++] = br; tris[t++] = tl; tris[t++] = tr;
            }

        chunk.mesh.Clear();
        chunk.mesh.vertices = verts;
        chunk.mesh.uv = uvs;
        chunk.mesh.colors = colors;
        chunk.mesh.triangles = tris;
        chunk.mesh.RecalculateNormals();
        chunk.mesh.RecalculateBounds();
        chunk.collider.sharedMesh = null;
        chunk.collider.sharedMesh = chunk.mesh;
    }

    private float EffectiveHeight(int gx, int gz)
    {
        int i = gz * vps + gx;
        return heights[i] + tillOffset[i];
    }

    // ---------------------------------------------------------------
    // Height generation
    // ---------------------------------------------------------------

    private void BuildRiverPath()
    {
        riverPath = new List<Vector2>();
        float entryRad = riverEntryAngle * Mathf.Deg2Rad;
        Vector2 entry = new Vector2(Mathf.Cos(entryRad), Mathf.Sin(entryRad)) * radius * 0.98f;
        float endAngle = entryRad + Mathf.PI * Rand(0.7f, 1.3f);
        Vector2 endDir = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
        Vector2 end = entry + (endDir * radius - entry) * (riverLength * 0.6f);
        end = Vector2.ClampMagnitude(end, radius * 0.75f);

        int steps = 48;
        Vector2 perp = Vector2.Perpendicular((end - entry).normalized);
        float noiseOff = Rand(0f, 500f);
        for (int s = 0; s <= steps; s++)
        {
            float tt = (float)s / steps;
            Vector2 straight = Vector2.Lerp(entry, end, tt);
            float envelope = Mathf.Sin(tt * Mathf.PI);
            float bend = Mathf.Sin(tt * Mathf.PI * riverBends) * riverWind * radius * 0.2f;
            float noise = (Mathf.PerlinNoise(noiseOff, tt * 3f) - 0.5f) * riverWind * radius * 0.15f;
            riverPath.Add(straight + perp * (bend + noise) * envelope);
        }
    }

    private float HeightAt(float x, float z)
    {
        float bumps = (Mathf.PerlinNoise(bumpOffX + x * bumpScale, bumpOffZ + z * bumpScale) - 0.5f) * 2f * BumpHeight;
        float detail = (Mathf.PerlinNoise(detailOffX + x * detailScale, detailOffZ + z * detailScale) - 0.5f) * 2f * DetailHeight;
        float ground = Mathf.Max(groundFloor, groundFloor + bumps + detail);
        float dRiver = DistanceToRiverPath(new Vector2(x, z));
        float river = ChannelProfile(dRiver, RiverWidth * 0.5f, BankWidth, RiverDepth);
        float pond = PondCarve(x, z);
        return ground - Mathf.Max(river, pond);
    }

    private float PondCarve(float x, float z)
    {
        Vector2 d = new Vector2(x, z) - pondCentre;
        float ca = Mathf.Cos(-pondAxisAngle), sa = Mathf.Sin(-pondAxisAngle);
        Vector2 local = new Vector2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);
        local.x /= pondStretch;
        float dist = local.magnitude;
        float broad = (Mathf.PerlinNoise(pondBroadOff + x * 0.03f, pondBroadOff + z * 0.03f) - 0.5f) * 2f * pondBroadWobble;
        float fine = (Mathf.PerlinNoise(pondDetailOff + x * 0.12f, pondDetailOff + z * 0.12f) - 0.5f) * 2f * pondEdgeDetail;
        float effR = PondRadius * (1f + broad + fine);
        return ChannelProfile(dist, effR, BankWidth, PondDepth);
    }

    private static float ChannelProfile(float dist, float halfWidth, float bankWidth, float depth)
    {
        float outer = halfWidth + bankWidth;
        if (dist >= outer) return 0f;
        float t = 1f - (dist / outer);
        return depth * (t * t * (3f - 2f * t));
    }

    private float DistanceToRiverPath(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < riverPath.Count - 1; i++)
        {
            float dd = DistToSegment(p, riverPath[i], riverPath[i + 1]);
            if (dd < best) best = dd;
        }
        return best;
    }

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f);
        return Vector2.Distance(p, a + ab * Mathf.Clamp01(t));
    }

    public float SampleHeight(float worldX, float worldZ)
    {
        int i = IndexAt(new Vector3(worldX, 0f, worldZ));
        if (i < 0) return 0f;
        int gx = i % vps, gz = i / vps;
        return EffectiveHeight(gx, gz);
    }

    private float Rand(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

    private void RegisterCreated(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, "Generate Terrain");
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
}