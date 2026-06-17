using FactionColonies;

namespace EmpireVOE
{
    /// <summary>
    /// Coverage for <see cref="WorldObjectComp_OutpostBonus"/>'s additive/multiplier bookkeeping. The
    /// comp is exercised standalone (the tested members never touch comp parent/world state), so these
    /// are non-destructive. The additive-sum test builds a side-effect-free <see cref="ResourceFC"/>
    /// fixture, so it needs no live game state.
    /// </summary>
    public static class VOEOutpostBonusTests
    {
        [EmpireTest("VOE.Bonus")]
        public static void NewComp_HasNoBonus()
        {
            var comp = new WorldObjectComp_OutpostBonus();
            TestAssert.IsFalse(comp.HasAnyBonus);
        }

        [EmpireTest("VOE.Bonus")]
        public static void SetBonus_NonPositive_Ignored()
        {
            var comp = new WorldObjectComp_OutpostBonus();
            comp.SetBonus("X", 0.0);
            comp.SetBonus("Y", -1.0);
            TestAssert.IsFalse(comp.HasAnyBonus, "Zero/negative bonuses should be ignored");
        }

        [EmpireTest("VOE.Bonus")]
        public static void SetBonus_Positive_FlipsHasAnyBonus()
        {
            var comp = new WorldObjectComp_OutpostBonus();
            comp.SetBonus("X", 0.5);
            TestAssert.IsTrue(comp.HasAnyBonus);
        }

        [EmpireTest("VOE.Bonus")]
        public static void SetTownFlatAdditive_Positive_FlipsHasAnyBonus()
        {
            var comp = new WorldObjectComp_OutpostBonus();
            comp.SetTownFlatAdditive(0.25);
            TestAssert.IsTrue(comp.HasAnyBonus);
        }

        [EmpireTest("VOE.Bonus")]
        public static void GetResourceMultiplierModifier_IsOne()
        {
            var comp = new WorldObjectComp_OutpostBonus();
            comp.SetTownFlatAdditive(0.25);
            TestAssert.AreEqual(1.0, comp.GetResourceMultiplierModifier(null));
        }

        [EmpireTest("VOE.Bonus")]
        public static void GetResourceAdditiveModifier_NullResource_IsZero()
        {
            var comp = new WorldObjectComp_OutpostBonus();
            comp.SetTownFlatAdditive(0.25);
            TestAssert.AreEqual(0.0, comp.GetResourceAdditiveModifier(null));
        }

        [EmpireTest("VOE.Bonus")]
        public static void GetResourceAdditiveModifier_SumsTownAndPerResource()
        {
            // GetResourceAdditiveModifier only reads resource.def.defName, so a bare ResourceFC built on
            // the empty parameterless ctor (no settlement/filter init) is a sufficient, side-effect-free fixture.
            var res = new ResourceFC { def = new ResourceTypeDef { defName = "VOE_TestResource", label = "test" } };
            var comp = new WorldObjectComp_OutpostBonus();
            comp.SetTownFlatAdditive(0.25);

            // A per-resource bonus keyed to a different resource: only the flat town additive applies.
            comp.SetBonus("VOE_TestResource_NotThisOne", 0.5);
            TestAssert.AreEqual(0.25, comp.GetResourceAdditiveModifier(res), 0.001,
                "Only the town flat additive should apply when the per-resource bonus is for another resource");

            // Now key the per-resource bonus to this resource: town flat + per-resource.
            comp.SetBonus("VOE_TestResource", 0.5);
            TestAssert.AreEqual(0.75, comp.GetResourceAdditiveModifier(res), 0.001,
                "Town flat (0.25) + per-resource (0.5) should sum to 0.75");
        }
    }
}
