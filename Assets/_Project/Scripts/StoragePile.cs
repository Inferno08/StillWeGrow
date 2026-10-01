using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A storage point. Remembers how many of each crop it holds; depositing adds
/// to the pile, and you can eat straight from it. Spawns crop props on the mat
/// so the pile visibly fills up and stacks, each at a fixed world height even
/// though the mat itself is scaled flat.
/// </summary>
public class StoragePile : MonoBehaviour
{
    [Header("Contents")]
    [SerializeField] private int capacity = 60;

    [Header("Eating")]
    [SerializeField] private float hungerPerCrop = 25f;

    [Header("Visual stacking")]
    [Tooltip("Anchor the pile builds from. Can be a child of the flat mat.")]
    [SerializeField] private Transform pileAnchor;
    [SerializeField] private int columns = 4;
    [SerializeField] private int rows = 4;
    [Tooltip("Spacing between crops (world units).")]
    [SerializeField] private float spacing = 0.4f;
    [Tooltip("Height added per stacked layer (world units).")]
    [SerializeField] private float layerHeight = 0.3f;
    [Tooltip("Rendered height of each crop in world units.")]
    [SerializeField] private float propWorldHeight = 0.4f;
    [SerializeField] private float jitter = 0.05f;

    private readonly Dictionary<CropData, int> stored = new();
    private readonly List<GameObject> props = new();

    public int Total
    {
        get { int t = 0; foreach (var kv in stored) t += kv.Value; return t; }
    }
    public IReadOnlyDictionary<CropData, int> Stored => stored;
    public bool IsFull => Total >= capacity;

    public event Action OnChanged;

    public int DepositAll(CarryInventory hands)
    {
        if (hands == null) return 0;

        int moved = 0;
        var snapshot = new List<KeyValuePair<CropData, int>>(hands.Held);

        foreach (var kv in snapshot)
        {
            int room = capacity - Total;
            if (room <= 0) break;

            int take = Mathf.Min(kv.Value, room);
            int actuallyRemoved = hands.Remove(kv.Key, take);

            stored.TryGetValue(kv.Key, out int cur);
            stored[kv.Key] = cur + actuallyRemoved;
            moved += actuallyRemoved;
        }

        if (moved > 0) { RebuildVisual(); OnChanged?.Invoke(); }
        return moved;
    }

    public bool EatOne()
    {
        CropData crop = FirstStored();
        if (crop == null) return false;

        Remove(crop, 1);

        if (SurvivalStats.Instance != null)
            SurvivalStats.Instance.Eat(hungerPerCrop);

        return true;
    }

    public int Remove(CropData crop, int amount = 1)
    {
        if (crop == null || !stored.TryGetValue(crop, out int cur)) return 0;

        int removed = Mathf.Min(cur, amount);
        int left = cur - removed;
        if (left > 0) stored[crop] = left; else stored.Remove(crop);

        RebuildVisual();
        OnChanged?.Invoke();
        return removed;
    }

    public CropData FirstStored()
    {
        foreach (var kv in stored) return kv.Key;
        return null;
    }

    private void RebuildVisual()
    {
        foreach (var go in props) if (go != null) Destroy(go);
        props.Clear();

        Transform anchor = pileAnchor != null ? pileAnchor : transform;
        int perLayer = Mathf.Max(1, columns * rows);

        int index = 0;
        foreach (var kv in stored)
        {
            GameObject prop = ResolveProp(kv.Key);
            if (prop == null) continue;

            for (int i = 0; i < kv.Value; i++)
            {
                int layer = index / perLayer;
                int inLayer = index % perLayer;
                int col = inLayer % columns;
                int row = inLayer / columns;

                float x = (col - (columns - 1) * 0.5f) * spacing;
                float z = (row - (rows - 1) * 0.5f) * spacing;
                float y = layer * layerHeight;

                Vector3 localOffset = new Vector3(
                    x + UnityEngine.Random.Range(-jitter, jitter),
                    y,
                    z + UnityEngine.Random.Range(-jitter, jitter));

                var go = CropVisualUtil.SpawnAtSize(prop, anchor, localOffset, propWorldHeight);
                props.Add(go);
                index++;
            }
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