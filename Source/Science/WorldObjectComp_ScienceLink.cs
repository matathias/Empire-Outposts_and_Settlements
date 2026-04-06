using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_ScienceLink : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_ScienceLink()
        {
            compClass = typeof(WorldObjectComp_ScienceLink);
        }
    }

    /// <summary>
    /// Provides an additive Research production bonus to the settlement based on a
    /// linked Outpost_Science's pawn count and Intellectual skill.
    /// Stateless — all link data lives in WorldComponent_VOETracker.
    /// </summary>
    public class WorldObjectComp_ScienceLink : WorldObjectComp, IResourceProductionModifier
    {
        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            if (EmpireVOESettings.disableIntegration) return 0;
            if (resource?.def is null || resource.def != ResourceTypeDefOf.RTD_Research) return 0;
            return CalculateBonus();
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            return 1.0;
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            double bonus = GetResourceAdditiveModifier(resource);
            if (bonus > 0)
                return "VOE_ScienceLinkBonusDesc".Translate() + ": +" + bonus.ToString("F2");
            return null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            return null;
        }

        private double CalculateBonus()
        {
            WorldSettlementFC settlement = parent as WorldSettlementFC;
            if (settlement is null) return 0;

            Outpost_Science linked = WorldComponent_VOETracker.GetLinkedScienceOutpost(settlement);
            if (linked is null || linked.Destroyed) return 0;

            List<Pawn> pawns = linked.CapablePawns.ToList();
            if (pawns.Count == 0) return 0;

            double bonus = pawns.Count * EmpireVOESettings.scienceBonusPerPawn;

            if (EmpireVOESettings.scienceSkillScaling)
            {
                double avgIntellectual = pawns
                    .Where(p => p.skills != null)
                    .Select(p => (double)p.skills.GetSkill(SkillDefOf.Intellectual).Level)
                    .DefaultIfEmpty(0)
                    .Average();
                bonus *= avgIntellectual / 10.0;
            }

            return bonus;
        }
    }
}
