using Outposts;

namespace EmpireVOE;

/// <summary>
/// A per-settlement bonus built from the outposts near it: how many outposts contribute, and the summed
/// bonus they add to the settlement's stat.
/// </summary>
public interface IOutpostProximityBonus
{
    int SourceCount { get; }
    double Bonus { get; }
}

/// <summary>
/// Static lazy cache mapping settlement tiles to an entry built from the outposts near that settlement.
/// The entries feed settlement stats through <see cref="IStatModifierProvider"/>, whose contribution is
/// baked into the cached settlement stat partial, so <see cref="Invalidate"/> also dirties every settlement
/// stat cache; otherwise settlements would serve a stale bonus until an unrelated invalidation.
/// </summary>
public sealed class OutpostProximityCache<TEntry> where TEntry : class
{
    private readonly Dictionary<PlanetTile, TEntry> cache = [];
    private readonly Func<WorldSettlementFC, TEntry> build;

    public OutpostProximityCache(Func<WorldSettlementFC, TEntry> build)
    {
        this.build = build;
    }

    public TEntry GetOrBuild(WorldSettlementFC settlement)
    {
        if (settlement is null) return null;
        if (cache.TryGetValue(settlement.Tile, out TEntry entry))
            return entry;

        entry = build(settlement);
        cache[settlement.Tile] = entry;
        return entry;
    }

    public void Invalidate()
    {
        cache.Clear();
        FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
    }
}

public static class OutpostProximityUtil
{
    /// <summary>
    /// Every garrisoned (PawnCount &gt; 0) outpost of type <typeparamref name="TOutpost"/> on the same planet
    /// layer as <paramref name="tile"/> and within <paramref name="range"/> of it. Outposts on other layers
    /// never count: the distance across layers is infinite.
    /// </summary>
    public static IEnumerable<TOutpost> GarrisonedOutpostsNear<TOutpost>(PlanetTile tile, float range)
        where TOutpost : Outpost
    {
        foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
        {
            if (wo is not TOutpost outpost) continue;
            if (outpost.PawnCount == 0) continue;
            if (outpost.Tile.ApproxDistanceSameLayer(tile) > range) continue;
            yield return outpost;
        }
    }
}
