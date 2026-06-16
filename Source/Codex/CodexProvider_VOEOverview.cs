using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Live "Live Data" panel for the Outpost Integration overview entry. Shows
    /// which integration features are currently active and a tally of the outposts
    /// that exist in the world, grouped by type.
    /// </summary>
    public class CodexProvider_VOEOverview : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (EmpireVOESettings.disableIntegration)
                return "VOE_CodexOverviewDisabled".Translate();

            string result = "VOE_CodexOverviewFeatures".Translate() + "\n";
            result += FeatureLine("VOE_EnableMilitary", EmpireVOESettings.MilitaryActive);
            result += FeatureLine("VOE_EnableDelivery", EmpireVOESettings.DeliveryActive);
            result += FeatureLine("VOE_EnableFinancing", EmpireVOESettings.FinancingActive);
            result += FeatureLine("VOE_EnableScienceLink", EmpireVOESettings.ScienceLinkActive);
            result += FeatureLine("VOE_EnableEncampment", EmpireVOESettings.EncampmentActive);
            result += FeatureLine("VOE_EnableTownConversion", EmpireVOESettings.TownConversionActive);
            result += FeatureLine("VOE_EnableRoads", EmpireVOESettings.RoadsActive);
            result += "\n";

            List<Outpost> outposts = Find.WorldObjects?.AllWorldObjects?.OfType<Outpost>().ToList()
                                     ?? new List<Outpost>();
            if (outposts.Count == 0)
                return result + "VOE_CodexOverviewNoOutposts".Translate();

            result += "VOE_CodexOverviewOutposts".Translate(outposts.Count) + "\n";
            foreach (IGrouping<string, Outpost> group in outposts
                         .GroupBy(o => o.def.LabelCap.ToString())
                         .OrderBy(g => g.Key))
            {
                result += "VOE_CodexOverviewOutpostLine".Translate(group.Key, group.Count()) + "\n";
            }

            return result.TrimEnd();
        }

        private static string FeatureLine(string labelKey, bool active)
        {
            return "  " + (active ? "+ " : "- ") + labelKey.Translate() + "\n";
        }
    }
}
