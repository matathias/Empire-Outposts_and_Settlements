using Outposts;

namespace EmpireVOE;

/// <summary>
/// Implements <see cref="ITaxDeliveryInterceptor"/> to redirect taxColony events
/// to VOE Outposts designated as delivery destinations.
/// </summary>
public class OutpostDeliveryInterceptor : ITaxDeliveryInterceptor
{
    public static readonly OutpostDeliveryInterceptor Instance = new();

    public void OnTaxEventCreated(TaxDeliveryContext context)
    {
        if (!EmpireVOESettings.DeliveryActive) return;

        FCEvent fcevent = context.Event;
        WorldSettlementFC settlement = context.Settlement;
        if (settlement is null) return;

        // Check if this event was already redirected (prevents re-redirect loop)
        if (WorldComponent_VOETracker.IsRedirected(fcevent.source))
        {
            WorldComponent_VOETracker.ClearRedirected(fcevent.source);
            DeliveryUtil.DebugLog($"Skipping redirect for already-redirected event from {settlement.Name}");
            return;
        }

        WorldObjectComp_OutpostLinks links = settlement.GetComponent<WorldObjectComp_OutpostLinks>();
        Outpost outpost = links?.GetDeliveryOutpost();
        if (outpost is null) return;

        // Validate outpost still exists on the world map
        if (Find.WorldObjects.WorldObjectAt<Outpost>(outpost.Tile) is null)
        {
            Messages.Message("FCVOE_InvalidDeliveryOutpost".Translate(settlement.Name), MessageTypeDefOf.NeutralEvent);
            links.deliveryOutpost = null;
            return;
        }

        // Redirect the event to the outpost tile
        fcevent.location = outpost.Tile;
        fcevent.timeTillTrigger = Find.TickManager.TicksGame + TravelUtil.ReturnTicksToArrive(fcevent.source, fcevent.location);
        fcevent.customDescription = "FCVOE_DeliveryEventDesc".Translate(settlement.Name, outpost.LabelCap, DeliveryUtil.GoodsToString(fcevent.goods));
        fcevent.hasCustomDescription = true;
        context.Redirected = true;

        DeliveryUtil.DebugLog($"Redirected tax event from {settlement.Name} to outpost {outpost.LabelCap}");
    }

