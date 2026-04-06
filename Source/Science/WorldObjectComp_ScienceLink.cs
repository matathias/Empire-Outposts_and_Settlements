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

            double bonus = 0;
            foreach (Pawn pawn in linked.CapablePawns)
            {
                if (pawn.skills is null) continue;
                SkillRecord skill = pawn.skills.GetSkill(SkillDefOf.Intellectual);
                if (skill is null) continue;
                int level = skill.Level;
                if (level >= EmpireVOESettings.skillFloor)
                    bonus += level * EmpireVOESettings.additivePerLevel;
            }

            return bonus;
        }
    }
}
