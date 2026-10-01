using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Creates a single flat water plane at the terrain's water level. The carved
/// river channel and pond dip below it, so water shows through there and is
/// hidden where terrain rises above. One mesh, one collider — not cubes.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterPlane : MonoBehaviour
{
    [Header("Link to terrain (optional)")]
    [Tooltip("If set, size and height are taken from the terrain automatically.")]
    [SerializeField] private EditableTerrain terrain;

    [Header("Manual settings (used if no terrain linked)")]
    [SerializeField] private float size = 140f;
    [SerializeField] private float waterLevel = -0.5f;

    [Header("Mesh")]
    [Tooltip("Subdivisions. 1 = a single quad (fine, ripples are in the shader). " +
             "Raise only if you later add vertex waves.")]
    [SerializeField] private int subdivisions = 1;

    [Header("Material & layer")]
    [SerializeField] private Material waterMaterial;
    [Tooltip("Must match the layer your drink raycast checks.")]
    [SerializeField] private string waterLayer = "Water";
    [Tooltip("Add a collider so the water can be clicked (for drinking).")]
    [SerializeField] private bool addCollider = true;

    [ContextMenu("Generate")]
    public void Generate()
    {
        float s = size;
        float y = waterLevel;

        if (terrain != null)
        {
            s = terrain.Radius * 2f;
            y = terrain.WaterLevel;
        }

        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        Mesh mesh = BuildPlane(s, Mathf.Max(1, subdivisions));
        GetComponent<MeshFilter>().sharedMesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        if (waterMaterial != null) mr.sharedMaterial = waterMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (addCollider)
        {
            var mc = GetComponent<MeshCollider>();
            if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }

        int layer = LayerMask.NameToLayer(waterLayer);
        if (layer >= 0) gameObject.layer = layer;
        else Debug.LogWarning($"Layer '{waterLayer}' doesn't exist.");

        MarkDirty();
        Debug.Log($"Water plane generated: {s} units at Y={y}.");
    }

    private Mesh BuildPlane(float size, int sub)
    {
        int vps = sub + 1;
        var verts = new Vector3[vps * vps];
        var uvs = new Vector2[verts.Length];
        var tris = new int[sub * sub * 6];

        float half = size * 0.5f;
        float step = size / sub;

        for (int z = 0; z <= sub; z++)
            for (int x = 0; x <= sub; x++)
            {
                int idx = z * vps + x;
                verts[idx] = new Vector3(-half + x * step, 0f, -half + z * step);
                uvs[idx] = new Vector2((float)x / sub, (float)z / sub);
            }

        int t = 0;
        for (int z = 0; z < sub; z++)
            for (int x = 0; x < sub; x++)
            {
                int bl = z * vps + x, br = bl + 1, tl = bl + vps, tr = tl + 1;
                tris[t++] = bl; tris[t++] = tl; tris[t++] = br;
                tris[t++] = br; tris[t++] = tl; tris[t++] = tr;
            }

        var mesh = new Mesh { name = "WaterPlane" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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