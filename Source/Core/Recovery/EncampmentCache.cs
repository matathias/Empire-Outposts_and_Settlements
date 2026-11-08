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
public class EncampmentCacheEntry
{
    public readonly List<EncampmentData> encampments;
    public readonly double totalHealRateBonus;

    public EncampmentCacheEntry(WorldSettlementFC settlement)
    {
        encampments = [];
        totalHealRateBonus = 0;

        foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
        {
            if (wo is not Outpost_Encampment enc) continue;
            if (enc.PawnCount == 0) continue;
            // Infinite across planet layers: the encampment heal bonus doesn't cross them
            float distance = enc.Tile.ApproxDistanceSameLayer(settlement.Tile);
            if (distance > EmpireVOESettings.encampmentRange) continue;

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
    private static readonly Dictionary<PlanetTile, EncampmentCacheEntry> cache = [];

    public static EncampmentCacheEntry GetOrBuild(WorldSettlementFC settlement)
    {
        if (settlement is null) return null;
        if (cache.TryGetValue(settlement.Tile, out EncampmentCacheEntry entry))
            return entry;

        entry = new EncampmentCacheEntry(settlement);
        cache[settlement.Tile] = entry;
        return entry;
    }

    public static void Invalidate()
    {
        cache.Clear();
        // The heal bonus feeds mercHealRateMultiplier through the cached settlement stat partial
        // (WorldObjectComp_EncampmentBonus), so dirty the settlement stat caches too — otherwise
        // settlements serve the stale heal rate until an unrelated invalidation.
        FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
    }
}
