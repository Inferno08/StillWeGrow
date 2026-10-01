using UnityEngine;

/// <summary>Maps a 0..1 quality score to an F..S++ grade and colour. Farming
/// level sets the MAXIMUM grade reachable — early on everything caps at F.</summary>
public static class QualityGrade
{
    // Grade ladder, ascending. Index 0 = F (worst).
    public static readonly string[] Ladder =
        { "F", "E", "D", "C", "B", "A", "S", "S+", "S++" };

    // Quality threshold to reach each ladder index (same length as Ladder).
    private static readonly float[] Thresholds =
        { 0.00f, 0.15f, 0.28f, 0.42f, 0.55f, 0.68f, 0.80f, 0.90f, 0.97f };

    /// <summary>Ladder index a raw quality would earn, ignoring level cap.</summary>
    public static int RawIndex(float quality)
    {
        quality = Mathf.Clamp01(quality);
        int idx = 0;
        for (int i = 0; i < Thresholds.Length; i++)
            if (quality >= Thresholds[i]) idx = i;
        return idx;
    }

    /// <summary>0..1 how far up the ladder a grade index sits (F=0, S++=1).</summary>
    public static float GradeStrength(int gradeIndex)
    {
        return Ladder.Length <= 1 ? 1f
             : Mathf.Clamp01((float)gradeIndex / (Ladder.Length - 1));
    }

    public static int GradeIndex(float quality, float level01)
    {
        return Mathf.Min(RawIndex(quality), LevelCapIndex(level01));
    }

    /// <summary>
    /// The highest ladder index the player's farming level (0..1) permits.
    /// At level 0 only F (index 0); climbs to S++ (top) near mastery.
    /// </summary>
    public static int LevelCapIndex(float level01)
    {
        level01 = Mathf.Clamp01(level01);
        // Map 0..1 across the ladder. floor so you fully "earn" each new tier.
        return Mathf.Clamp(Mathf.FloorToInt(level01 * (Ladder.Length - 1) + 0.0001f),
                           0, Ladder.Length - 1);
    }

    /// <summary>Final grade: quality's earned grade, capped by farming level.</summary>
    public static string ToGrade(float quality, float level01)
    {
        int capped = Mathf.Min(RawIndex(quality), LevelCapIndex(level01));
        return Ladder[capped];
    }

    public static Color GradeColor(float quality, float level01)
    {
        int capped = Mathf.Min(RawIndex(quality), LevelCapIndex(level01));
        return IndexColor(capped);
    }

    private static Color IndexColor(int idx)
    {
        switch (idx)
        {
            case 8: return new Color(1f, 0.85f, 0.2f);   // S++ gold
            case 7: return new Color(1f, 0.8f, 0.35f);   // S+ gold-ish
            case 6: return new Color(0.7f, 0.4f, 1f);    // S purple
            case 5: return new Color(0.3f, 0.7f, 1f);    // A blue
            case 4: return new Color(0.3f, 0.9f, 0.4f);  // B green
            case 3: return new Color(0.9f, 0.9f, 0.4f);  // C yellow
            case 2: return new Color(0.95f, 0.6f, 0.3f); // D orange
            case 1: return new Color(0.9f, 0.5f, 0.4f);  // E red-orange
            default: return new Color(0.9f, 0.4f, 0.4f); // F red
        }
    }
}