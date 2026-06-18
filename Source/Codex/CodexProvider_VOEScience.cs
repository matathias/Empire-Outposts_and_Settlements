using System.Linq;
using FactionColonies;
using RimWorld;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Live "Live Data" panel for the Science Outposts entry. Lists the current
    /// settlement-to-science-outpost research links and how many researchers (at or
    /// above the configured skill floor) each linked outpost is contributing.
    /// </summary>
    public class CodexProvider_VOEScience : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null) return null;
            if (!EmpireVOESettings.ResourceLinkActive)
                return "VOE_CodexFeatureDisabled".Translate();
            if (!faction.settlements.Any())
                return "VOE_CodexNoSettlements".Translate();

            string result = "VOE_CodexScienceHeader".Translate() + "\n\n";
            bool any = false;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_ResourceLink link = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (link?.linkedOutposts is null) continue;
                foreach (Outpost_Science outpost in link.linkedOutposts.OfType<Outpost_Science>())
                {
                    if (outpost is null || outpost.Destroyed) continue;
                    any = true;
                    result += "  " + "VOE_CodexScienceLine".Translate(
                        s.Name, outpost.Name ?? outpost.def.label, CountResearchers(outpost)) + "\n";
                }
            }

            return any ? result.TrimEnd() : "VOE_CodexScienceNone".Translate().ToString();
        }

        private static int CountResearchers(Outpost_Science outpost)
        {
            int count = 0;
            foreach (Pawn pawn in outpost.CapablePawns)
            {
                if (pawn.skills is null) continue;
                SkillRecord skill = pawn.skills.GetSkill(SkillDefOf.Intellectual);
                if (skill is object && skill.Level >= EmpireVOESettings.skillFloor)
                    count++;
            }
            return count;
        }
    }
}
