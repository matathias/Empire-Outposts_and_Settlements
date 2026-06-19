using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using FactionColonies.util;
using Outposts;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Live "Live Data" panel for the Defensive Outposts entry. Lists each
    /// defensive outpost with its current military level, range, status, and
    /// whether auto-defend is enabled.
    /// </summary>
    public class CodexProvider_VOEDefensive : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (!EmpireVOESettings.MilitaryActive)
                return "FCVOE_CodexFeatureDisabled".Translate();

            List<Outpost_Defensive> outposts = Find.WorldObjects?.AllWorldObjects?
                .OfType<Outpost_Defensive>().ToList() ?? new List<Outpost_Defensive>();
            if (outposts.Count == 0)
                return "FCVOE_CodexDefensiveNone".Translate();

            string result = "FCVOE_CodexDefensiveHeader".Translate() + "\n\n";
            foreach (Outpost_Defensive outpost in outposts)
            {
                WorldObjectComp_EmpireDefensive comp = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
                if (comp is null) continue;
                WorldObjectComp_EmpireOutpost outpostComp = outpost.GetComponent<WorldObjectComp_EmpireOutpost>();

                int level = comp.defender?.MilitaryLevel ?? 0;
                int range = comp.defender?.Range ?? outpost.Range;

                result += "FCVOE_CodexDefensiveLine".Translate(outpost.Name ?? outpost.def.label, level, range) + "\n";
                result += "    " + StatusLine(comp, outpostComp) + "\n";
                result += "    " + (comp.autoDefend
                    ? "FCVOE_CodexDefensiveAutoOn".Translate()
                    : "FCVOE_CodexDefensiveAutoOff".Translate()) + "\n\n";
            }

            return result.TrimEnd();
        }

        private static string StatusLine(WorldObjectComp_EmpireDefensive comp, WorldObjectComp_EmpireOutpost outpostComp)
        {
            bool underAttack = outpostComp?.raidTarget?.IsUnderAttack ?? false;
            if (underAttack)
                return "FCVOE_DefenseStatusAttacked".Translate();
            if (comp.defender is object && comp.defender.Busy)
                return "FCVOE_StatusDefending".Translate(comp.defender.DefendingTargetName);
            if (comp.IsOnCooldown)
                return "FCVOE_DefenseStatusCooldown".Translate(comp.CooldownTicksLeft.ToTimeString());
            return "FCVOE_DefenseStatusReady".Translate();
        }
    }
}
