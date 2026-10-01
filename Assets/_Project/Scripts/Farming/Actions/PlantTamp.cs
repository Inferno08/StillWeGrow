using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hold-and-release soil tamping. The target band moves to a new position on
/// every plant, so you have to watch the ring rather than count seconds.
/// Planting always succeeds — only the resulting soil quality varies.
/// </summary>
public class PlantTamp : MonoBehaviour
{
    [Header("Skill")]
    [Tooltip("Optional. Leave empty if planting shouldn't grant XP yet.")]
    [SerializeField] private DiggingSkill skill;
    [SerializeField] private int perfectXP = 8;
    [SerializeField] private int goodXP = 5;
    [SerializeField] private int weakXP = 2;

    [Header("Timing")]
    [Tooltip("How fast the tamp fills, in fill units per second.")]
    [SerializeField] private float fillSpeed = 1.15f;
    [Tooltip("Fill stops climbing here and auto-releases.")]
    [SerializeField] private float maxOverfill = 1.5f;

    [Header("Target band")]
    [Tooltip("The band's centre is rolled somewhere in this range on every plant.")]
    [SerializeField] private Vector2 targetCentreRange = new Vector2(0.45f, 1.1f);
    [Tooltip("Half-width of the Perfect band.")]
    [SerializeField] private float perfectHalfWidth = 0.075f;
    [Tooltip("Half-width of the Good band. Outside this = Weak.")]
    [SerializeField] private float goodHalfWidth = 0.2f;
    [Tooltip("Widen both bands as digging level rises. 1 = no change.")]
    [SerializeField] private float maxLevelBandMultiplier = 1.6f;

    [Header("Debug")]
    [Tooltip("Log the rolled target position on every plant.")]
    [SerializeField] private bool logTargetRoll = false;

    public bool IsActive { get; private set; }
    public float Fill { get; private set; }
    public Vector3 SitePosition { get; private set; }

    /// <summary>Centre of this plant's target band, rolled at Begin().</summary>
    public float TargetCentre { get; private set; }

    private float Mastery => skill != null ? skill.Mastery : 0f;
    private float BandScale => Mathf.Lerp(1f, maxLevelBandMultiplier, Mastery);

    public float PerfectHalfWidth => perfectHalfWidth * BandScale;
    public float GoodHalfWidth => goodHalfWidth * BandScale;

    public float PerfectFrom => TargetCentre - PerfectHalfWidth;
    public float PerfectTo => TargetCentre + PerfectHalfWidth;
    public float GoodFrom => TargetCentre - GoodHalfWidth;
    public float GoodTo => TargetCentre + GoodHalfWidth;
    public float MaxOverfill => maxOverfill;

    /// <summary>Frame the tamp ended, so the release click isn't reused.</summary>
    public int LastEndFrame { get; private set; } = -1;

    /// <summary>Quality of the tamp, plus a 0..1 soil multiplier for growth.</summary>
    public event Action<ActionQuality, float> OnComplete;
    public event Action OnCancelled;

    /// <summary>True if the player released before reaching the band (vs overshooting).</summary>
    public bool ReleasedEarly => Fill < TargetCentre;

    private void OnValidate()
    {
        // Stop the range collapsing to nothing, which would freeze the target.
        if (targetCentreRange.y <= targetCentreRange.x)
            targetCentreRange = new Vector2(0.45f, 1.1f);

        maxOverfill = Mathf.Max(maxOverfill, targetCentreRange.y + goodHalfWidth + 0.1f);
    }

    public void Begin(Vector3 site)
    {
        IsActive = true;
        SitePosition = site;
        Fill = 0f;
        TargetCentre = RollTarget();

        if (logTargetRoll)
            Debug.Log($"Tamp target rolled at {TargetCentre:0.000} " +
                      $"(range {targetCentreRange.x:0.00}-{targetCentreRange.y:0.00})");
    }

    /// <summary>
    /// Picks a new band position. Falls back to sane defaults rather than
    /// silently collapsing to a fixed value if the range is misconfigured.
    /// </summary>
    private float RollTarget()
    {
        float lo = targetCentreRange.x;
        float hi = targetCentreRange.y;

        if (hi <= lo)
        {
            Debug.LogWarning($"PlantTamp: targetCentreRange is empty ({lo}-{hi}). " +
                             "Using 0.45-1.1 instead — fix the field in the Inspector.");
            lo = 0.45f;
            hi = 1.1f;
        }

        // Keep the band clear of both ends, but never let clamping invert the range
        float ceiling = maxOverfill - GoodHalfWidth - 0.05f;
        if (ceiling > lo) hi = Mathf.Min(hi, ceiling);

        return UnityEngine.Random.Range(lo, hi);
    }

    public void Cancel()
    {
        if (!IsActive) return;
        IsActive = false;
        LastEndFrame = Time.frameCount;
        OnCancelled?.Invoke();
    }

    private void Update()
    {
        if (!IsActive) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cancel();
            return;
        }

        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.isPressed)
        {
            Fill = Mathf.Min(Fill + fillSpeed * Time.deltaTime, maxOverfill);

            // Held to the ceiling — auto-release rather than stall
            if (Fill >= maxOverfill) Release();
            return;
        }

        // Only score once they've actually pressed and let go
        if (Fill > 0.01f) Release();
    }

    private void Release()
    {
        IsActive = false;
        LastEndFrame = Time.frameCount;

        float dist = Mathf.Abs(Fill - TargetCentre);

        ActionQuality quality;
        float soil;

        if (dist <= PerfectHalfWidth)
        {
            quality = ActionQuality.Perfect;
            soil = 1f;
        }
        else if (dist <= GoodHalfWidth)
        {
            quality = ActionQuality.Good;
            soil = Mathf.Lerp(0.95f, 0.72f,
                              Mathf.InverseLerp(PerfectHalfWidth, GoodHalfWidth, dist));
        }
        else
        {
            quality = ActionQuality.Weak;
            float far = Mathf.InverseLerp(GoodHalfWidth, GoodHalfWidth + 0.5f, dist);
            soil = Mathf.Lerp(0.68f, 0.4f, far);
        }

        // Awarded here because releasing IS completing — there's no partial plant
        if (skill != null)
        {
            int gain = quality switch
            {
                ActionQuality.Perfect => perfectXP,
                ActionQuality.Good => goodXP,
                _ => weakXP
            };
            skill.AddXP(gain);
        }

        OnComplete?.Invoke(quality, soil);
    }
}