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
        /* Efficiency: skill-dominant, pawn count irrelevant. Linear in skill. Slope 0.05 up to skill 20
           (anchored at 0 -> 0.5, 10 -> 1.0, 20 -> 1.5), then continues above 20 at half the rate
           (slope 0.025) for modded skills past the vanilla cap. Both branches meet at 1.5 when
           skill == 20. */
        public static double Efficiency(double avgSkill)
        {
            if (avgSkill <= 20.0)
                return 0.5 + 0.05 * avgSkill;
            return 1.5 + 0.025 * (avgSkill - 20.0);
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
