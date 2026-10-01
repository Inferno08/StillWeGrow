using UnityEngine;

/// <summary>
/// A living crop. Grows through stages as days pass, scaled by soil and water.
/// Tracks an evolving QUALITY (0..1) from soil and daily care; the player's
/// farming level CAPS the grade — early on everything is F, higher tiers unlock
/// as mastery climbs. Grade is only revealed at maturity.
/// </summary>
public class PlantedCrop : MonoBehaviour
{
    [SerializeField] private CropData data;

    [Header("Pop-in")]
    [SerializeField] private float popDuration = 0.35f;
    [SerializeField] private float popOvershoot = 1.2f;

    [Tooltip("0..1 — soil quality from tamping. Set at planting.")]
    public float soilQuality = 1f;

    public bool WateredToday { get; set; }
    public bool IsMature => stage >= data.StageCount - 1 && stageProgress >= 1f;
    public CropData Data => data;

    // --- Quality ---
    private float quality = 0.5f;
    private int daysLived;
    private int daysCaredFor;

    public float Quality => Mathf.Clamp01(quality);
    public string Grade => QualityGrade.ToGrade(quality, FarmingLevel01());
    public Color GradeColor => QualityGrade.GradeColor(quality, FarmingLevel01());

    private int stage;
    private float stageProgress;
    private GameObject currentVisual;

    private Vector3 baseScale;
    private float popTimer;
    private bool popping;

    /// <summary>Fires the moment a crop is harvested, before it's destroyed.</summary>
    public static event System.Action<PlantedCrop> OnHarvested;

    /// <summary>Exposes the capped farming level for the info panel.</summary>
    public float FarmingLevel01Public => FarmingLevel01();

    public void Initialise(CropData cropData, float soil)
    {
        data = cropData;
        soilQuality = Mathf.Clamp01(soil);
        stage = 0;
        stageProgress = 0f;

        // Planting baseline comes from soil; level caps the grade later.
        quality = Mathf.Clamp01(soilQuality * 0.7f);
        daysLived = 0;
        daysCaredFor = 0;

        SpawnStageVisual();

        if (DayCycle.Instance != null)
            DayCycle.Instance.OnDayEnd += OnDayEnd;
    }

    private void OnDestroy()
    {
        if (DayCycle.Instance != null)
            DayCycle.Instance.OnDayEnd -= OnDayEnd;
    }

    private float FarmingLevel01()
    {
        if (DiggingSkill.Current != null) return DiggingSkill.Current.Mastery;
        return 0.5f;
    }

    private void OnDayEnd(int day)
    {
        EvolveQuality();

        if (IsMature) { WateredToday = false; return; }

        float soilFactor = Mathf.Lerp(1f, soilQuality, data.soilSensitivity);
        float waterFactor = WateredToday ? 1f : data.unwateredGrowthFactor;
        AdvanceGrowth(soilFactor * waterFactor);

        WateredToday = false;
    }

    /// <summary>
    /// Each day, nudge quality toward what soil + care deserve. Good days raise
    /// it, neglect lowers it. Farming level does NOT feed here — it caps the
    /// final grade at display time instead.
    /// </summary>
    private void EvolveQuality()
    {
        daysLived++;
        if (WateredToday) daysCaredFor++;

        float care01 = daysLived > 0 ? (float)daysCaredFor / daysLived : 0f;
        float target = Mathf.Clamp01(soilQuality * 0.5f + care01 * 0.5f);

        quality = Mathf.MoveTowards(quality, target, 0.15f);
    }

    private void AdvanceGrowth(float days)
    {
        while (days > 0f && !IsMature)
        {
            float needed = data.DaysForStage(stage);
            float remainingThisStage = (1f - stageProgress) * needed;

            if (days >= remainingThisStage)
            {
                days -= remainingThisStage;
                stage = Mathf.Min(stage + 1, data.StageCount - 1);
                stageProgress = (stage >= data.StageCount - 1) ? 1f : 0f;
                SpawnStageVisual();
                if (stage >= data.StageCount - 1) break;
            }
            else
            {
                stageProgress += days / needed;
                days = 0f;
            }
        }
    }

    private void SpawnStageVisual()
    {
        if (data.stagePrefabs == null || stage >= data.stagePrefabs.Length) return;
        if (currentVisual != null) Destroy(currentVisual);

        var prefab = data.stagePrefabs[stage];
        if (prefab == null) return;

        currentVisual = Instantiate(prefab, transform.position,
                                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);

        baseScale = currentVisual.transform.localScale * Mathf.Lerp(0.8f, 1f, soilQuality);
        currentVisual.transform.localScale = Vector3.zero;
        popTimer = 0f;
        popping = true;
    }

    private void Update()
    {
        if (!popping || currentVisual == null) return;

        popTimer += Time.deltaTime;
        float t = Mathf.Clamp01(popTimer / popDuration);

        float s = t < 0.7f
            ? Mathf.Lerp(0f, popOvershoot, (1f - Mathf.Pow(1f - t / 0.7f, 3f)))
            : Mathf.Lerp(popOvershoot, 1f, (t - 0.7f) / 0.3f);

        currentVisual.transform.localScale = baseScale * s;

        if (t >= 1f)
        {
            currentVisual.transform.localScale = baseScale;
            popping = false;
        }
    }

    public bool TryHarvestToHands()
    {
        if (!IsMature) return false;

        var hands = CarryInventory.Instance;
        if (hands == null) return false;
        if (!hands.TryAdd(data, data.yieldAmount)) return false;

        OnHarvested?.Invoke(this);   // reveal card reads grade/effects here
        Destroy(gameObject);
        return true;
    }
}