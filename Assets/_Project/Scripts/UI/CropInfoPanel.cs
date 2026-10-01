using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Two behaviours:
/// - Hovering a growing crop shows its TYPE (name), not its grade (suspense).
/// - Harvesting pops a REVEAL card: grade, description, grade-scaled stats,
///   and any special effect unlocked at that grade.
/// Screen-space, works in any camera view.
/// </summary>
public class CropInfoPanel : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask cropMask;
    [SerializeField] private float maxDistance = 200f;

    [Header("Reveal card")]
    [Tooltip("How long the harvest reveal card stays up.")]
    [SerializeField] private float revealDuration = 3.5f;

    private PlantedCrop hovered;

    // Reveal state — captured at harvest so it persists after the crop is destroyed
    private bool revealActive;
    private float revealTimer;
    private string revealName;
    private string revealDescription;
    private string revealGrade;
    private Color revealGradeColor;
    private float revealHunger;
    private int revealSellValue;
    private string revealSpecial;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void OnEnable() { PlantedCrop.OnHarvested += HandleHarvested; }
    private void OnDisable() { PlantedCrop.OnHarvested -= HandleHarvested; }

    private void Update()
    {
        // Hover detection (growing crops → show type only)
        hovered = null;
        if (cam != null && Mouse.current != null)
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var hits = Physics.RaycastAll(ray, maxDistance, cropMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                var crop = h.collider.GetComponentInParent<PlantedCrop>();
                if (crop == null) continue;
                Vector3 toCrop = crop.transform.position - ray.origin;
                float along = Vector3.Dot(toCrop, ray.direction);
                Vector3 onRay = ray.origin + ray.direction * along;
                float off = (crop.transform.position - onRay).sqrMagnitude;
                if (off < best) { best = off; hovered = crop; }
            }
        }

        if (revealActive)
        {
            revealTimer -= Time.deltaTime;
            if (revealTimer <= 0f) revealActive = false;
        }
    }

    private void HandleHarvested(PlantedCrop crop)
    {
        if (crop == null || crop.Data == null) return;

        var d = crop.Data;
        float level01 = crop.FarmingLevel01Public;
        int gradeIdx = QualityGrade.GradeIndex(crop.Quality, level01);
        float strength = QualityGrade.GradeStrength(gradeIdx);

        revealName = d.cropName;
        revealDescription = d.description;
        revealGrade = QualityGrade.Ladder[gradeIdx];
        revealGradeColor = QualityGrade.GradeColor(crop.Quality, level01);
        // Hunger scales from base up to base×topMultiplier across the ladder
        revealHunger = d.baseHungerValue * Mathf.Lerp(1f, d.topGradeHungerMultiplier, strength);
        revealSellValue = d.sellValue;
        // Special effect: must meet grade threshold AND win the proc roll
        revealSpecial = "";
        if (!string.IsNullOrEmpty(d.specialEffect) && gradeIdx >= d.specialEffectFromGradeIndex)
        {
            // Chance scales from base (at the unlock grade) to top (at S++)
            float chance = Mathf.Lerp(d.specialEffectBaseChance, d.specialEffectTopChance, strength);
            if (Random.value < chance)
                revealSpecial = d.specialEffect;
        }
        revealActive = true;
        revealTimer = revealDuration;
    }

    private void OnGUI()
    {
        // Hover panel (growing crop → type only, no grade)
        if (hovered != null && hovered.Data != null && !hovered.IsMature)
        {
            DrawHoverPanel(hovered);
        }
        else if (hovered != null && hovered.Data != null && hovered.IsMature)
        {
            DrawReadyPanel(hovered);
        }

        // Harvest reveal card
        if (revealActive) DrawRevealCard();
    }

    private void DrawHoverPanel(PlantedCrop crop)
    {
        float w = 220f, h = 70f, x = 20f, y = Screen.height - h - 20f;
        GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
        GUILayout.Label(crop.Data.cropName, Title());
        GUILayout.Label("Still growing…", Body());
        GUILayout.Label("<i>Quality revealed at harvest</i>", Body());
        GUILayout.EndArea();
    }

    private void DrawReadyPanel(PlantedCrop crop)
    {
        float w = 220f, h = 56f, x = 20f, y = Screen.height - h - 20f;
        GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
        GUILayout.Label(crop.Data.cropName, Title());
        GUILayout.Label("<b>Ready to harvest</b>", Body());
        GUILayout.EndArea();
    }

    private void DrawRevealCard()
    {
        float w = 300f, h = 220f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.28f;

        // Fade out over the last second
        float alpha = Mathf.Clamp01(revealTimer);
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);

        GUILayout.Label(revealName, Title(18));

        string hex = ColorUtility.ToHtmlStringRGB(revealGradeColor);
        GUILayout.Label($"Quality: <color=#{hex}>{revealGrade}</color>", Title(24));

        GUILayout.Space(4);
        GUILayout.Label(revealDescription, Body());
        GUILayout.Space(6);

        GUILayout.Label($"Restores <b>{revealHunger:0}</b> hunger", Body());
        GUILayout.Label($"Sells for <b>{revealSellValue}</b>c", Body());

        if (!string.IsNullOrEmpty(revealSpecial))
        {
            GUILayout.Space(4);
            GUILayout.Label($"<color=#FFD84A><b>✦ {revealSpecial}</b></color>", Body());
        }

        GUILayout.EndArea();
        GUI.color = prev;
    }

    private GUIStyle Title(int size = 15) => new GUIStyle(GUI.skin.label)
    { fontSize = size, fontStyle = FontStyle.Bold, richText = true, wordWrap = true };

    private GUIStyle Body() => new GUIStyle(GUI.skin.label)
    { fontSize = 12, richText = true, wordWrap = true };
}