using System.Linq;
using FactionColonies;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Live "Live Data" panel for the Encampment Recovery entry. Shows which
    /// settlements currently have encampments within range and the merc heal-rate
    /// bonus each is receiving (read live from the encampment cache).
    /// </summary>
    public class CodexProvider_VOEEncampment : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null) return null;
            if (!EmpireVOESettings.EncampmentActive)
                return "FCVOE_CodexFeatureDisabled".Translate();
            if (!faction.settlements.Any())
                return "FCVOE_CodexNoSettlements".Translate();

            string result = "FCVOE_CodexEncampmentHeader".Translate() + "\n\n";
            bool any = false;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                EncampmentCacheEntry entry = EncampmentCache.GetOrBuild(s);
                if (entry is null || entry.encampments.Count == 0) continue;
                any = true;
                result += s.Name + ":\n";
                result += "  " + "FCVOE_EncampmentHealRateDesc".Translate(
                    entry.encampments.Count,
                    (entry.totalHealRateBonus * 100).ToString("F0")) + "\n\n";
            }

            return any ? result.TrimEnd() : "FCVOE_CodexEncampmentNone".Translate().ToString();
        }
    }
}
