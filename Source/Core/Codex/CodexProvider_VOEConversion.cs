using System.Collections.Generic;
using System.Linq;
using System.Text;
using FactionColonies;
using Outposts;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Live "Live Data" panel for the outpost-conversion entry. Lists the player's outposts that can
    /// become a settlement, the types each can become, and whether the establishment delay has elapsed.
    /// </summary>
    public class CodexProvider_VOEConversion : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (!EmpireVOESettings.OutpostConversionActive)
                return "FCVOE_CodexFeatureDisabled".Translate();

            List<Outpost> outposts = Find.WorldObjects?.AllWorldObjects?.OfType<Outpost>()
                                         .Where(o => o.Faction == Faction.OfPlayer)
                                         .ToList()
                                     ?? new List<Outpost>();

            List<Outpost> convertible = outposts
                .Where(o => OutpostConversionUtil.GetConvertibleTypes(o).Count > 0)
                .OrderBy(o => o.LabelCap.ToString())
                .ToList();

            if (convertible.Count == 0)
                return "FCVOE_CodexConversionNone".Translate();

            StringBuilder sb = new StringBuilder();
            sb.Append("FCVOE_CodexConversionHeader".Translate()).Append("\n");
            foreach (Outpost o in convertible)
            {
                List<WorldSettlementDef> types = OutpostConversionUtil.GetConvertibleTypes(o);
                string typeList = string.Join(", ", types.Select(t => t.LabelCap.ToString()));
                int remaining = OutpostConversionUtil.DelayDaysRemaining(o);
                string status = remaining > 0
                    ? "FCVOE_ConvertAvailableInDays".Translate(remaining.ToString())
                    : "FCVOE_ConvertReady".Translate();
                sb.Append("FCVOE_CodexConversionLine".Translate(o.LabelCap, typeList, status)).Append("\n");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
