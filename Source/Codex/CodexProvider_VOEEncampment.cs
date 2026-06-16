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
                return "VOE_CodexFeatureDisabled".Translate();
            if (!faction.settlements.Any())
                return "VOE_CodexNoSettlements".Translate();

            string result = "VOE_CodexEncampmentHeader".Translate() + "\n\n";
            bool any = false;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                EncampmentCacheEntry entry = EncampmentCache.GetOrBuild(s);
                if (entry is null || entry.encampments.Count == 0) continue;
                any = true;
                result += s.Name + ":\n";
                result += "  " + "VOE_EncampmentHealRateDesc".Translate(
                    entry.encampments.Count,
                    (entry.totalHealRateBonus * 100).ToString("F0")) + "\n\n";
            }

            return any ? result.TrimEnd() : "VOE_CodexEncampmentNone".Translate().ToString();
        }
    }
}
