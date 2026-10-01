using UnityEngine;

/// <summary>Draws the tamp fill ring on the ground at the planting site.</summary>
public class PlantTampUI : MonoBehaviour
{
    [SerializeField] private PlantTamp tamp;

    [Header("Geometry")]
    [SerializeField] private float radius = 0.8f;
    [SerializeField] private float heightOffset = 0.04f;
    [SerializeField] private float layerGap = 0.012f;
    [SerializeField] private float lineWidth = 0.16f;
    [SerializeField] private int ringSegments = 64;

    [Header("Colors")]
    [SerializeField] private Color trackColor = new Color(0.15f, 0.12f, 0.10f, 0.7f);
    [SerializeField] private Color fillColor = new Color(0.55f, 0.4f, 0.25f, 0.95f);
    [SerializeField] private Color goodColor = new Color(0.35f, 0.6f, 0.3f, 0.85f);
    [SerializeField] private Color perfectColor = new Color(0.95f, 0.93f, 0.45f, 1f);
    [SerializeField] private Color overColor = new Color(0.9f, 0.35f, 0.3f, 0.95f);

    private Transform ringRoot;
    private LineRenderer track;
    private LineRenderer goodBand;
    private LineRenderer perfectBand;
    private LineRenderer fill;

    private void Awake()
    {
        var rootGO = new GameObject("TampRing (runtime)");
        ringRoot = rootGO.transform;

        track = MakeLine("Track", trackColor, 0);
        goodBand = MakeLine("GoodBand", goodColor, 1);
        perfectBand = MakeLine("PerfectBand", perfectColor, 2);
        fill = MakeLine("Fill", fillColor, 3);

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (ringRoot != null) Destroy(ringRoot.gameObject);
    }

    private LineRenderer MakeLine(string lineName, Color c, int sortOrder)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(ringRoot, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.numCapVertices = 4;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = sortOrder;

        var shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        mat.renderQueue = 4000 + sortOrder;
        lr.material = mat;

        lr.startColor = c;
        lr.endColor = c;
        return lr;
    }

    private void LateUpdate()
    {
        bool active = tamp != null && tamp.IsActive;
        SetVisible(active);
        if (!active) return;

        track.widthMultiplier = lineWidth * 0.6f;
        goodBand.widthMultiplier = lineWidth;
        perfectBand.widthMultiplier = lineWidth;
        fill.widthMultiplier = lineWidth;

        Vector3 basePos = tamp.SitePosition + Vector3.up * heightOffset;
        float span = tamp.MaxOverfill;

        // Full track represents 0..maxOverfill
        DrawArc(track, basePos, radius, 0f, 360f);

        // Bands, positioned by this plant's rolled target
        float goodStart = tamp.GoodFrom / span * 360f;
        float goodSpan = (tamp.GoodTo - tamp.GoodFrom) / span * 360f;
        DrawArc(goodBand, basePos + Vector3.up * layerGap, radius + 0.03f, goodStart, goodSpan);

        float perfStart = tamp.PerfectFrom / span * 360f;
        float perfSpan = (tamp.PerfectTo - tamp.PerfectFrom) / span * 360f;
        DrawArc(perfectBand, basePos + Vector3.up * (layerGap * 2f),
                radius + 0.06f, perfStart, perfSpan);

        // The growing fill, turning red once it passes the good band
        float fillSpan = tamp.Fill / span * 360f;
        Color c = tamp.Fill > tamp.GoodTo ? overColor : fillColor;
        fill.startColor = c;
        fill.endColor = c;
        DrawArc(fill, basePos + Vector3.up * (layerGap * 3f), radius + 0.09f, 0f, fillSpan);
    }

    private void SetVisible(bool v)
    {
        track.enabled = v;
        goodBand.enabled = v;
        perfectBand.enabled = v;
        fill.enabled = v;
    }

    private static Vector3 AngleToDir(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    private void DrawArc(LineRenderer lr, Vector3 centre, float r, float startAngle, float spanDeg)
    {
        if (spanDeg <= 0.5f) { lr.positionCount = 0; return; }

        int segs = Mathf.Max(2, Mathf.CeilToInt(spanDeg / 6f));
        lr.positionCount = segs + 1;
        for (int i = 0; i <= segs; i++)
        {
            float a = startAngle + spanDeg * i / segs;
            lr.SetPosition(i, centre + AngleToDir(a) * r);
        }
    }
}