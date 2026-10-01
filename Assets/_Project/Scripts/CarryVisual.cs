using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns held crops as a loose armful/bunch — clustered together with organic
/// spread and slight height variation, like carrying vegetables in your arms.
/// </summary>
public class CarryVisual : MonoBehaviour
{
    [SerializeField] private CarryInventory inventory;

    [Header("Where crops sit")]
    [Tooltip("Anchor the bunch hangs from. Position with local position only, keep scale 1,1,1.")]
    [SerializeField] private Transform holdAnchor;
    [Tooltip("Centre of the bunch relative to the anchor (world units).")]
    [SerializeField] private Vector3 bunchCentre = new Vector3(0f, 0.2f, 0.35f);

    [Header("Bunch shape")]
    [Tooltip("How wide the bunch spreads sideways (world units).")]
    [SerializeField] private float spreadWidth = 0.18f;
    [Tooltip("How much it spreads front-to-back (world units).")]
    [SerializeField] private float spreadDepth = 0.12f;
    [Tooltip("How much crops rise as the bunch grows (world units per crop, roughly).")]
    [SerializeField] private float heightBuild = 0.05f;
    [Tooltip("Max random tilt per crop, degrees — organic, not aligned.")]
    [SerializeField] private float tiltJitter = 25f;
    [Tooltip("Rendered height of each carried crop in world units.")]
    [SerializeField] private float propWorldHeight = 0.35f;

    private readonly List<GameObject> spawned = new();
    private bool subscribed;

    private void OnEnable() { TrySubscribe(); }
    private void Start() { TrySubscribe(); }

    private void OnDisable()
    {
        if (subscribed && inventory != null)
        {
            inventory.OnChanged -= Rebuild;
            subscribed = false;
        }
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        if (inventory == null) inventory = CarryInventory.Instance;
        if (inventory == null) return;

        inventory.OnChanged += Rebuild;
        subscribed = true;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();

        if (inventory == null || holdAnchor == null) return;

        var flat = new List<CropData>();
        foreach (var kv in inventory.Held)
            for (int i = 0; i < kv.Value; i++)
                flat.Add(kv.Key);

        int count = flat.Count;

        // Deterministic per-slot randomness so the bunch doesn't reshuffle
        // wildly every rebuild — each slot index gets a stable-ish offset.
        for (int i = 0; i < count; i++)
        {
            GameObject prop = ResolveProp(flat[i]);
            if (prop == null) continue;

            var rand = new System.Random(i * 7919 + 13);
            float rx = (float)(rand.NextDouble() * 2 - 1);
            float rz = (float)(rand.NextDouble() * 2 - 1);
            float ry = (float)rand.NextDouble();

            // Cluster: spread out from centre, rising a little as more pile in
            Vector3 localOffset = bunchCentre + new Vector3(
                rx * spreadWidth,
                ry * heightBuild + i * heightBuild * 0.4f,
                rz * spreadDepth);

            var go = CropVisualUtil.SpawnAtSize(prop, holdAnchor, localOffset, propWorldHeight);

            // Organic tilt so they lie against each other, not all upright
            float tiltX = (float)(rand.NextDouble() * 2 - 1) * tiltJitter;
            float tiltZ = (float)(rand.NextDouble() * 2 - 1) * tiltJitter;
            go.transform.rotation = go.transform.rotation *
                                    Quaternion.Euler(tiltX, UnityEngine.Random.Range(0f, 360f), tiltZ);

            spawned.Add(go);
        }
    }

    private GameObject ResolveProp(CropData crop)
    {
        if (crop.carryProp != null) return crop.carryProp;
        if (crop.stagePrefabs != null && crop.stagePrefabs.Length > 0)
            return crop.stagePrefabs[crop.stagePrefabs.Length - 1];
        return null;
    }
}