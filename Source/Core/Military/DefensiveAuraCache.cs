using VOE;

namespace EmpireVOE;

/// <summary>
/// Per-settlement cache entry holding nearby garrisoned defensive outposts and the passive military-level
/// bonus they contribute. Each outpost contributes a fraction (<c>defensiveAuraLevelFactor</c>) of its
/// active military level, so the passive shield is always weaker than actually deploying the garrison.
/// Only outposts standing a passive watch count (<see cref="WorldObjectComp_EmpireDefensive.ProvidesAura"/>):
/// one set to auto-defend, deployed on a defense, or regrouping on cooldown has its garrison committed
/// elsewhere and projects no aura.
/// </summary>
public class DefensiveAuraEntry : IOutpostProximityBonus
{
    public readonly List<Outpost_Defensive> outposts = [];
    public readonly double militaryLevelBonus;

    int IOutpostProximityBonus.SourceCount => outposts.Count;
    double IOutpostProximityBonus.Bonus => militaryLevelBonus;

    public DefensiveAuraEntry(WorldSettlementFC settlement)
    {
        foreach (Outpost_Defensive defensive in OutpostProximityUtil.GarrisonedOutpostsNear<Outpost_Defensive>(
                     settlement.Tile, EmpireVOESettings.defensiveAuraRange))
        {
            WorldObjectComp_EmpireDefensive defComp = defensive.GetComponent<WorldObjectComp_EmpireDefensive>();
            if (defComp is not null && !defComp.ProvidesAura) continue;   // committed garrisons project no aura

            outposts.Add(defensive);
            militaryLevelBonus += OutpostBonus(defensive);
        }
    }

    /// <summary>
    /// The militaryBaseLevel bonus a single outpost contributes to each nearby settlement: a fraction
    /// (<c>defensiveAuraLevelFactor</c>) of its active military level. Single source of truth, shared by the
    /// aura sum here, the outpost infobox, and the settlement overview subtab.
    /// </summary>
    public static double OutpostBonus(Outpost_Defensive outpost)
        => OutpostMilitaryUtil.MilitaryLevel(outpost) * EmpireVOESettings.defensiveAuraLevelFactor;
}

/// <summary>
/// Static lazy cache mapping settlement tiles to their nearby defensive-outpost aura data. Invalidated on
/// defensive-outpost roster changes, outpost creation/destruction, aura settings changes, and aura-eligibility
/// flips (auto-defend toggled, defense begun/ended, cooldown set/expired — see
/// <see cref="WorldObjectComp_EmpireDefensive.CompTick"/>).
/// </summary>
public static class DefensiveAuraCache
{
    private static readonly OutpostProximityCache<DefensiveAuraEntry> cache = new(s => new DefensiveAuraEntry(s));

    public static DefensiveAuraEntry GetOrBuild(WorldSettlementFC settlement) => cache.GetOrBuild(settlement);

    /// <summary>
    /// Clears the cache and dirties the settlement stat caches, since the aura feeds militaryBaseLevel
    /// through the cached settlement stat partial.
    /// </summary>
    public static void Invalidate() => cache.Invalidate();
}
