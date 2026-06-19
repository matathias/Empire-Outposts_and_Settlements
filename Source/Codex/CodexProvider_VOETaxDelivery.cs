using System.Linq;
using FactionColonies;
using Outposts;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Live "Live Data" panel for the Tax Delivery entry. Lists the settlements
    /// currently routing their tax shipments to an outpost (rather than the tax map).
    /// </summary>
    public class CodexProvider_VOETaxDelivery : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null) return null;
            if (!EmpireVOESettings.DeliveryActive)
                return "FCVOE_CodexFeatureDisabled".Translate();
            if (!faction.settlements.Any())
                return "FCVOE_CodexNoSettlements".Translate();

            string result = "FCVOE_CodexDeliveryHeader".Translate() + "\n\n";
            bool any = false;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                Outpost outpost = links?.GetDeliveryOutpost();
                if (outpost is null) continue;
                any = true;
                result += "  " + "FCVOE_CodexDeliveryLine".Translate(s.Name, outpost.LabelCap) + "\n";
            }

            return any ? result.TrimEnd() : "FCVOE_CodexDeliveryNone".Translate().ToString();
        }
    }
}