    public bool TryDeliverGoods(TaxDeliveryContext context)
    {
        if (!EmpireVOESettings.DeliveryActive) return false;

        FCEvent evt = context.Event;
        if (!evt.source.Valid) return false;

        Map taxMap = FindFC.TaxMap;
        // Ordinary tax events are bound for the capital (FCEventMaker sets location = capitalLocation);
        // only events we redirected point at an outpost tile. Test against the capital tile directly:
        // it holds even when the capital's map is unloaded and TaxMap falls back to another home map,
        // whereas the old `location != taxMap.Tile` check misclassified every ordinary tax event as an
        // outpost delivery and hijacked it.
        PlanetTile capital = FindFC.FactionComp?.capitalLocation ?? PlanetTile.Invalid;
        if (capital.Valid && evt.location == capital) return false;
        if (taxMap is not null && evt.location == taxMap.Tile) return false;

        Outpost destination = Find.WorldObjects.WorldObjectAt<Outpost>(evt.location);
        WorldSettlementFC source = context.Settlement;

        if (destination is not null)
        {
            // Deliver goods to outpost
            List<Pawn> reroutedPawns = null;
            foreach (Thing t in evt.goods)
            {
                if (t is Pawn p)
                {
                    // AddPawn refuses ideology-unwilling / vehicle pawns. Rather than strand a
                    // refused pawn, redirect it straight to the player's colony.
                    if (!destination.AddPawn(p))
                        (reroutedPawns ??= []).Add(p);
                }
                else
                    destination.AddItem(t);
            }

            if (reroutedPawns is not null)
            {
                // Drop the rerouted pawns from the outpost delivery's goods so its arrival letter
                // (built below from evt.goods) doesn't list pawns that never actually joined it.
                foreach (Pawn rerouted in reroutedPawns)
                    evt.goods.Remove(rerouted);

                string rerouteLabel = "FCVOE_DeliveryPawnRerouted".Translate(destination.LabelCap);
                string rerouteText = "FCVOE_DeliveryPawnReroutedDesc".Translate(
                    destination.LabelCap, DeliveryUtil.GoodsToString(reroutedPawns.ConvertAll<Thing>(p => p)));
                VOEPawnUtil.DeliverPawnsToPlayer(reroutedPawns, destination.Tile, rerouteLabel, rerouteText);
            }

            // Notify via the base mod's delivery notification utility so the
            // outpost delivery honors the player's FCSettings.taxNotificationMode
            // (letter / message / both / none). Goods are already deposited above,
            // so we want only the notification step -- not DeliverThings/DeliveryEvent,
            // which would also try to physically transport goods to the outpost tile.
            string sourceName = source is not null ? source.Name : "Unknown";
            string label = "FCVOE_TaxDeliveredToOutpost".Translate(sourceName, destination.LabelCap);
            string text = "FCVOE_TaxDeliveredToOutpostDesc".Translate(
                sourceName, destination.LabelCap, DeliveryUtil.GoodsToString(evt.goods));

            evt.let = LetterMaker.MakeLetter(label, text, LetterDefOf.PositiveEvent);
            evt.msg = new Message(label, MessageTypeDefOf.PositiveEvent);
            DeliveryNotification.MakeDeliveryLetterAndMessage(evt);

            DeliveryUtil.DebugLog("Delivered taxes from " + sourceName + " to outpost " + destination.LabelCap);
            return true;
        }

        // Outpost no longer exists — redirect to tax map
        DeliveryUtil.DebugLog("Delivery outpost at tile " + evt.location + " no longer exists. Redirecting to tax map.");
        Messages.Message("FCVOE_TaxRedirectedDesc".Translate(
            source is not null ? source.Name : "Unknown",
            "FCVOE_PlayerTaxMap".Translate()),
            MessageTypeDefOf.NeutralEvent);

        // Clear the stale delivery destination
        if (source is not null)
        {
            WorldObjectComp_OutpostLinks links = source.GetComponent<WorldObjectComp_OutpostLinks>();
            if (links is not null)
                links.deliveryOutpost = null;
        }

        // No player home map at all: there is nowhere to redirect to. Building a redirect whose
        // location falls back to evt.source would re-enter this interceptor every cycle (infinite
        // loop + message spam). Bail out and let the base mod log the undeliverable tax instead.
        if (taxMap is null)
        {
            DeliveryUtil.DebugLog("No tax map available; skipping redirect to avoid a re-interception loop.");
            return false;
        }

        // Create redirect event to tax map
        FCEvent redirect = FCEventMaker.MakeEvent(FCEventDefOf.taxColony);
        redirect.source = evt.source;
        redirect.goods = evt.goods;
        redirect.location = taxMap.Tile;
        // Preserve the delivery mode locked at the event's creation (MakeEvent leaves it default) and
        // start the progress bar at the beginning of this new travel leg.
        redirect.deliveryMode = evt.deliveryMode;
        redirect.tickStarted = Find.TickManager.TicksGame;
        redirect.timeTillTrigger = Find.TickManager.TicksGame + TravelUtil.ReturnTicksToArrive(evt.source, redirect.location);
        redirect.customDescription = "FCVOE_TaxRedirectedDesc".Translate(source is not null ? source.Name : "Unknown", "FCVOE_PlayerTaxMap".Translate());
        redirect.hasCustomDescription = true;

        WorldComponent_VOETracker.SetRedirected(evt.source);
        FindFC.EventManager.AddEvent(redirect);
        return true;
    }
}
