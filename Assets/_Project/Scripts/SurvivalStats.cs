using System;
using UnityEngine;

/// <summary>
/// Energy / hunger / thirst. Energy is the daily action budget; hunger and
/// thirst set how much energy you wake with. Moderate model: running low
/// hurts (costlier actions, tighter timing) but is never fatal and always
/// recoverable with a good meal.
/// </summary>
public class SurvivalStats : MonoBehaviour
{
    public static SurvivalStats Instance { get; private set; }

    [Header("Energy (daily action budget)")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energy = 100f;
    [Tooltip("Below this fraction, actions cost more and timing windows shrink.")]
    [Range(0f, 1f)][SerializeField] private float lowEnergyThreshold = 0.3f;
    [Tooltip("Action cost multiplier while below the low-energy threshold.")]
    [SerializeField] private float tiredCostMultiplier = 1.5f;
    [Tooltip("How much timing windows shrink when exhausted (0 = none, 0.3 = 30% tighter).")]
    [Range(0f, 0.6f)][SerializeField] private float tiredPrecisionPenalty = 0.3f;

    [Header("Action costs (energy)")]
    [Tooltip("Per dig STRIKE. ~10 holes/day means tune so a full hole costs ~8-10.")]
    public float digStrikeCost = 3f;
    public float plantCost = 4f;
    public float cutCost = 3f;

    [Header("Hunger & thirst")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float hunger = 70f;
    [SerializeField] private float maxThirst = 100f;
    [SerializeField] private float thirst = 70f;
    [Tooltip("Hunger lost per in-game day.")]
    [SerializeField] private float hungerDrainPerDay = 55f;
    [SerializeField] private float thirstDrainPerDay = 65f;

    [Header("Wake-up energy")]
    [Tooltip("Energy you wake with when perfectly fed & hydrated (fraction of max).")]
    [Range(0f, 1f)][SerializeField] private float wellFedWakeEnergy = 1f;
    [Tooltip("Energy you wake with when starving & parched (fraction of max).")]
    [Range(0f, 1f)][SerializeField] private float starvingWakeEnergy = 0.35f;

    public float Energy => energy;
    public float EnergyNormalized => maxEnergy > 0 ? energy / maxEnergy : 0f;
    public float Hunger => hunger;
    public float HungerNormalized => maxHunger > 0 ? hunger / maxHunger : 0f;
    public float Thirst => thirst;
    public float ThirstNormalized => maxThirst > 0 ? thirst / maxThirst : 0f;

    public bool IsTired => EnergyNormalized < lowEnergyThreshold;
    public bool IsExhausted => energy <= 0f;

    public bool HungerIsFull => hunger >= maxHunger - 0.01f;
    public bool ThirstIsFull => thirst >= maxThirst - 0.01f;

    /// <summary>Multiplier applied to timing-window widths (1 = normal, <1 when tired).</summary>
    public float PrecisionMultiplier => IsTired ? 1f - tiredPrecisionPenalty : 1f;

    public event Action OnStatsChanged;

    private bool subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start() { TrySubscribe(); }
    private void OnEnable() { TrySubscribe(); }

    private void OnDisable()
    {
        if (DayCycle.Instance != null) DayCycle.Instance.OnDayEnd -= HandleDayEnd;
        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed || DayCycle.Instance == null) return;
        DayCycle.Instance.OnDayEnd += HandleDayEnd;
        subscribed = true;
    }

    // ---------------------------------------------------------------

    /// <summary>
    /// True if the player can afford an action costing baseCost. Doesn't spend —
    /// call Spend() when the action actually happens.
    /// </summary>
    public bool CanAfford(float baseCost)
    {
        if (IsExhausted && baseCost > 0.5f) return false;
        return true;
    }

    /// <summary>Spend energy for an action, scaled up if tired.</summary>
    public void Spend(float baseCost)
    {
        float cost = baseCost * (IsTired ? tiredCostMultiplier : 1f);
        energy = Mathf.Max(0f, energy - cost);
        OnStatsChanged?.Invoke();
    }

    public void Eat(float hungerRestored)
    {
        hunger = Mathf.Min(maxHunger, hunger + hungerRestored);
        OnStatsChanged?.Invoke();
    }

    public void Drink(float thirstRestored)
    {
        thirst = Mathf.Min(maxThirst, thirst + thirstRestored);
        OnStatsChanged?.Invoke();
    }

    // ---------------------------------------------------------------

    private void HandleDayEnd(int day)
    {
        // Drain hunger/thirst for the day that just passed
        hunger = Mathf.Max(0f, hunger - hungerDrainPerDay);
        thirst = Mathf.Max(0f, thirst - thirstDrainPerDay);

        // Tomorrow's energy is the average of how fed & hydrated you are now
        float fullness = (HungerNormalized + ThirstNormalized) * 0.5f;
        float wakeFraction = Mathf.Lerp(starvingWakeEnergy, wellFedWakeEnergy, fullness);
        energy = maxEnergy * wakeFraction;

        OnStatsChanged?.Invoke();
        Debug.Log($"Woke on day {day + 1} with {energy:0}/{maxEnergy} energy " +
                  $"(fullness {fullness:0.00}). Hunger {hunger:0}, Thirst {thirst:0}.");
    }
}