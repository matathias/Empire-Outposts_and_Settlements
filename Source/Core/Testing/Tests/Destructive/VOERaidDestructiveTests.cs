using Outposts;

namespace EmpireVOE;

/* DESTRUCTIVE: exercises the raid-target wrapper against live outposts. OnRaidLost injures outpost
   pawns and destroys items; tests prefer a transient outpost so the player's real outposts are not
   damaged. The registry round-trip self-reverts. Created outposts are NOT cleaned up. */
public static class VOERaidDestructiveTests
{
    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void OutpostRaidTarget_MilitaryLevel_MatchesCostCurve()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        Outpost outpost = VOEDestructiveTestUtil.FirstPlayerOutpost()
            ?? VOEDestructiveTestUtil.RequireTransientOutpost();

        var target = new OutpostRaidTarget(outpost);

        // The raid target must read off the shared cost-curve helper (the single source of truth
        // shared with DefensiveAutoDefender) so the infobox and the military tab agree.
        TestAssert.AreEqual(OutpostMilitaryUtil.MilitaryLevel(outpost), target.MilitaryLevel,
            "MilitaryLevel should come from OutpostMilitaryUtil (the shared cost-curve helper)");

        // Contract: empty outpost reads 0; any non-empty garrison floors at 1.
        if (outpost.PawnCount == 0)
            TestAssert.AreEqual(0, target.MilitaryLevel, "Empty outpost should read level 0");
        else
            TestAssert.GreaterThan(target.MilitaryLevel, 0, "A manned outpost should have a positive military level");

        DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostRaidTarget_MilitaryLevel");
    }

    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void OutpostRaidTarget_OnRaidWon_DoesNotThrow()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        Outpost outpost = VOEDestructiveTestUtil.FirstPlayerOutpost()
            ?? VOEDestructiveTestUtil.RequireTransientOutpost();

        var target = new OutpostRaidTarget(outpost);
        TestAssert.DoesNotThrow(() => target.OnRaidWon(new BattleResult()), "OnRaidWon threw");

        DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostRaidTarget_OnRaidWon");
    }

    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void OutpostRaidTarget_OnRaidLost_DoesNotThrow()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        // Use a transient outpost so we don't injure the player's real outpost pawns.
        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();

        var target = new OutpostRaidTarget(outpost);
        TestAssert.DoesNotThrow(() => target.OnRaidLost(new BattleResult()), "OnRaidLost threw");

        DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostRaidTarget_OnRaidLost");
    }

    /// <summary>
    /// T5: OnRaidLost's item-destruction is bounded — it removes min(itemCount, Rand.Range(1,4)) = 1..3
    /// stored items when the outpost has any. Stocks a transient outpost with 5 distinct stacks (so the
    /// count exceeds the cap) and asserts exactly 1..3 were destroyed. The pawn-injury half is random in
    /// which parts it hits, so it stays covered by the non-throw contract above.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void OutpostRaidTarget_OnRaidLost_DestroysBoundedItems()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        // Transient so we never destroy the player's real stored goods or injure their pawns.
        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();

        try
        {
            // Five distinct stacks so they don't merge and the count is above the 3-item destroy cap.
            AddStack(outpost, ThingDefOf.Silver, 50);
            AddStack(outpost, ThingDefOf.Steel, 50);
            AddStack(outpost, ThingDefOf.WoodLog, 50);
            AddStack(outpost, ThingDefOf.Gold, 50);
            AddStack(outpost, ThingDefOf.Plasteel, 50);

            int before = outpost.Things.Count();
            TestAssert.GreaterThan(before, 3, "Five distinct stacks should leave more items than the 3-item destroy cap");

            new OutpostRaidTarget(outpost).OnRaidLost(new BattleResult());

            int destroyed = before - outpost.Things.Count();
            TestAssert.GreaterThan(destroyed, 0, "A raid loss should destroy at least one stored item");
            TestAssert.LessThanOrEqual(destroyed, 3, "A raid loss should destroy at most three stored items");
        }
        finally
        {
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostRaidTarget_OnRaidLost_Items");
    }

    private static void AddStack(Outpost outpost, ThingDef def, int count)
    {
        Thing thing = ThingMaker.MakeThing(def);
        thing.stackCount = count;
        outpost.AddItem(thing);
    }

    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void RaidTargetRegistry_RegisterUnregister_RoundTrips()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();

        var target = new OutpostRaidTarget(outpost);
        // Submods register through the EmpireRegistry facade; the probe routes IRaidTarget to RaidTargetRegistry.
        EmpireRegistry.Register(target);
        try
        {
            TestAssert.Contains(RaidTargetRegistry.Targets, (IRaidTarget)target,
                "Registered target should appear in RaidTargetRegistry.Targets");
        }
        finally
        {
            EmpireRegistry.Unregister(target);
        }
        TestAssert.IsFalse(RaidTargetRegistry.Targets.Contains(target),
            "Target should be gone after unregister");

        DestructiveTestUtil.AssertEmpireInvariants(f, "RaidTargetRegistry_RoundTrip");
    }
}
