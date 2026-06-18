using FactionColonies;

namespace EmpireVOE
{
    /// <summary>
    /// Pure-math coverage for <see cref="VOEFormulas"/>. All formulas take their inputs as primitives,
    /// so these are fully settings-independent and use literal asserts (including the clamp/floor
    /// boundaries). Non-destructive.
    /// </summary>
    public static class VOEFormulaTests
    {
        /*-*-*- Efficiency: linear, slope 0.05 to skill 20 then half-rate above. Anchors: 0->0.5, 10->1.0, 20->1.5 -*-*-*/

        [EmpireTest("VOE.Formula")]
        public static void Efficiency_Anchors()
        {
            TestAssert.AreEqual(0.5, VOEFormulas.Efficiency(0.0));
            TestAssert.AreEqual(1.0, VOEFormulas.Efficiency(10.0));
            TestAssert.AreEqual(1.5, VOEFormulas.Efficiency(20.0));
        }

        [EmpireTest("VOE.Formula")]
        public static void Efficiency_HalfRateAboveTwenty()
        {
            TestAssert.AreEqual(1.525, VOEFormulas.Efficiency(21.0)); // 1.5 + 0.025*(21-20)
            TestAssert.AreEqual(1.60, VOEFormulas.Efficiency(24.0));  // 1.5 + 0.025*(24-20)
        }

        /*-*-*- DelayDaysRemaining: disabled -> 0, else max(0, delay - daysSince) -*-*-*/

        [EmpireTest("VOE.Formula")]
        public static void DelayDaysRemaining_Disabled_IsZero()
        {
            TestAssert.AreEqual(0, VOEFormulas.DelayDaysRemaining(0, 30, false));
        }

        [EmpireTest("VOE.Formula")]
        public static void DelayDaysRemaining_CountsDownAndClamps()
        {
            TestAssert.AreEqual(30, VOEFormulas.DelayDaysRemaining(0, 30, true));
            TestAssert.AreEqual(20, VOEFormulas.DelayDaysRemaining(10, 30, true));
            TestAssert.AreEqual(0, VOEFormulas.DelayDaysRemaining(30, 30, true));   // exactly elapsed
            TestAssert.AreEqual(0, VOEFormulas.DelayDaysRemaining(40, 30, true));   // over-elapsed clamps to 0
        }

        /*-*-*- SkillContribution: level >= floor ? level*perLevel : 0 -*-*-*/

        [EmpireTest("VOE.Formula")]
        public static void SkillContribution_AboveFloor_Scales()
        {
            TestAssert.AreEqual(0.125, VOEFormulas.SkillContribution(5, 0, 0.025));
            TestAssert.AreEqual(0.25, VOEFormulas.SkillContribution(10, 10, 0.025)); // at the floor counts
        }

        [EmpireTest("VOE.Formula")]
        public static void SkillContribution_BelowFloor_IsZero()
        {
            TestAssert.AreEqual(0.0, VOEFormulas.SkillContribution(5, 10, 0.025));
        }
    }
}
