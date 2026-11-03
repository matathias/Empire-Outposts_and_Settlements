using System;
using FactionColonies;
using Outposts;

namespace EmpireVOE
{
    /// <summary>
    /// Single source of truth for an outpost's military level. Prices the live garrison through the
    /// base mod's cost->level curve: SquadPowerRegistry.LevelFromPawns sums each capable pawn's
    /// MarketValue (body + worn gear + implants), effectiveness-weighted so downed/injured pawns
    /// contribute proportionally, then inverts CalculateSquadBudget. This keeps an outpost on the same
    /// scale as settlement squads (and ranks it fairly in auto-defender selection). Empty outpost reads
    /// 0; any non-empty garrison floors at 1. Used by both OutpostRaidTarget (raid defense + world-map
    /// infobox) and DefensiveAutoDefender (military tab + auto-defend). Combat *efficiency* stays
    /// skill-based (DefensiveAutoDefender.CalculateEfficiency) — level is "how much force", efficiency
    /// is "how well they fight".
    /// </summary>
    public static class OutpostMilitaryUtil
    {
        public static int MilitaryLevel(Outpost outpost)
        {
            if (outpost is null || outpost.PawnCount == 0) return 0;
            return Math.Max(1, (int)Math.Round(SquadPowerRegistry.LevelFromPawns(outpost.CapablePawns)));
        }
    }
}
