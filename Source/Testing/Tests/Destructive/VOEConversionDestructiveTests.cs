using System.Collections.Generic;
using FactionColonies;
using Outposts;
using Verse;

namespace EmpireVOE
{
    /* DESTRUCTIVE: drives the real outpost->settlement conversion against live world/faction state.
       Creates transient outposts and (on success) a real settlement; spends silver. Settings are pinned
       (conversion delay off, founding cost forced free) and restored, but the created settlement and the
       destroyed outpost are NOT reverted. */
    public static class VOEConversionDestructiveTests
    {
        [EmpireDestructiveTest("VOE.Destructive.Conversion")]
        public static void ConvertOutpost_CreatesSettlement_DestroysOutpost()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost outpost = VOEDestructiveTestUtil.FirstConvertibleOutpost()
                ?? VOEDestructiveTestUtil.CreateTransientOutpost(requireConversion: true);
            if (outpost is null) TestAssert.Skip("No convertible outpost available");

            List<WorldSettlementDef> types = OutpostConversionUtil.GetConvertibleTypes(outpost);
            if (types.Count == 0) TestAssert.Skip("Outpost has no currently-convertible settlement types");

            int baseline = f.settlements.Count;
            var snap = VOETestHelper.SnapshotSettings();
            bool ok;
            try
            {
                EmpireVOESettings.enableConversionDelay = false;     // skip the establish-delay gate
                EmpireVOESettings.reducedFoundingCostFactor = 0f;    // force the conversion to be free
                ok = OutpostConversionUtil.ConvertOutpost(outpost, types[0]);
            }
            finally
            {
                VOETestHelper.RestoreSettings(snap);
            }

            TestAssert.IsTrue(ok, "Zero-cost conversion of a ready outpost should succeed");
            TestAssert.AreEqual(baseline + 1, f.settlements.Count, "Faction should gain exactly one settlement");
            TestAssert.IsTrue(outpost.Destroyed, "Source outpost should be destroyed after conversion");

            DestructiveTestUtil.AssertEmpireInvariants(f, "ConvertOutpost_CreatesSettlement");
        }

        [EmpireDestructiveTest("VOE.Destructive.Conversion")]
        public static void CanConvertNow_PawnlessOutpost_ReturnsFalseWithReason()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost outpost = VOEDestructiveTestUtil.CreateTransientOutpost(pawnCount: 0);
            if (outpost is null) TestAssert.Skip("Could not create a transient outpost");

            var snap = VOETestHelper.SnapshotSettings();
            try
            {
                EmpireVOESettings.enableConversionDelay = false; // isolate the pawn-count branch
                bool can = OutpostConversionUtil.CanConvertNow(outpost, out string reason);
                TestAssert.IsFalse(can, "A pawnless outpost should not be convertible");
                TestAssert.IsNotNull(reason, "A blocked conversion should report a reason");
            }
            finally
            {
                VOETestHelper.RestoreSettings(snap);
                VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe — clean up
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "CanConvertNow_Pawnless");
        }

        [EmpireDestructiveTest("VOE.Destructive.Conversion")]
        public static void GetConvertibleTypes_AllEntriesPassTheFilters()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost existing = VOEDestructiveTestUtil.FirstConvertibleOutpost();
            Outpost outpost = existing ?? VOEDestructiveTestUtil.CreateTransientOutpost(requireConversion: true);
            if (outpost is null) TestAssert.Skip("No convertible outpost available");
            bool transient = existing is null;

            try
            {
                foreach (WorldSettlementDef d in OutpostConversionUtil.GetConvertibleTypes(outpost))
                {
                    TestAssert.IsTrue(d.available, $"{d.defName}: returned type should be available");
                    TestAssert.IsTrue(d.IsUnlocked(), $"{d.defName}: returned type should be unlocked");
                    TestAssert.IsTrue(d.AllowsTileLayer(outpost.Tile),
                        $"{d.defName}: returned type should allow the outpost's tile layer");
                }
            }
            finally
            {
                if (transient) VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe — clean up
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "GetConvertibleTypes_AllEntriesValid");
        }
    }
}
