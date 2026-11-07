using Outposts;

namespace EmpireVOE;

/* DESTRUCTIVE: drives OutpostDeliveryInterceptor.OnTaxEventCreated against a live Empire settlement.
   Builds a throwaway taxColony FCEvent (no goods, never actually delivered), temporarily points the
   settlement's delivery link at a transient outpost, and restores the delivery setting, the link, and
   the redirect flag afterward. The transient outpost is a pure probe and is removed. */
public static class VOEDeliveryDestructiveTests
{
    /// <summary>
    /// A tax event whose settlement has a delivery outpost gets redirected: the interceptor flags the
    /// context and retargets the event's location to the outpost tile.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Delivery")]
    public static void Delivery_OnTaxEventCreated_RedirectsToDeliveryOutpost()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_OutpostLinks links = TestAssert.NotNull(settlement.GetComponent<WorldObjectComp_OutpostLinks>(),
            "Every settlement def carries WorldObjectComp_OutpostLinks; this one has none");

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();

        bool deliveryBefore = EmpireVOESettings.enableDelivery;
        bool integrationBefore = EmpireVOESettings.disableIntegration;
        Outpost linkBefore = links.deliveryOutpost;
        try
        {
            EmpireVOESettings.disableIntegration = false;
            EmpireVOESettings.enableDelivery = true;
            links.deliveryOutpost = outpost;
            WorldComponent_VOETracker.ClearRedirected(settlement.Tile); // ensure the loop guard is clear

            FCEvent evt = MakeTaxEvent(settlement.Tile);
            var ctx = new TaxDeliveryContext(evt, settlement);
            OutpostDeliveryInterceptor.Instance.OnTaxEventCreated(ctx);

            TestAssert.IsTrue(ctx.Redirected, "A settlement with a delivery outpost should have its tax event redirected");
            TestAssert.IsTrue(evt.location == outpost.Tile, "The redirected event should point at the delivery outpost tile");
        }
        finally
        {
            EmpireVOESettings.enableDelivery = deliveryBefore;
            EmpireVOESettings.disableIntegration = integrationBefore;
            links.deliveryOutpost = linkBefore;
            WorldComponent_VOETracker.ClearRedirected(settlement.Tile);
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "Delivery_Redirects");
    }

    /// <summary>
    /// The re-redirect loop guard: if the event's source is already flagged as redirected, the interceptor
    /// must NOT redirect again — it consumes (clears) the flag and returns, even with a delivery outpost set.
    /// This is what stops an outpost delivery that falls back to the tax map from bouncing forever.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Delivery")]
    public static void Delivery_OnTaxEventCreated_AlreadyRedirected_SkipsAndConsumesFlag()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_OutpostLinks links = TestAssert.NotNull(settlement.GetComponent<WorldObjectComp_OutpostLinks>(),
            "Every settlement def carries WorldObjectComp_OutpostLinks; this one has none");

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();

        bool deliveryBefore = EmpireVOESettings.enableDelivery;
        bool integrationBefore = EmpireVOESettings.disableIntegration;
        Outpost linkBefore = links.deliveryOutpost;
        try
        {
            EmpireVOESettings.disableIntegration = false;
            EmpireVOESettings.enableDelivery = true;
            links.deliveryOutpost = outpost;
            WorldComponent_VOETracker.SetRedirected(settlement.Tile); // pretend this event was already redirected

            FCEvent evt = MakeTaxEvent(settlement.Tile);
            var ctx = new TaxDeliveryContext(evt, settlement);
            OutpostDeliveryInterceptor.Instance.OnTaxEventCreated(ctx);

            TestAssert.IsFalse(ctx.Redirected, "An already-redirected event must not be redirected a second time");
            TestAssert.IsFalse(WorldComponent_VOETracker.IsRedirected(settlement.Tile),
                "The interceptor should consume (clear) the redirect flag on the skip path");
        }
        finally
        {
            EmpireVOESettings.enableDelivery = deliveryBefore;
            EmpireVOESettings.disableIntegration = integrationBefore;
            links.deliveryOutpost = linkBefore;
            WorldComponent_VOETracker.ClearRedirected(settlement.Tile);
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "Delivery_AlreadyRedirected");
    }

    private static FCEvent MakeTaxEvent(RimWorld.Planet.PlanetTile source)
    {
        FCEvent evt = FCEventMaker.MakeEvent(FCEventDefOf.taxColony);
        evt.source = source;
        evt.location = source;
        evt.goods = [];
        return evt;
    }
}
