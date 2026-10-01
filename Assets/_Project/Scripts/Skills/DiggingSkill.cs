using System;
using UnityEngine;

public class DiggingSkill : MonoBehaviour
{
    /// <summary>Convenient static access to the active skill (set in Awake).</summary>
    public static DiggingSkill Current { get; private set; }

    [Header("Level")]
    [SerializeField] private int level = 1;
    [SerializeField] private int maxLevel = 10;

    [Header("XP")]
    [SerializeField] private int xp = 0;
    [Tooltip("XP needed for level 2. Each level costs this much more.")]
    [SerializeField] private int xpPerLevel = 40;

    [Header("XP rewards per strike")]
    [SerializeField] private int perfectXP = 6;
    [SerializeField] private int goodXP = 3;
    [SerializeField] private int weakXP = 1;

    public int Level => level;
    public int MaxLevel => maxLevel;
    public int XP => xp;

    /// <summary>Earned during the current action, not banked until it completes.</summary>
    public int PendingXP { get; private set; }

    /// <summary>0 at level 1, 1 at max level. Drives how much easier digging gets.</summary>
    public float Mastery => maxLevel <= 1
        ? 1f
        : Mathf.Clamp01((level - 1f) / (maxLevel - 1f));

    public int XPForNextLevel => level >= maxLevel ? 0 : level * xpPerLevel;

    public event Action<int> OnLevelUp;

    private void Awake()
    {
        if (Current == null) Current = this;
    }

    public int XPFor(ActionQuality quality) => quality switch
    {
        ActionQuality.Perfect => perfectXP,
        ActionQuality.Good => goodXP,
        ActionQuality.Weak => weakXP,
        _ => 0
    };

    /// <summary>Bank XP toward the current action. Nothing is awarded until commit.</summary>
    public void AddPendingXP(ActionQuality quality)
    {
        PendingXP += XPFor(quality);
    }

    /// <summary>Action finished — pay out everything earned during it.</summary>
    public void CommitPendingXP()
    {
        int amount = PendingXP;
        PendingXP = 0;
        AddXP(amount);
    }

    /// <summary>Action abandoned — the effort is lost.</summary>
    public void DiscardPendingXP()
    {
        PendingXP = 0;
    }

    public void AddXP(int amount)
    {
        if (amount <= 0 || level >= maxLevel) return;

        xp += amount;

        while (level < maxLevel && xp >= XPForNextLevel)
        {
            xp -= XPForNextLevel;
            level++;
            OnLevelUp?.Invoke(level);
            Debug.Log($"Digging is now level {level}.");
        }

        if (level >= maxLevel) xp = 0;
    }

    /// <summary>Debug/testing helper.</summary>
    public void SetLevel(int newLevel)
    {
        level = Mathf.Clamp(newLevel, 1, maxLevel);
        xp = 0;
        PendingXP = 0;
    }
}