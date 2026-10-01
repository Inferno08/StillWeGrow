using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Central juice dispatcher. Listens to the dig and tamp events and layers
/// hitstop, camera shake, particles, sound and popups on top of them.
/// Nothing here affects gameplay logic.
/// </summary>
public class ActionFeedback : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private DigMeter digMeter;
    [SerializeField] private PlantTamp plantTamp;

    [Header("Hitstop (seconds, unscaled)")]
    [Tooltip("Brief freeze on impact. The single cheapest way to make a hit feel solid.")]
    [SerializeField] private float perfectHitstop = 0.07f;
    [SerializeField] private float goodHitstop = 0.03f;
    [SerializeField] private float completeHitstop = 0.11f;

    [Header("Camera shake")]
    [Tooltip("Cinemachine Impulse Source. Needs an Impulse Listener on the vcam.")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float perfectShake = 0.35f;
    [SerializeField] private float goodShake = 0.18f;
    [SerializeField] private float weakShake = 0.08f;
    [SerializeField] private float completeShake = 0.6f;

    [Header("Particles")]
    [SerializeField] private Color dirtColor = new Color(0.42f, 0.29f, 0.17f);
    [SerializeField] private Color perfectSparkColor = new Color(1f, 0.93f, 0.5f);
    [SerializeField] private int weakBurst = 6;
    [SerializeField] private int goodBurst = 14;
    [SerializeField] private int perfectBurst = 26;
    [SerializeField] private int completeBurst = 45;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip weakClip;
    [SerializeField] private AudioClip goodClip;
    [SerializeField] private AudioClip perfectClip;
    [SerializeField] private AudioClip completeClip;
    [SerializeField] private AudioClip missClip;
    [Tooltip("Pitch climbs with the combo, so a streak audibly rises.")]
    [SerializeField] private float comboPitchStep = 0.055f;
    [SerializeField] private int comboPitchCap = 8;

    [Header("Popups")]
    [SerializeField] private bool showPopups = true;
    [SerializeField] private float popupRise = 1.4f;
    [SerializeField] private float popupLife = 0.8f;
    [SerializeField] private int popupFontSize = 22;
    [Tooltip("Vertical gap between popups that spawn near each other.")]
    [SerializeField] private float popupStackGap = 0.55f;
    [Tooltip("How close two popups must be to count as stacked.")]
    [SerializeField] private float popupStackRadius = 2f;

    private ParticleSystem dirtBurst;
    private ParticleSystem sparkBurst;
    private Camera cam;
    private Coroutine hitstopRoutine;

    private class PopupText
    {
        public string text;
        public Vector3 worldPos;
        public float stackOffset;
        public float age;
        public Color color;
        public float scale;
    }

    private readonly List<PopupText> popups = new();

    // ---------------------------------------------------------------

    private void Awake()
    {
        cam = Camera.main;
        dirtBurst = BuildParticles("DirtBurst", dirtColor, 0.11f, 3.2f, 0.55f, true);
        sparkBurst = BuildParticles("SparkBurst", perfectSparkColor, 0.07f, 5.5f, 0.4f, false);
    }

    private void OnEnable()
    {
        if (digMeter != null)
        {
            digMeter.OnStrike += HandleStrike;
            digMeter.OnDigComplete += HandleDigComplete;
        }
        if (plantTamp != null)
            plantTamp.OnComplete += HandlePlantComplete;
    }

    private void OnDisable()
    {
        if (digMeter != null)
        {
            digMeter.OnStrike -= HandleStrike;
            digMeter.OnDigComplete -= HandleDigComplete;
        }
        if (plantTamp != null)
            plantTamp.OnComplete -= HandlePlantComplete;
    }

    // ---------------------------------------------------------------

    private void HandleStrike(ActionQuality quality)
    {
        Vector3 pos = digMeter != null ? digMeter.SitePosition : transform.position;
        int combo = digMeter != null ? digMeter.Combo : 0;

        if (quality == ActionQuality.Miss)
        {
            Play(missClip, 0.85f, 0.5f);
            Popup("miss", pos, new Color(0.8f, 0.35f, 0.3f), 0.85f);
            return;
        }

        float pitch = 1f + Mathf.Min(combo, comboPitchCap) * comboPitchStep;

        switch (quality)
        {
            case ActionQuality.Perfect:
                Hitstop(perfectHitstop);
                Shake(perfectShake);
                Burst(dirtBurst, pos, perfectBurst);
                Burst(sparkBurst, pos, 12);
                Play(perfectClip, pitch, 1f);
                Popup(combo > 1 ? $"perfect  x{combo}" : "perfect",
                      pos, perfectSparkColor, 1.25f);
                break;

            case ActionQuality.Good:
                Hitstop(goodHitstop);
                Shake(goodShake);
                Burst(dirtBurst, pos, goodBurst);
                Play(goodClip, pitch, 0.9f);
                Popup(combo > 1 ? $"good  x{combo}" : "good",
                      pos, new Color(0.55f, 0.9f, 0.45f), 1f);
                break;

            case ActionQuality.Weak:
                Shake(weakShake);
                Burst(dirtBurst, pos, weakBurst);
                Play(weakClip, pitch * 0.95f, 0.7f);
                break;
        }
    }

    private void HandleDigComplete()
    {
        Vector3 pos = digMeter != null ? digMeter.SitePosition : transform.position;
        int combo = digMeter != null ? digMeter.Combo : 0;

        // The payoff beat — deliberately bigger than any single strike
        Hitstop(completeHitstop);
        Shake(completeShake);
        Burst(dirtBurst, pos, completeBurst);
        Burst(sparkBurst, pos, 20);
        Play(completeClip, 1f + Mathf.Min(combo, comboPitchCap) * 0.02f, 1f);

        // Spawns above the strike popup that fired this same frame
        Popup("hole dug", pos, new Color(1f, 0.88f, 0.55f), 1.5f);
    }

    private void HandlePlantComplete(ActionQuality quality, float soil)
    {
        Vector3 pos = plantTamp != null ? plantTamp.SitePosition : transform.position;

        switch (quality)
        {
            case ActionQuality.Perfect:
                Hitstop(perfectHitstop);
                Shake(perfectShake * 0.8f);
                Burst(dirtBurst, pos, goodBurst);
                Burst(sparkBurst, pos, 16);
                Play(perfectClip, 1.15f, 1f);
                Popup("firm soil", pos, perfectSparkColor, 1.3f);
                break;

            case ActionQuality.Good:
                Shake(goodShake * 0.7f);
                Burst(dirtBurst, pos, weakBurst);
                Play(goodClip, 1f, 0.85f);
                Popup("planted", pos, new Color(0.55f, 0.9f, 0.45f), 1f);
                break;

            default:
                Shake(weakShake);
                Burst(dirtBurst, pos, 4);
                Play(weakClip, 0.9f, 0.6f);
                Popup(plantTamp != null && plantTamp.ReleasedEarly
                          ? "loose soil" : "packed too hard",
                      pos, new Color(0.85f, 0.65f, 0.35f), 0.9f);
                break;
        }
    }

    // ---------------------------------------------------------------

    private void Hitstop(float duration)
    {
        if (duration <= 0f) return;
        if (hitstopRoutine != null) StopCoroutine(hitstopRoutine);
        hitstopRoutine = StartCoroutine(HitstopRoutine(duration));
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        float previous = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = previous > 0f ? previous : 1f;
        hitstopRoutine = null;
    }

    private void Shake(float force)
    {
        if (impulseSource == null || force <= 0f) return;
        impulseSource.GenerateImpulseWithForce(force);
    }

    private void Burst(ParticleSystem ps, Vector3 pos, int count)
    {
        if (ps == null || count <= 0) return;
        ps.transform.position = pos + Vector3.up * 0.05f;
        ps.Emit(count);
    }

    private void Play(AudioClip clip, float pitch, float volume)
    {
        if (audioSource == null || clip == null) return;
        audioSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.5f);
        audioSource.PlayOneShot(clip, volume);
    }

    private void Popup(string text, Vector3 worldPos, Color color, float scale)
    {
        if (!showPopups) return;

        // Stack above any young popup already sitting near this spot,
        // so a strike and a completion in the same frame don't collide.
        float offset = 0f;
        foreach (var existing in popups)
        {
            if (existing.age > popupLife * 0.5f) continue;
            if ((existing.worldPos - worldPos).sqrMagnitude > popupStackRadius * popupStackRadius)
                continue;

            offset = Mathf.Max(offset, existing.stackOffset + popupStackGap);
        }

        popups.Add(new PopupText
        {
            text = text,
            worldPos = worldPos,
            stackOffset = offset,
            age = 0f,
            color = color,
            scale = scale
        });
    }

    private void Update()
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            popups[i].age += Time.unscaledDeltaTime;
            if (popups[i].age >= popupLife) popups.RemoveAt(i);
        }
    }

    private void OnGUI()
    {
        if (!showPopups || cam == null || popups.Count == 0) return;

        foreach (var p in popups)
        {
            float t = p.age / popupLife;

            Vector3 world = p.worldPos
                          + Vector3.up * (p.stackOffset + popupRise * EaseOut(t));
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) continue;

            // Quick pop-in, slow fade-out
            float pop = t < 0.15f ? Mathf.Lerp(0.5f, 1.15f, t / 0.15f)
                                  : Mathf.Lerp(1.15f, 1f, Mathf.Min((t - 0.15f) / 0.2f, 1f));
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(popupFontSize * p.scale * pop),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            var rect = new Rect(screen.x - 120f, Screen.height - screen.y - 20f, 240f, 40f);

            // Cheap outline so it reads over any background
            style.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.6f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), p.text, style);

            style.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, alpha);
            GUI.Label(rect, p.text, style);
        }
    }

    private static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 2f);

    // ---------------------------------------------------------------

    private ParticleSystem BuildParticles(string psName, Color color, float size,
                                          float speed, float life, bool gravity)
    {
        var go = new GameObject(psName);
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();

        var main = ps.main;
        main.startColor = color;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.3f);
        main.gravityModifier = gravity ? 1.6f : 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 400;

        var emission = ps.emission;
        emission.enabled = false;   // emit manually via Emit()

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.15f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = color;
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        return ps;
    }
}