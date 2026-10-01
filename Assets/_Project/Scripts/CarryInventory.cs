using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player is carrying in their hands. Limited capacity, so you have
/// to make trips to storage. Holds crops as (CropData, count) stacks.
/// </summary>
public class CarryInventory : MonoBehaviour
{
    public static CarryInventory Instance { get; private set; }

    [SerializeField] private int capacity = 5;

    private readonly Dictionary<CropData, int> held = new();

    public int Capacity => capacity;
    public int Count
    {
        get
        {
            int total = 0;
            foreach (var kv in held) total += kv.Value;
            return total;
        }
    }
    public bool IsFull => Count >= capacity;
    public IReadOnlyDictionary<CropData, int> Held => held;

    public event Action OnChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    /// <summary>Try to pick up one crop. Returns false if hands are full.</summary>
    public bool TryAdd(CropData crop, int amount = 1)
    {
        if (crop == null) return false;
        if (Count + amount > capacity) return false;

        held.TryGetValue(crop, out int cur);
        held[crop] = cur + amount;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Remove up to 'amount' of a crop. Returns how many were actually removed.</summary>
    public int Remove(CropData crop, int amount = 1)
    {
        if (crop == null || !held.TryGetValue(crop, out int cur)) return 0;

        int removed = Mathf.Min(cur, amount);
        int left = cur - removed;
        if (left > 0) held[crop] = left;
        else held.Remove(crop);

        OnChanged?.Invoke();
        return removed;
    }

    /// <summary>The first crop type currently held, or null. Handy for "eat one".</summary>
    public CropData FirstHeld()
    {
        foreach (var kv in held) return kv.Key;
        return null;
    }

    public int CountOf(CropData crop) =>
        crop != null && held.TryGetValue(crop, out int c) ? c : 0;

    public void Clear()
    {
        held.Clear();
        OnChanged?.Invoke();
    }
}