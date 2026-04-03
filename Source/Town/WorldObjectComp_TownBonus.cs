using System.Collections.Generic;
using FactionColonies;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_TownBonus : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_TownBonus()
        {
            compClass = typeof(WorldObjectComp_TownBonus);
        }
    }

    /// <summary>
    /// Stores persistent per-resource production bonuses from Town pawn skills.
    /// Only populated when the Specialists submod is NOT loaded and the Town's pawns
    /// are converted into skill-based bonuses instead of assigned as specialists.
    /// Implements IResourceProductionModifier so it's automatically picked up by
    /// ResourceFC's production formula.
    /// </summary>
    public class WorldObjectComp_TownBonus : WorldObjectComp, IResourceProductionModifier
    {
        private Dictionary<string, double> resourceBonuses = new Dictionary<string, double>();

        public void SetBonus(string resourceDefName, double value)
        {
            if (value > 0)
            {
                resourceBonuses[resourceDefName] = value;
            }
        }

        public bool HasAnyBonus
        {
            get { return resourceBonuses.Count > 0; }
        }

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            if (resource == null || resource.def == null) return 0;
            double bonus;
            if (resourceBonuses.TryGetValue(resource.def.defName, out bonus))
                return bonus;
            return 0;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            return 1.0;
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            if (resource == null || resource.def == null) return null;
            double bonus;
            if (resourceBonuses.TryGetValue(resource.def.defName, out bonus) && bonus > 0)
                return "VOE_TownBonusDesc".Translate() + ": +" + bonus.ToString("F2");
            return null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            return null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref resourceBonuses, "townBonuses", LookMode.Value, LookMode.Value);
            if (resourceBonuses == null)
            {
                resourceBonuses = new Dictionary<string, double>();
            }
        }
    }
}
