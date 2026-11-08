namespace EmpireVOE;

/* DESTRUCTIVE: drives the conversion refund (used when the new settlement cannot be created) against the
   live tax map. It places real silver; the test then takes back exactly what it added, by shrinking or
   destroying the silver stacks that grew or appeared, so the map's silver total ends where it started. */
public static class VOEConversionRefundDestructiveTests
{
    private static Dictionary<Thing, int> SilverStacks(Map map) =>
        map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToDictionary(t => t, t => t.stackCount);

    [EmpireDestructiveTest("VOE.Destructive.Conversion")]
    public static void RefundSilver_PlacesExactAmountInLegalStacks()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Map map = TestAssert.NotNull(FindFC.TaxMap, "A refund needs a tax map to land on");
        int stackLimit = ThingDefOf.Silver.stackLimit;
        int amount = stackLimit * 2 + 37;   // two full stacks and a partial one

        Dictionary<Thing, int> before = SilverStacks(map);
        Dictionary<Thing, int> after = null;
        try
        {
            OutpostConversionUtil.RefundSilver(amount);
            after = SilverStacks(map);

            int added = after.Sum(kv => kv.Value) - before.Sum(kv => kv.Value);
            TestAssert.AreEqual(amount, added, "the refund should place exactly the refunded amount of silver");
            TestAssert.IsFalse(after.Keys.Any(t => t.stackCount > stackLimit), "no refunded stack should exceed the stack limit");

            DestructiveTestUtil.AssertEmpireInvariants(f, "RefundSilver");
        }
        finally
        {
            // Undo what the refund added: new stacks are destroyed, merged-into stacks shrink back.
            foreach (Thing t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList())
            {
                if (!before.TryGetValue(t, out int original))
                {
                    if (!t.Destroyed) t.Destroy(DestroyMode.Vanish);
                }
                else if (t.stackCount > original)
                {
                    t.stackCount = original;
                }
            }
        }
    }

    [EmpireDestructiveTest("VOE.Destructive.Conversion")]
    public static void RefundSilver_ZeroOrNegative_PlacesNothing()
    {
        Map map = TestAssert.NotNull(FindFC.TaxMap, "A refund needs a tax map to land on");
        int before = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);

        OutpostConversionUtil.RefundSilver(0);
        OutpostConversionUtil.RefundSilver(-5);

        int after = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t => t.stackCount);
        TestAssert.AreEqual(before, after, "a zero or negative refund should place no silver");
    }
}
