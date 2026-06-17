using System;

namespace EmpireVOE
{
    /// <summary>
    /// Pure, settings-free arithmetic extracted from EmpireVOE's logic so it can be unit-tested with
    /// exact-value asserts (see <c>VOEFormulaTests</c>). Mirrors the base mod's <c>SettlementFormulas</c>
    /// pattern: callers do the world/pawn aggregation, then pass the resulting scalars in here.
    /// </summary>
    public static class VOEFormulas
    {
        /* Military level: pawn-count-dominant (more bodies = bigger force pool), skill is secondary.
           Floors at 1 once there is at least one pawn. */
        public static int MilitaryLevel(int pawnCount, double avgCombatSkill)
        {
            if (pawnCount == 0) return 0;
            return Math.Max(1, (int)(pawnCount * (0.5 + avgCombatSkill / 40.0)));
        }

        /* Efficiency: skill-dominant, pawn count irrelevant. Piecewise quadratic up to skill 20, then a
           sqrt tail for diminishing returns. Anchored at skill 0 -> 0.3, 10 -> 1.0, 20 -> 1.6 (both
           branches meet at 1.6 when skill == 20). */
        public static double Efficiency(double avgSkill)
        {
            if (avgSkill <= 20.0)
                return 0.3 + 0.075 * avgSkill - 0.0005 * avgSkill * avgSkill;
            return 1.6 + 0.05 * Math.Sqrt(avgSkill - 20.0);
        }

        /* Days remaining before an outpost may be converted. 0 when the delay is disabled or elapsed. */
        public static int DelayDaysRemaining(int daysSinceEstablished, int conversionDelayDays, bool enableDelay)
        {
            if (!enableDelay) return 0;
            return Math.Max(0, conversionDelayDays - daysSinceEstablished);
        }

        /* A single pawn's per-level contribution (skill bonus or heal rate): the skill level times the
           per-level rate, or 0 when the level is below the configured floor. Shared by the conversion
           skill-bonus calc and the encampment heal-rate calc. */
        public static double SkillContribution(int level, int skillFloor, double perLevel)
        {
            return level >= skillFloor ? level * perLevel : 0.0;
        }
    }
}
