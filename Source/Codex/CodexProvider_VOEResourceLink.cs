using System.Linq;
using FactionColonies;
using Outposts;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Live "Live Data" panel for the Resource Outpost Linking entry. Lists, per settlement, the linked
    /// production outposts, the resource(s) each feeds, and its current additive contribution.
    /// </summary>
    public class CodexProvider_VOEResourceLink : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null) return null;
            if (!EmpireVOESettings.ResourceLinkActive)
                return "FCVOE_CodexFeatureDisabled".Translate();
            if (!faction.settlements.Any())
                return "FCVOE_CodexNoSettlements".Translate();

            string result = "FCVOE_CodexResourceLinkHeader".Translate() + "\n\n";
            bool any = false;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_ResourceLink link = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (link?.linkedOutposts is null) continue;
                foreach (Outpost outpost in link.linkedOutposts)
                {
                    if (outpost is null || outpost.Destroyed) continue;
                    OutpostResourceLinkExtension ext = outpost.def.GetModExtension<OutpostResourceLinkExtension>();
                    if (ext?.resources is null) continue;
                    any = true;
                    string resources = string.Join(", ", ext.resources.Select(r => r.LabelCap.ToString()).ToArray());
                    double contribution = ResourceLinkUtil.ContributionOf(outpost, s);
                    result += "  " + "FCVOE_CodexResourceLinkLine".Translate(
                        s.Name, outpost.Name ?? outpost.def.label, resources, contribution.ToString("0.##")) + "\n";
                }
            }

            return any ? result.TrimEnd() : "FCVOE_CodexResourceLinkNone".Translate().ToString();
        }
    }
}
