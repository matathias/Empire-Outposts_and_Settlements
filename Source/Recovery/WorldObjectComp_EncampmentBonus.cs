using RimWorld;
using RimWorld.Planet;
using FactionColonies;
using Verse;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_EncampmentBonus : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_EncampmentBonus()
        {
            compClass = typeof(WorldObjectComp_EncampmentBonus);
        }
    }

    /// <summary>
    /// Provides a merc heal rate multiplier bonus to the settlement based on nearby
    /// Outpost_Encampment Medicine skill, using the same scaling as Specialists.
    /// Reads from EncampmentCache for performance.
    /// </summary>
    public class WorldObjectComp_EncampmentBonus : WorldObjectComp, IStatModifierProvider
    {
        public double GetStatModifier(FCStatDef stat)
        {
            if (!EmpireVOESettings.EncampmentActive) return stat.IdentityValue;
            if (stat != FCStatDefOf.mercHealRateMultiplier) return stat.IdentityValue;

            WorldSettlementFC settlement = parent as WorldSettlementFC;
            if (settlement is null) return stat.IdentityValue;

            EncampmentCacheEntry entry = EncampmentCache.GetOrBuild(settlement);
            if (entry is null || entry.encampments.Count == 0) return stat.IdentityValue;

            return stat.IdentityValue + entry.totalHealRateBonus;
        }

        public string GetStatModifierDesc(FCStatDef stat)
        {
            if (!EmpireVOESettings.EncampmentActive) return null;
            if (stat != FCStatDefOf.mercHealRateMultiplier) return null;

            WorldSettlementFC settlement = parent as WorldSettlementFC;
            if (settlement is null) return null;

            EncampmentCacheEntry entry = EncampmentCache.GetOrBuild(settlement);
            if (entry is null || entry.encampments.Count == 0) return null;

            return TextUtil.MultiplierBonusLine(
                stat.IdentityValue + entry.totalHealRateBonus,
                "VOE_EncampmentHealRateSource".Translate(entry.encampments.Count)) + "\n";
        }
    }
}
