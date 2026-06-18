using FactionColonies;
using RimWorld;
using UnityEngine;

namespace EmpireVOE
{
    /// <summary>
    /// Optional "lightning rod" half of the defensive aura (off by default): a settlement shielded by nearby
    /// defensive outposts becomes a less attractive raid target, shifting the weighted roll toward other
    /// targets — including the defensive outposts themselves, which are already registered as raid targets via
    /// <see cref="OutpostRaidTarget"/>. Singleton registered through the EmpireRegistry facade.
    /// </summary>
    public class DefensiveAuraRaidWeightProvider : IRaidWeightProvider
    {
        public static readonly DefensiveAuraRaidWeightProvider Instance = new DefensiveAuraRaidWeightProvider();

        public float GetSettlementRaidWeight(WorldSettlementFC settlement, Faction attackingFaction)
        {
            if (!EmpireVOESettings.DefensiveAuraDivertActive || settlement is null) return 1f;

            DefensiveAuraEntry entry = DefensiveAuraCache.GetOrBuild(settlement);
            if (entry is null || entry.outposts.Count == 0) return 1f;

            // More passive defense -> lower weight, clamped to a floor so the settlement is never excluded.
            float weight = 1f / (1f + (float)entry.militaryLevelBonus * EmpireVOESettings.defensiveAuraDivertScale);
            return Mathf.Max(EmpireVOESettings.defensiveAuraWeightFloor, weight);
        }
    }
}
