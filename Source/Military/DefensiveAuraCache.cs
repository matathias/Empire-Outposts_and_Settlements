using System.Collections.Generic;
using FactionColonies;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Per-settlement cache entry holding nearby garrisoned defensive outposts and the passive military-level
    /// bonus they contribute. Each outpost contributes a fraction (<c>defensiveAuraLevelFactor</c>) of its
    /// active military level, so the passive shield is always weaker than actually deploying the garrison.
    /// Only outposts standing a passive watch count (<see cref="WorldObjectComp_EmpireDefensive.ProvidesAura"/>):
    /// one set to auto-defend, deployed on a defense, or regrouping on cooldown has its garrison committed
    /// elsewhere and projects no aura.
    /// </summary>
    public class DefensiveAuraEntry
    {
        public readonly List<Outpost_Defensive> outposts = new List<Outpost_Defensive>();
        public readonly double militaryLevelBonus;

        public DefensiveAuraEntry(WorldSettlementFC settlement)
        {
            foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
            {
                if (!(wo is Outpost_Defensive defensive)) continue;
                if (defensive.PawnCount == 0) continue;
                WorldObjectComp_EmpireDefensive defComp = defensive.GetComponent<WorldObjectComp_EmpireDefensive>();
                if (defComp is object && !defComp.ProvidesAura) continue;   // committed garrisons project no aura
                float distance = Find.WorldGrid.ApproxDistanceInTiles(defensive.Tile, settlement.Tile);
                if (distance > EmpireVOESettings.defensiveAuraRange) continue;

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
        private static readonly Dictionary<PlanetTile, DefensiveAuraEntry> cache = new Dictionary<PlanetTile, DefensiveAuraEntry>();

        public static DefensiveAuraEntry GetOrBuild(WorldSettlementFC settlement)
        {
            if (settlement is null) return null;
            if (cache.TryGetValue(settlement.Tile, out DefensiveAuraEntry entry))
                return entry;

            entry = new DefensiveAuraEntry(settlement);
            cache[settlement.Tile] = entry;
            return entry;
        }

        public static void Invalidate()
        {
            cache.Clear();
            // The aura feeds militaryBaseLevel through the cached settlement stat partial, so dirty the
            // settlement stat caches too (the IStatModifierProvider contribution is baked in at cache time).
            FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
        }
    }
}
