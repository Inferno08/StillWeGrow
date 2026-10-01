using UnityEngine;

/// <summary>Draws the dig ring on the ground at the dig site, entirely in code.</summary>
public class DigRingUI : MonoBehaviour
{
    [SerializeField] private DigMeter meter;
    [SerializeField] private DiggingSkill skill;

    [Header("Geometry")]
    [SerializeField] private float radius = 0.95f;
    [SerializeField] private float progressRadius = 0.6f;
    [SerializeField] private float tickInner = 0.72f;
    [SerializeField] private float tickOuter = 1.25f;
    [SerializeField] private float heightOffset = 0.03f;
    [SerializeField] private float layerGap = 0.012f;
    [SerializeField] private float radiusStep = 0.02f;
    [SerializeField] private int ringSegments = 72;
    [SerializeField] private float lineWidth = 0.16f;
    [SerializeField] private float ringWidthScale = 0.6f;

    [Header("Colors")]
    [SerializeField] private Color ringColor = new Color(0.15f, 0.12f, 0.10f, 0.75f);
    [SerializeField] private Color weakColor = new Color(0.85f, 0.62f, 0.25f, 0.9f);
    [SerializeField] private Color goodColor = new Color(0.45f, 0.85f, 0.35f, 0.95f);
    [SerializeField] private Color perfectColor = new Color(0.98f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color tickColor = Color.white;
    [SerializeField] private Color progressColor = new Color(0.6f, 0.4f, 0.22f, 0.95f);

    [Header("Feel")]
    [SerializeField] private float strikePunch = 0.12f;
    [SerializeField] private float punchDecay = 0.6f;

    private Transform ringRoot;
    private LineRenderer ring;
    private LineRenderer weakArc;
    private LineRenderer goodArc;
    private LineRenderer perfectArc;
    private LineRenderer tick;
    private LineRenderer progressArc;

    private float punch;

    private void Awake()
    {
        var rootGO = new GameObject("DigRing (runtime)");
        ringRoot = rootGO.transform;

        ring = MakeLine("Ring", ringColor, 0);
        progressArc = MakeLine("ProgressArc", progressColor, 1);
        weakArc = MakeLine("WeakArc", weakColor, 2);
        goodArc = MakeLine("GoodArc", goodColor, 3);
        perfectArc = MakeLine("PerfectArc", perfectColor, 4);
        tick = MakeLine("Tick", tickColor, 5);

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (ringRoot != null) Destroy(ringRoot.gameObject);
    }

    private void OnEnable()
    {
        if (meter != null) meter.OnStrike += HandleStrike;
    }

    private void OnDisable()
    {
        if (meter != null) meter.OnStrike -= HandleStrike;
    }

    private void HandleStrike(ActionQuality q)
    {
        if (q == ActionQuality.Miss) return;
        punch = strikePunch;
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

        // UI/Default exposes a ZTest property, so the ring can draw over the
        // character and any other geometry instead of being hidden behind it.
        var shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        mat.renderQueue = 4000 + sortOrder;   // after normal transparents
        lr.material = mat;

        lr.startColor = c;
        lr.endColor = c;

        return lr;
    }

    private void LateUpdate()
    {
        bool active = meter != null && meter.IsActive;
        SetVisible(active);
        if (!active) { punch = 0f; return; }

        ApplyWidths();

        punch = Mathf.MoveTowards(punch, 0f, punchDecay * Time.deltaTime);

        Vector3 basePos = meter.SitePosition + Vector3.up * heightOffset;
        float r = radius + punch;

        DrawCircle(ring, basePos, r, ringSegments);

        DrawArcFrom(progressArc, basePos + Vector3.up * layerGap,
                    progressRadius + punch * 0.5f, 90f, meter.Progress * 360f);

        DrawArc(weakArc, basePos + Vector3.up * (layerGap * 2f),
                r + radiusStep, meter.TargetAngle, meter.WeakWidth);

        DrawArc(goodArc, basePos + Vector3.up * (layerGap * 3f),
                r + radiusStep * 2f, meter.TargetAngle, meter.GoodWidth);

        DrawArc(perfectArc, basePos + Vector3.up * (layerGap * 4f),
                r + radiusStep * 3f, meter.TargetAngle, meter.PerfectWidth);

        Vector3 top = basePos + Vector3.up * (layerGap * 5f);
        Vector3 dir = AngleToDir(meter.Angle);
        tick.positionCount = 2;
        tick.SetPosition(0, top + dir * (tickInner + punch));
        tick.SetPosition(1, top + dir * (tickOuter + punch));
    }

    private void ApplyWidths()
    {
        ring.widthMultiplier = lineWidth * ringWidthScale;
        progressArc.widthMultiplier = lineWidth;
        weakArc.widthMultiplier = lineWidth;
        goodArc.widthMultiplier = lineWidth;
        perfectArc.widthMultiplier = lineWidth;
        tick.widthMultiplier = lineWidth;
    }

    private void SetVisible(bool v)
    {
        ring.enabled = v;
        weakArc.enabled = v;
        goodArc.enabled = v;
        perfectArc.enabled = v;
        tick.enabled = v;
        progressArc.enabled = v;
    }

    private static Vector3 AngleToDir(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    private void DrawCircle(LineRenderer lr, Vector3 center, float r, int segs)
    {
        lr.positionCount = segs + 1;
        for (int i = 0; i <= segs; i++)
        {
            float a = 360f * i / segs;
            lr.SetPosition(i, center + AngleToDir(a) * r);
        }
    }

    private void DrawArc(LineRenderer lr, Vector3 center, float r, float centerAngle, float widthDeg)
    {
        DrawArcFrom(lr, center, r, centerAngle - widthDeg * 0.5f, widthDeg);
    }

    private void DrawArcFrom(LineRenderer lr, Vector3 center, float r, float startAngle, float spanDeg)
    {
        if (spanDeg <= 0.01f)
        {
            lr.positionCount = 0;
            return;
        }

        int segs = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(spanDeg) / 6f));
        lr.positionCount = segs + 1;
        for (int i = 0; i <= segs; i++)
        {
            float a = startAngle + spanDeg * i / segs;
            lr.SetPosition(i, center + AngleToDir(a) * r);
        }
    }

    private void OnGUI()
    {
        if (skill == null || meter == null || !meter.IsActive) return;
        GUI.Label(new Rect(20f, 20f, 400f, 20f),
                      $"Digging Lv {skill.Level}   XP {skill.XP}/{skill.XPForNextLevel}" + $"   (+{skill.PendingXP} pending)   Combo {meter.Combo}");
    }
}