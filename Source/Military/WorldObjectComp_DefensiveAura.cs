using FactionColonies;
using RimWorld.Planet;
using Verse;
using RimWorld;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_DefensiveAura : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_DefensiveAura()
        {
            compClass = typeof(WorldObjectComp_DefensiveAura);
        }
    }

    /// <summary>
    /// Passive "shield" tier of the defensive-outpost integration: nearby garrisoned <c>Outpost_Defensive</c>
    /// add to this settlement's <see cref="FCStatDefOf.militaryBaseLevel"/>, stiffening its auto-resolved
    /// defense when raided — even when the outpost never deploys. Complements the active
    /// <see cref="DefensiveAutoDefender"/>. Reads from <see cref="DefensiveAuraCache"/>; mirrors
    /// <see cref="WorldObjectComp_EncampmentBonus"/>.
    /// </summary>
    public class WorldObjectComp_DefensiveAura : WorldObjectComp, IStatModifierProvider
    {
        public double GetStatModifier(FCStatDef stat)
        {
            if (!EmpireVOESettings.DefensiveAuraActive) return stat.IdentityValue;
            if (stat != FCStatDefOf.militaryBaseLevel) return stat.IdentityValue;

            WorldSettlementFC settlement = parent as WorldSettlementFC;
            if (settlement is null) return stat.IdentityValue;

            DefensiveAuraEntry entry = DefensiveAuraCache.GetOrBuild(settlement);
            if (entry is null || entry.outposts.Count == 0) return stat.IdentityValue;

            return stat.IdentityValue + entry.militaryLevelBonus;
        }

        public string GetStatModifierDesc(FCStatDef stat)
        {
            if (!EmpireVOESettings.DefensiveAuraActive) return null;
            if (stat != FCStatDefOf.militaryBaseLevel) return null;

            WorldSettlementFC settlement = parent as WorldSettlementFC;
            if (settlement is null) return null;

            DefensiveAuraEntry entry = DefensiveAuraCache.GetOrBuild(settlement);
            if (entry is null || entry.outposts.Count == 0) return null;

            return TextUtil.AdditiveBonusLine(
                entry.militaryLevelBonus,
                "FCVOE_DefensiveAuraSource".Translate(entry.outposts.Count)) + "\n";
        }
    }
}
