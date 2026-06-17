using System;
using System.Linq;
using FactionColonies;
using Outposts;
using Verse;

namespace EmpireVOE
{
    /* DESTRUCTIVE: exercises the raid-target wrapper against live outposts. OnRaidLost injures outpost
       pawns and destroys items; tests prefer a transient outpost so the player's real outposts are not
       damaged. The registry round-trip self-reverts. Created outposts are NOT cleaned up. */
    public static class VOERaidDestructiveTests
    {
        [EmpireDestructiveTest("VOE.Destructive.Military")]
        public static void OutpostRaidTarget_MilitaryLevel_FromPawnCount()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost outpost = VOEDestructiveTestUtil.FirstPlayerOutpost()
                ?? VOEDestructiveTestUtil.CreateTransientOutpost();
            if (outpost is null) TestAssert.Skip("No player outpost available");

            var target = new OutpostRaidTarget(outpost);
            TestAssert.AreEqual(Math.Max(0, outpost.PawnCount / 3), target.MilitaryLevel,
                "MilitaryLevel should be PawnCount / 3 (integer division)");

            DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostRaidTarget_MilitaryLevel");
        }

        [EmpireDestructiveTest("VOE.Destructive.Military")]
        public static void OutpostRaidTarget_OnRaidWon_DoesNotThrow()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost outpost = VOEDestructiveTestUtil.FirstPlayerOutpost()
                ?? VOEDestructiveTestUtil.CreateTransientOutpost();
            if (outpost is null) TestAssert.Skip("No player outpost available");

            var target = new OutpostRaidTarget(outpost);
            TestAssert.DoesNotThrow(() => target.OnRaidWon(new BattleResult()), "OnRaidWon threw");

            DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostRaidTarget_OnRaidWon");
        }

        [EmpireDestructiveTest("VOE.Destructive.Military")]
        public static void OutpostRaidTarget_OnRaidLost_DoesNotThrow()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            // Use a transient outpost so we don't injure the player's real outpost pawns.
            Outpost outpost = VOEDestructiveTestUtil.CreateTransientOutpost();
            if (outpost is null) TestAssert.Skip("Could not create a transient outpost");

            var target = new OutpostRaidTarget(outpost);
            TestAssert.DoesNotThrow(() => target.OnRaidLost(new BattleResult()), "OnRaidLost threw");

            DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostRaidTarget_OnRaidLost");
        }

        [EmpireDestructiveTest("VOE.Destructive.Military")]
        public static void RaidTargetRegistry_RegisterUnregister_RoundTrips()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost outpost = VOEDestructiveTestUtil.CreateTransientOutpost();
            if (outpost is null) TestAssert.Skip("Could not create a transient outpost");

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
}
