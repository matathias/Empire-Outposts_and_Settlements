using System.Linq;
using FactionColonies;
using Outposts;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Live "Live Data" panel for the Outpost Financing entry. Lists the settlements
    /// whose silver payments are currently bankrolled by an outpost.
    /// </summary>
    public class CodexProvider_VOEFinancing : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null) return null;
            if (!EmpireVOESettings.FinancingActive)
                return "VOE_CodexFeatureDisabled".Translate();
            if (!faction.settlements.Any())
                return "VOE_CodexNoSettlements".Translate();

            string result = "VOE_CodexFinancingHeader".Translate() + "\n\n";
            bool any = false;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                Outpost outpost = links?.GetFinancingOutpost();
                if (outpost is null) continue;
                any = true;
                result += "  " + "VOE_CodexFinancingLine".Translate(s.Name, outpost.LabelCap) + "\n";
            }

            return any ? result.TrimEnd() : "VOE_CodexFinancingNone".Translate().ToString();
        }
    }
}
