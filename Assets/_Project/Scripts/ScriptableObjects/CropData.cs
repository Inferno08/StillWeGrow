using UnityEngine;

[CreateAssetMenu(fileName = "CropData", menuName = "Sunward Hollow/Crop Data")]
public class CropData : ScriptableObject
{
    [Header("Identity")]
    public string cropName = "Crop";

    [Header("Growth")]
    [Tooltip("Stage meshes in order: sprout -> ... -> mature. At least 2.")]
    public GameObject[] stagePrefabs;
    [Tooltip("In-game days each stage takes at 100% growth conditions.")]
    public float[] daysPerStage = { 2f, 2f, 3f };

    [Header("Description")]
    [TextArea(2, 4)]
    public string description = "A humble crop.";

    [Header("Effects (scale with grade)")]
    [Tooltip("Hunger restored at F grade. Higher grades restore more.")]
    public float baseHungerValue = 20f;
    [Tooltip("Hunger multiplier at the top grade (S++).")]
    public float topGradeHungerMultiplier = 2.5f;
    [TextArea(1, 3)]
    [Tooltip("Special effect description. Empty = none.")]
    public string specialEffect = "";
    [Tooltip("Grade index (0=F ... 8=S++) at which the special effect can occur.")]
    public int specialEffectFromGradeIndex = 5;
    [Tooltip("Chance (0..1) the effect procs at the minimum grade. Keep tiny for rare effects.")]
    [Range(0f, 1f)] public float specialEffectBaseChance = 1f;
    [Tooltip("Chance (0..1) the effect procs at the TOP grade (S++). Lets rarity ease up as quality climbs.")]
    [Range(0f, 1f)] public float specialEffectTopChance = 1f;

    [Header("Conditions")]
    [Range(0f, 2f)] public float soilSensitivity = 1f;
    [Range(0f, 1f)] public float unwateredGrowthFactor = 0.4f;

    [Header("Harvest")]
    public int sellValue = 10;
    public int yieldAmount = 1;
    [Tooltip("Hunger restored when this crop is eaten.")]
    public float hungerValue = 25f;

    [Header("Placement")]
    public float rootRadius = 0.6f;

    [Header("Visuals")]
    [Tooltip("Small prop shown when carried or stacked in storage. " +
             "If empty, the mature stage prefab is used.")]
    public GameObject carryProp;

    public int StageCount => stagePrefabs != null ? stagePrefabs.Length : 0;

    public float DaysForStage(int stage) =>
        (daysPerStage != null && stage < daysPerStage.Length) ? daysPerStage[stage] : 2f;
}