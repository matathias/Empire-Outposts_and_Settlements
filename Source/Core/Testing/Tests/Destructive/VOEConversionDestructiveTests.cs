using Outposts;

namespace EmpireVOE;

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

    /// <summary>
    /// T9: conversion wires bonuses onto the new settlement. The town flat-additive path is deterministic —
    /// a Town-derived settlement gets EmpireVOESettings.townFlatAdditive applied to every resource — so we
    /// pin that setting to a known value and assert the settlement's OutpostBonus comp reports it. (The
    /// skill-derived per-resource path is comp-level covered by VOEOutpostBonusTests; here we prove the
    /// integration seam actually attaches a bonus.) Requires an outpost whose conversion def grants the
    /// town additive; skips otherwise.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Conversion")]
    public static void ConvertOutpost_TownAdditive_AppliedToNewSettlement()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        WorldObjectDef def = DefDatabase<WorldObjectDef>.AllDefsListForReading.FirstOrDefault(d =>
        {
            OutpostConversionExtension e = d.GetModExtension<OutpostConversionExtension>();
            return e is not null && e.grantsTownAdditive
                && d.worldObjectClass is not null && typeof(Outpost).IsAssignableFrom(d.worldObjectClass);
        });
        if (def is null) TestAssert.Skip("No conversion def grants the town flat additive");

        Outpost outpost = Find.WorldObjects.AllWorldObjects.OfType<Outpost>()
            .FirstOrDefault(o => o.Faction == Faction.OfPlayer && o.def == def)
            ?? VOEDestructiveTestUtil.CreateTransientOutpost(def);
        if (outpost is null) TestAssert.Skip("Could not obtain a town-additive outpost");

        List<WorldSettlementDef> types = OutpostConversionUtil.GetConvertibleTypes(outpost);
        if (types.Count == 0) TestAssert.Skip("Outpost has no currently-convertible settlement types");

        RimWorld.Planet.PlanetTile tile = outpost.Tile;
        const double flat = 0.5;
        var snap = VOETestHelper.SnapshotSettings();
        bool ok;
        try
        {
            EmpireVOESettings.enableConversionDelay = false;
            EmpireVOESettings.reducedFoundingCostFactor = 0f;
            EmpireVOESettings.townFlatAdditive = (float)flat;
            ok = OutpostConversionUtil.ConvertOutpost(outpost, types[0]);
        }
        finally
        {
            VOETestHelper.RestoreSettings(snap);
        }

        TestAssert.IsTrue(ok, "Zero-cost conversion of a ready town outpost should succeed");
        WorldSettlementFC settlement = f.settlements.FirstOrDefault(s => s.Tile == tile);
        TestAssert.IsNotNull(settlement, "The converted settlement should exist on the outpost's tile");

        WorldObjectComp_OutpostBonus bonusComp = settlement.GetComponent<WorldObjectComp_OutpostBonus>();
        TestAssert.IsNotNull(bonusComp, "The settlement should carry a WorldObjectComp_OutpostBonus");
        TestAssert.IsTrue(bonusComp.HasAnyBonus, "A town conversion with a flat additive should record a bonus");

        // The flat additive lands on EVERY resource, so an arbitrary resource with no skill bonus reads it exactly.
        var probe = new ResourceFC { def = new ResourceTypeDef { defName = "VOE_TownAdditiveProbe", label = "probe" } };
        TestAssert.AreEqual(flat, bonusComp.GetResourceAdditiveModifier(probe), 0.001,
            "The town flat additive should apply to every resource on the converted settlement");

        DestructiveTestUtil.AssertEmpireInvariants(f, "ConvertOutpost_TownAdditive");
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
