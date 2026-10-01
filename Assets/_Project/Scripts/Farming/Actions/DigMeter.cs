using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DigMeter : MonoBehaviour
{
    [Header("Skill")]
    [SerializeField] private DiggingSkill skill;

    [Header("Weak zone, degrees (grows first: levels 1-3)")]
    [SerializeField] private float weakStartWidth = 34f;
    [SerializeField] private float weakEndWidth = 60f;
    [SerializeField] private int weakFromLevel = 1;
    [SerializeField] private int weakToLevel = 3;

    [Header("Good zone, degrees (grows next: levels 3-6)")]
    [SerializeField] private float goodStartWidth = 14f;
    [SerializeField] private float goodEndWidth = 38f;
    [SerializeField] private int goodFromLevel = 3;
    [SerializeField] private int goodToLevel = 6;

    [Header("Perfect zone, degrees (grows last: levels 6-10)")]
    [SerializeField] private float perfectStartWidth = 5f;
    [SerializeField] private float perfectEndWidth = 20f;
    [SerializeField] private int perfectFromLevel = 6;
    [SerializeField] private int perfectToLevel = 10;

    [Header("Sweep")]
    [SerializeField] private float baseSweepSpeed = 0.78f;
    [SerializeField] private float maxLevelSweepSpeed = 0.6f;
    [SerializeField] private float comboSpeedStep = 0.11f;
    [SerializeField] private int comboSpeedCap = 6;

    [Header("Target jump")]
    [SerializeField] private float minJumpDistance = 80f;

    [Header("Progress per strike")]
    [SerializeField] private float perfectProgress = 0.42f;
    [SerializeField] private float goodProgress = 0.26f;
    [SerializeField] private float weakProgress = 0.12f;
    [SerializeField] private float missProgress = 0f;
    [SerializeField] private float comboProgressStep = 0.12f;
    [SerializeField] private int comboProgressCap = 5;
    [SerializeField] private float maxLevelProgressMultiplier = 1.6f;

    public bool IsActive { get; private set; }
    public float Angle { get; private set; }
    public float TargetAngle { get; private set; }
    public float Progress { get; private set; }
    public int Combo { get; private set; }
    public Vector3 SitePosition { get; private set; }

    private int Level => skill != null ? skill.Level : 1;
    private float Mastery => skill != null ? skill.Mastery : 0f;

    private float TiredScale =>
        SurvivalStats.Instance != null ? SurvivalStats.Instance.PrecisionMultiplier : 1f;

    public float WeakWidth => Ramp(weakStartWidth, weakEndWidth, weakFromLevel, weakToLevel) * TiredScale;
    public float GoodWidth => Mathf.Min(
        Ramp(goodStartWidth, goodEndWidth, goodFromLevel, goodToLevel) * TiredScale, WeakWidth);
    public float PerfectWidth => Mathf.Min(
        Ramp(perfectStartWidth, perfectEndWidth, perfectFromLevel, perfectToLevel) * TiredScale, GoodWidth);

    public float SweepSpeed
    {
        get
        {
            float baseSpeed = Mathf.Lerp(baseSweepSpeed, maxLevelSweepSpeed, Mastery);
            float comboBoost = 1f + Mathf.Min(Combo, comboSpeedCap) * comboSpeedStep;
            return baseSpeed * comboBoost;
        }
    }

    private float ProgressMultiplier
    {
        get
        {
            float skillMult = Mathf.Lerp(1f, maxLevelProgressMultiplier, Mastery);
            float comboMult = 1f + Mathf.Min(Combo, comboProgressCap) * comboProgressStep;
            return skillMult * comboMult;
        }
    }

    public int LastEndFrame { get; private set; } = -1;

    public event Action<ActionQuality> OnStrike;
    public event Action OnDigComplete;
    public event Action OnDigCancelled;
    public event Action OnTooTired;

    private float Ramp(float from, float to, int fromLevel, int toLevel)
    {
        if (toLevel <= fromLevel) return Level >= toLevel ? to : from;
        float t = Mathf.InverseLerp(fromLevel, toLevel, Level);
        return Mathf.Lerp(from, to, t);
    }

    /// <param name="startProgress">Where the dig begins (0 fresh, >0 resuming a decayed hole).</param>
    public bool Begin(Vector3 site, float startProgress = 0f)
    {
        var stats = SurvivalStats.Instance;
        if (stats != null && !stats.CanAfford(stats.digStrikeCost))
        {
            OnTooTired?.Invoke();
            return false;
        }

        IsActive = true;
        SitePosition = site;
        Progress = Mathf.Clamp01(startProgress);
        Combo = 0;
        Angle = 0f;
        TargetAngle = UnityEngine.Random.Range(0f, 360f);

        if (skill != null) skill.DiscardPendingXP();
        return true;
    }

    public void Cancel()
    {
        if (!IsActive) return;
        IsActive = false;
        LastEndFrame = Time.frameCount;
        if (skill != null) skill.DiscardPendingXP();
        OnDigCancelled?.Invoke();
    }

    private void Update()
    {
        if (!IsActive) return;

        Angle = Mathf.Repeat(Angle + SweepSpeed * 360f * Time.deltaTime, 360f);

        if ((Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            Cancel();
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Strike();
    }

    private void Strike()
    {
        var stats = SurvivalStats.Instance;
        if (stats != null) stats.Spend(stats.digStrikeCost);

        float dist = Mathf.Abs(Mathf.DeltaAngle(Angle, TargetAngle));

        ActionQuality result;
        float gain;

        if (dist <= PerfectWidth * 0.5f) { result = ActionQuality.Perfect; gain = perfectProgress; }
        else if (dist <= GoodWidth * 0.5f) { result = ActionQuality.Good; gain = goodProgress; }
        else if (dist <= WeakWidth * 0.5f) { result = ActionQuality.Weak; gain = weakProgress; }
        else { result = ActionQuality.Miss; gain = missProgress; }

        if (result == ActionQuality.Miss) Combo = 0;
        else
        {
            Progress = Mathf.Clamp01(Progress + gain * ProgressMultiplier);
            Combo++;
            JumpTarget();
        }

        if (skill != null) skill.AddPendingXP(result);
        OnStrike?.Invoke(result);

        if (Progress >= 1f)
        {
            IsActive = false;
            LastEndFrame = Time.frameCount;
            if (skill != null) skill.CommitPendingXP();
            OnDigComplete?.Invoke();
        }
        else if (stats != null && stats.IsExhausted)
        {
            Cancel();
        }
    }

    private void JumpTarget()
    {
        float offset = UnityEngine.Random.Range(minJumpDistance, 360f - minJumpDistance);
        TargetAngle = Mathf.Repeat(Angle + offset, 360f);
    }
}