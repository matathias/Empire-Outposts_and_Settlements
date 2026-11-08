namespace EmpireVOE;

/// <summary>
/// Coverage for how the tax-delivery interceptor tells its own outpost deliveries apart from ordinary
/// tax events. Only events stamped by <see cref="WorldComponent_VOETracker.MarkOutpostBound"/> (or an
/// unstamped one still headed for a live outpost) are handled; anything else, such as a tax event bound
/// for an unset or moved capital, falls through to core delivery. Uses fabricated loadIDs and tiles no
/// real event or world object owns, and leaves the tracker as it found it.
/// </summary>
public static class VOEDeliveryStampTests
{
    // loadIDs far above anything the event manager hands out.
    private const int FakeLoadId = int.MaxValue - 11;
    private const int OtherFakeLoadId = int.MaxValue - 12;

    [EmpireTest("VOE.Delivery")]
    public static void OutpostBoundStamp_ConsumedExactlyOnce()
    {
        FCEvent stamped = new() { loadID = FakeLoadId };
        FCEvent unstamped = new() { loadID = OtherFakeLoadId };
        try
        {
            TestAssert.IsFalse(WorldComponent_VOETracker.ConsumeOutpostBound(stamped), "an unmarked event should not read as stamped");

            WorldComponent_VOETracker.MarkOutpostBound(stamped);
            TestAssert.IsFalse(WorldComponent_VOETracker.ConsumeOutpostBound(unstamped),
                "stamping one event must not stamp another");
            TestAssert.IsTrue(WorldComponent_VOETracker.ConsumeOutpostBound(stamped), "a marked event should read as stamped");
            TestAssert.IsFalse(WorldComponent_VOETracker.ConsumeOutpostBound(stamped),
                "consuming the stamp should forget it, so the event is handled once");
        }
        finally
        {
            WorldComponent_VOETracker.ConsumeOutpostBound(stamped);
            WorldComponent_VOETracker.ConsumeOutpostBound(unstamped);
        }
    }

    [EmpireTest("VOE.Delivery")]
    public static void OutpostBoundStamp_NullEvent_Safe()
    {
        TestAssert.DoesNotThrow(() => WorldComponent_VOETracker.MarkOutpostBound(null), "marking a null event should be a no-op");
        TestAssert.IsFalse(WorldComponent_VOETracker.ConsumeOutpostBound(null), "a null event is never stamped");
    }

    /// <summary>
    /// An ordinary tax event whose destination is not an outpost (here: an unset capital) must not be
    /// taken over by the interceptor, even with delivery enabled.
    /// </summary>
    [EmpireTest("VOE.Delivery")]
    public static void TryDeliverGoods_UnstampedEventNotBoundForOutpost_FallsThrough()
    {
        bool deliveryBefore = EmpireVOESettings.enableDelivery;
        bool integrationBefore = EmpireVOESettings.disableIntegration;
        FCEvent evt = new()
        {
            loadID = FakeLoadId,
            def = FCEventDefOf.taxColony,
            source = new PlanetTile(0, Find.WorldGrid.Surface),
            location = PlanetTile.Invalid,
        };
        try
        {
            EmpireVOESettings.disableIntegration = false;
            EmpireVOESettings.enableDelivery = true;

            bool handled = OutpostDeliveryInterceptor.Instance.TryDeliverGoods(new TaxDeliveryContext(evt, null));
            TestAssert.IsFalse(handled, "a tax event bound for an unset capital should be left to core delivery");
        }
        finally
        {
            EmpireVOESettings.enableDelivery = deliveryBefore;
            EmpireVOESettings.disableIntegration = integrationBefore;
            WorldComponent_VOETracker.ConsumeOutpostBound(evt);
        }
    }

    /// <summary>
    /// The interceptor is registered once at startup as a persistent participant, so it is still
    /// registered in a loaded game even though every load resets the registries.
    /// </summary>
    [EmpireTest("VOE.Delivery")]
    public static void DeliveryInterceptor_StaysRegisteredAfterLoad()
    {
        TestAssert.Contains(TaxDeliveryRegistry.Interceptors, (ITaxDeliveryInterceptor)OutpostDeliveryInterceptor.Instance,
            "the outpost delivery interceptor should survive the registry reset that runs on every load");
    }

    /// <summary>
    /// Turning the master integration off and back on from the main menu must leave the outpost financer
    /// registered. Off unregisters it for good (it is dropped from the persistent set); on has to add it
    /// back even without a loaded world, or financing stays dead for the rest of the session.
    /// </summary>
    [EmpireTest("VOE.Delivery", GameState = TestGameState.MainMenu)]
    public static void Financer_ReregisteredAfterIntegrationToggleAtMainMenu()
    {
        bool integrationBefore = EmpireVOESettings.disableIntegration;
        try
        {
            EmpireVOESettings.disableIntegration = true;
            WorldObjectComp_EmpireOutpost.UnregisterAll();
            EmpireVOESettings.disableIntegration = false;
            WorldObjectComp_EmpireOutpost.ReregisterAll();

            TestAssert.Contains(SilverPaymentRegistry.Modifiers, (ISilverPaymentModifier)OutpostFinancer.Instance,
                "re-enabling the integration should register the outpost financer again");
        }
        finally
        {
            EmpireVOESettings.disableIntegration = integrationBefore;
            // Put the startup registrations back whatever the outcome.
            EmpireRegistry.RegisterPersistent(OutpostFinancer.Instance);
            OutpostMainTab.SetRegistered(EmpireVOESettings.OutpostMainTabActive);
        }
    }
}
