using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hold-to-open radial crop picker. Hold the key, flick the mouse toward a
/// wedge, release to select. Sets the chosen crop on PlayerTargeting. Drawn in
/// screen space so it works in any camera perspective.
/// </summary>
public class CropRadialMenu : MonoBehaviour
{
    [SerializeField] private PlayerTargeting targeting;

    [Header("Available crops")]
    [Tooltip("Crops shown in the radial. Later: only crops you have seeds for.")]
    [SerializeField] private List<CropData> crops = new();

    [Header("Open control")]
    [SerializeField] private Key openKey = Key.Tab;
    [Tooltip("Slow motion while open, for that satisfying pause. 1 = no slow.")]
    [Range(0.05f, 1f)][SerializeField] private float timeScaleWhileOpen = 0.15f;

    [Header("Layout")]
    [SerializeField] private float radius = 160f;
    [SerializeField] private float wedgeIconScale = 1f;
    [Tooltip("Mouse must move this far from center to highlight a wedge.")]
    [SerializeField] private float deadZone = 40f;

    [Header("Colors")]
    [SerializeField] private Color wedgeColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color wedgeHighlight = new Color(0.3f, 0.8f, 0.35f, 0.95f);
    [SerializeField] private Color textColor = Color.white;

    private bool open;
    private int highlighted = -1;
    private float savedTimeScale = 1f;
    private Texture2D wedgeTex;
    private Texture2D highlightTex;

    private void Awake()
    {
        if (targeting == null) targeting = FindFirstObjectByType<PlayerTargeting>();
        wedgeTex = SolidTex(wedgeColor);
        highlightTex = SolidTex(wedgeHighlight);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        bool keyHeld = Keyboard.current[openKey].isPressed;

        if (keyHeld && !open) OpenMenu();
        else if (!keyHeld && open) CloseAndSelect();

        if (open) UpdateHighlight();
    }

    private void OpenMenu()
    {
        if (crops == null || crops.Count == 0) return;
        open = true;
        highlighted = -1;
        savedTimeScale = Time.timeScale;
        Time.timeScale = timeScaleWhileOpen;
    }

    private void CloseAndSelect()
    {
        open = false;
        Time.timeScale = savedTimeScale;

        if (highlighted >= 0 && highlighted < crops.Count && targeting != null)
        {
            targeting.SelectedCrop = crops[highlighted];
            Debug.Log($"Selected crop: {crops[highlighted].cropName}");
        }
    }

    private void UpdateHighlight()
    {
        if (Mouse.current == null) return;

        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir = mouse - center;

        if (dir.magnitude < deadZone) { highlighted = -1; return; }

        // Angle from up, clockwise, mapped to a wedge index
        float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        int count = crops.Count;
        float wedgeSize = 360f / count;
        highlighted = Mathf.FloorToInt((angle + wedgeSize * 0.5f) % 360f / wedgeSize);
        if (highlighted >= count) highlighted = 0;
    }

    private void OnGUI()
    {
        if (!open || crops == null || crops.Count == 0) return;

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        int count = crops.Count;

        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = textColor },
            wordWrap = true
        };

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * radius;

            float box = 88f * wedgeIconScale;
            var rect = new Rect(pos.x - box * 0.5f, pos.y - box * 0.5f, box, box);

            GUI.DrawTexture(rect, i == highlighted ? highlightTex : wedgeTex);
            GUI.Label(rect, crops[i].cropName, labelStyle);
        }

        // Center hint
        var hintStyle = new GUIStyle(labelStyle) { fontSize = 12 };
        string hint = highlighted >= 0 && highlighted < count
            ? crops[highlighted].cropName
            : "flick to choose";
        GUI.Label(new Rect(center.x - 80f, center.y - 12f, 160f, 24f), hint, hintStyle);
    }

    private Texture2D SolidTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}