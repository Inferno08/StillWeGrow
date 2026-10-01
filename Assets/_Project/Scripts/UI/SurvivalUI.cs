using UnityEngine;

public class SurvivalUI : MonoBehaviour
{
    [SerializeField] private SurvivalStats stats;

    private void OnGUI()
    {
        if (stats == null) return;

        float x = 20f, y = 60f, w = 220f, h = 18f, gap = 6f;

        Bar(x, y, w, h, stats.EnergyNormalized,
            new Color(0.95f, 0.85f, 0.3f), $"Energy {stats.Energy:0}" +
            (stats.IsExhausted ? "  (spent)" : stats.IsTired ? "  (tired)" : ""));

        Bar(x, y + (h + gap), w, h, stats.HungerNormalized,
            new Color(0.85f, 0.5f, 0.3f), $"Hunger {stats.Hunger:0}");

        Bar(x, y + (h + gap) * 2, w, h, stats.ThirstNormalized,
            new Color(0.4f, 0.7f, 0.95f), $"Thirst {stats.Thirst:0}");
    }

    private void Bar(float x, float y, float w, float h, float fill, Color c, string label)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fill), h), Texture2D.whiteTexture);

        GUI.color = Color.white;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(x + 6f, y, w, h), label, style);
    }
}