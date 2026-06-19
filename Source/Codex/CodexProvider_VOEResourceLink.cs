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
                return "VOE_CodexFeatureDisabled".Translate();
            if (!faction.settlements.Any())
                return "VOE_CodexNoSettlements".Translate();

            string result = "VOE_CodexResourceLinkHeader".Translate() + "\n\n";
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
                    result += "  " + "VOE_CodexResourceLinkLine".Translate(
                        s.Name, outpost.Name ?? outpost.def.label, resources, contribution.ToString("0.##")) + "\n";
                }
            }

            return any ? result.TrimEnd() : "VOE_CodexResourceLinkNone".Translate().ToString();
        }
    }
}
