using System.Collections.Generic;
using FactionColonies;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_OutpostBonus : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_OutpostBonus()
        {
            compClass = typeof(WorldObjectComp_OutpostBonus);
        }
    }

    /// <summary>
    /// Stores persistent per-resource production bonuses earned when an outpost is converted into a
    /// settlement: a skill-derived additive per resource (when the Specialists submod is NOT loaded)
    /// plus, for Town-derived settlements, a flat additive applied to every resource.
    /// Implements IResourceProductionModifier so it's automatically picked up by ResourceFC's formula.
    /// </summary>
    public class WorldObjectComp_OutpostBonus : WorldObjectComp, IResourceProductionModifier
    {
        private Dictionary<string, double> resourceBonuses = new Dictionary<string, double>();
        private double townFlatAdditive;

        public void SetBonus(string resourceDefName, double value)
        {
            if (value > 0)
            {
                resourceBonuses[resourceDefName] = value;
            }
        }

        /// <summary>Flat additive applied to every resource for Town-derived settlements.</summary>
        public void SetTownFlatAdditive(double value)
        {
            if (value > 0)
            {
                townFlatAdditive = value;
            }
        }

        public bool HasAnyBonus => resourceBonuses.Count > 0 || townFlatAdditive > 0;

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            if (resource?.def is null) return 0;
            double bonus = townFlatAdditive;
            if (resourceBonuses.TryGetValue(resource.def.defName, out double skillBonus))
                bonus += skillBonus;
            return bonus;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            return 1.0;
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            double bonus = GetResourceAdditiveModifier(resource);
            if (bonus > 0)
                return "VOE_OutpostBonusDesc".Translate() + ": +" + bonus.ToString("F2");
            return null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            return null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref resourceBonuses, "outpostBonuses", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref townFlatAdditive, "townFlatAdditive", 0.0);
            if (resourceBonuses == null)
            {
                resourceBonuses = new Dictionary<string, double>();
            }
        }
    }
}
