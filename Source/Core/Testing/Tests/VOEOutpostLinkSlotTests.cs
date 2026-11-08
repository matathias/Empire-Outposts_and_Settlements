using Outposts;

namespace EmpireVOE;

/// <summary>
/// Coverage for the slot-keyed link accessors on <see cref="WorldObjectComp_OutpostLinks"/> (each slot
/// must read and write its own saved field), the never-null outpost label, and the per-settlement
/// proximity cache. All run on unspawned fixtures, so nothing in the world changes.
/// </summary>
public static class VOEOutpostLinkSlotTests
{
    [EmpireTest("VOE.Links")]
    public static void LinkSlots_MapToTheirOwnFields()
    {
        Outpost delivery = VOETestHelper.MakeUnspawnedOutpost("Delivery Fixture");
        Outpost financing = VOETestHelper.MakeUnspawnedOutpost("Financing Fixture");
        WorldObjectComp_OutpostLinks links = new() { deliveryOutpost = delivery, financingOutpost = financing };

        TestAssert.AreEqual(delivery, links.StoredLink(OutpostLinkSlot.Delivery), "the Delivery slot should read deliveryOutpost");
        TestAssert.AreEqual(financing, links.StoredLink(OutpostLinkSlot.Financing), "the Financing slot should read financingOutpost");
        TestAssert.AreEqual(delivery, links.GetDeliveryOutpost(), "GetDeliveryOutpost should return the delivery link");
        TestAssert.AreEqual(financing, links.GetFinancingOutpost(), "GetFinancingOutpost should return the financing link");

        links.financingOutpost = null;
        TestAssert.AreEqual(delivery, links.GetDeliveryOutpost(), "clearing financing must not touch delivery");
        TestAssert.IsNull(links.GetFinancingOutpost(), "an unset financing link should read null");
    }

    [EmpireTest("VOE.Links")]
    public static void DisplayLabelCap_UnnamedOutpost_FallsBackToDefLabel()
    {
        Outpost outpost = VOETestHelper.MakeUnspawnedOutpost();
        TestAssert.IsNull(outpost.Name, "the fixture outpost should have no name");

        string label = outpost.DisplayLabelCap();
        TestAssert.IsFalse(label.NullOrEmpty(), "an unnamed outpost should still get a label");
        TestAssert.AreEqual(outpost.def.label.CapitalizeFirst(), label, "an unnamed outpost should use its def label");
    }

    [EmpireTest("VOE.Links")]
    public static void DisplayLabelCap_NamedOutpost_UsesName()
    {
        Outpost outpost = VOETestHelper.MakeUnspawnedOutpost("north camp");
        TestAssert.AreEqual("North camp", outpost.DisplayLabelCap(), "a named outpost should use its capitalised name");
    }

    [EmpireTest("VOE.Links")]
    public static void DisplayLabelCap_NonOutpost_UsesLabelCap()
    {
        WorldObject other = new() { def = WorldObjectDefOf.Caravan };
        TestAssert.AreEqual(other.LabelCap, other.DisplayLabelCap(), "a non-outpost world object should keep its LabelCap");
    }

    /*-*-*- OutpostProximityCache -*-*-*/

    private static WorldSettlementFC FixtureSettlement(int tileId) =>
        new() { Tile = new PlanetTile(tileId, Find.WorldGrid.Surface) };

    [EmpireTest("VOE.Proximity")]
    public static void ProximityCache_BuildsOncePerTileUntilInvalidated()
    {
        int builds = 0;
        OutpostProximityCache<object> cache = new(_ =>
        {
            builds++;
            return new object();
        });
        WorldSettlementFC a = FixtureSettlement(0);
        WorldSettlementFC b = FixtureSettlement(1);

        TestAssert.IsNull(cache.GetOrBuild(null), "a null settlement should get no entry");
        TestAssert.AreEqual(0, builds, "a null settlement should not build an entry");

        object first = cache.GetOrBuild(a);
        TestAssert.IsTrue(ReferenceEquals(first, cache.GetOrBuild(a)), "a second read should return the cached entry");
        TestAssert.AreEqual(1, builds, "the entry should be built once per tile");

        object other = cache.GetOrBuild(b);
        TestAssert.IsFalse(ReferenceEquals(first, other), "a different tile should get its own entry");
        TestAssert.AreEqual(2, builds, "a different tile should build its own entry");

        cache.Invalidate();
        object rebuilt = cache.GetOrBuild(a);
        TestAssert.IsFalse(ReferenceEquals(first, rebuilt), "Invalidate should drop the cached entry");
        TestAssert.AreEqual(3, builds, "the entry should be rebuilt after Invalidate");
    }
}
