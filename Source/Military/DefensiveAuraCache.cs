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
                float distance = Find.WorldGrid.ApproxDistanceInTiles(defensive.Tile, settlement.Tile);
                if (distance > EmpireVOESettings.defensiveAuraRange) continue;

                outposts.Add(defensive);
                militaryLevelBonus += OutpostMilitaryUtil.MilitaryLevel(defensive) * EmpireVOESettings.defensiveAuraLevelFactor;
            }
        }
    }

    /// <summary>
    /// Static lazy cache mapping settlement tiles to their nearby defensive-outpost aura data. Invalidated on
    /// defensive-outpost roster changes, outpost creation/destruction, and aura settings changes.
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
