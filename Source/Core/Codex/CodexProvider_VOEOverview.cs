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
                return "FCVOE_CodexOverviewDisabled".Translate();

            string result = "FCVOE_CodexOverviewFeatures".Translate() + "\n";
            result += FeatureLine("FCVOE_EnableMilitary", EmpireVOESettings.MilitaryActive);
            result += FeatureLine("FCVOE_EnableDelivery", EmpireVOESettings.DeliveryActive);
            result += FeatureLine("FCVOE_EnableFinancing", EmpireVOESettings.FinancingActive);
            result += FeatureLine("FCVOE_EnableResourceLink", EmpireVOESettings.ResourceLinkActive);
            result += FeatureLine("FCVOE_EnableEncampment", EmpireVOESettings.EncampmentActive);
            result += FeatureLine("FCVOE_EnableOutpostConversion", EmpireVOESettings.OutpostConversionActive);
            result += FeatureLine("FCVOE_EnableRoads", EmpireVOESettings.RoadsActive);
            result += "\n";

            List<Outpost> outposts = Find.WorldObjects?.AllWorldObjects?.OfType<Outpost>().ToList()
                                     ?? new List<Outpost>();
            if (outposts.Count == 0)
                return result + "FCVOE_CodexOverviewNoOutposts".Translate();

            result += "FCVOE_CodexOverviewOutposts".Translate(outposts.Count) + "\n";
            foreach (IGrouping<string, Outpost> group in outposts
                         .GroupBy(o => o.def.LabelCap.ToString())
                         .OrderBy(g => g.Key))
            {
                result += "FCVOE_CodexOverviewOutpostLine".Translate(group.Key, group.Count()) + "\n";
            }

            return result.TrimEnd();
        }

        private static string FeatureLine(string labelKey, bool active)
        {
            return "  " + (active ? "+ " : "- ") + labelKey.Translate() + "\n";
        }
    }
}
