using VOE;

namespace EmpireVOE;

/// <summary>
/// Per-encampment precomputed Medicine data backing the merc heal-rate bonus.
/// </summary>
public class EncampmentData
{
    public readonly Outpost_Encampment encampment;
    public readonly double healRateContribution;

    public EncampmentData(Outpost_Encampment enc)
    {
        encampment = enc;
        List<Pawn> pawns = enc.CapablePawns.ToList();
        if (pawns.Count == 0)
        {
            healRateContribution = 0;
            return;
        }

        foreach (Pawn p in pawns)
        {
            if (p.skills is null) continue;
            int level = p.skills.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
            healRateContribution += VOEFormulas.SkillContribution(
                level, EmpireVOESettings.skillFloor, EmpireVOESettings.encampmentHealRatePerLevel);
        }
    }
}

/// <summary>
/// Per-settlement cache entry containing nearby encampment data.
/// </summary>
public class EncampmentCacheEntry : IOutpostProximityBonus
{
    public readonly List<EncampmentData> encampments;
    public readonly double totalHealRateBonus;

    int IOutpostProximityBonus.SourceCount => encampments.Count;
    double IOutpostProximityBonus.Bonus => totalHealRateBonus;

    public EncampmentCacheEntry(WorldSettlementFC settlement)
    {
        encampments = [];
        totalHealRateBonus = 0;

        foreach (Outpost_Encampment enc in OutpostProximityUtil.GarrisonedOutpostsNear<Outpost_Encampment>(
                     settlement.Tile, EmpireVOESettings.encampmentRange))
        {
            EncampmentData data = new(enc);
            encampments.Add(data);
            totalHealRateBonus += data.healRateContribution;
        }
    }
}

/// <summary>
/// Static lazy cache mapping settlement tiles to their nearby encampment data.
/// Invalidated on encampment pawn roster changes, outpost creation/destruction, and settings changes.
/// </summary>
public static class EncampmentCache
{
    private static readonly OutpostProximityCache<EncampmentCacheEntry> cache = new(s => new EncampmentCacheEntry(s));

    public static EncampmentCacheEntry GetOrBuild(WorldSettlementFC settlement) => cache.GetOrBuild(settlement);

    /// <summary>
    /// Clears the cache and dirties the settlement stat caches, since the heal bonus feeds
    /// mercHealRateMultiplier through the cached settlement stat partial.
    /// </summary>
    public static void Invalidate() => cache.Invalidate();
}
