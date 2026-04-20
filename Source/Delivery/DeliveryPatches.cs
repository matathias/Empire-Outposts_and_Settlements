using FactionColonies;
using FactionColonies.util;
using HarmonyLib;
using Outposts;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Implements <see cref="ITaxDeliveryInterceptor"/> to redirect taxColony events
    /// to VOE Outposts designated as delivery destinations.
    /// </summary>
    public class OutpostDeliveryInterceptor : ITaxDeliveryInterceptor
    {
        public static readonly OutpostDeliveryInterceptor Instance = new OutpostDeliveryInterceptor();

        public void OnTaxEventCreated(TaxDeliveryContext context)
        {
            if (EmpireVOESettings.disableIntegration) return;

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

            Outpost outpost = WorldComponent_VOETracker.GetDeliveryDestination(settlement);
            if (outpost is null) return;

            // Validate outpost still exists
            if (Find.WorldObjects.WorldObjectAt<Outpost>(outpost.Tile) is null)
            {
                Messages.Message("VOE_InvalidDeliveryOutpost".Translate(settlement.Name), MessageTypeDefOf.NeutralEvent);
                WorldComponent_VOETracker.SetDeliveryDestination(settlement, null);
                return;
            }

            // Redirect the event to the outpost tile
            fcevent.location = outpost.Tile;
            fcevent.timeTillTrigger = Find.TickManager.TicksGame + TravelUtil.ReturnTicksToArrive(fcevent.source, fcevent.location);
            fcevent.customDescription = "VOE_DeliveryEventDesc".Translate(settlement.Name, outpost.LabelCap, DeliveryUtil.GoodsToString(fcevent.goods));
            fcevent.hasCustomDescription = true;
            context.Redirected = true;

            DeliveryUtil.DebugLog($"Redirected tax event from {settlement.Name} to outpost {outpost.LabelCap}");
        }

        public bool TryDeliverGoods(TaxDeliveryContext context)
        {
            if (EmpireVOESettings.disableIntegration) return false;

            FCEvent evt = context.Event;
            if (evt.source == -1) return false;

            Map taxMap = FactionCache.FactionComp.TaxMap;
            if (taxMap is object && evt.location == taxMap.Tile) return false;

            Outpost destination = Find.WorldObjects.WorldObjectAt<Outpost>(evt.location);
            WorldSettlementFC source = context.Settlement;

            if (destination is object)
            {
                // Deliver goods to outpost
                foreach (Thing t in evt.goods)
                {
                    if (t is Pawn p)
                        destination.AddPawn(p);
                    else
                        destination.AddItem(t);
                }

                string sourceName = source is object ? source.Name : "Unknown";
                Find.LetterStack.ReceiveLetter(
                    "VOE_TaxDeliveredToOutpost".Translate(sourceName, destination.LabelCap),
                    "VOE_TaxDeliveredToOutpostDesc".Translate(
                        sourceName, destination.LabelCap,
                        DeliveryUtil.GoodsToString(evt.goods)),
                    LetterDefOf.PositiveEvent);

                DeliveryUtil.DebugLog("Delivered taxes from " + sourceName + " to outpost " + destination.LabelCap);
                return true;
            }

            // Outpost no longer exists — redirect to tax map
            DeliveryUtil.DebugLog("Delivery outpost at tile " + evt.location + " no longer exists. Redirecting to tax map.");
            Messages.Message("VOE_TaxRedirectedDesc".Translate(
                source is object ? source.Name : "Unknown",
                "VOE_PlayerTaxMap".Translate()),
                MessageTypeDefOf.NeutralEvent);

            // Clear the stale delivery destination
            if (source is object)
                WorldComponent_VOETracker.SetDeliveryDestination(source, null);

            // Create redirect event to tax map
            FCEvent redirect = FCEventMaker.MakeEvent(FCEventDefOf.taxColony);
            redirect.source = evt.source;
            redirect.goods = evt.goods;
            redirect.location = taxMap?.Tile ?? evt.source;
            redirect.timeTillTrigger = Find.TickManager.TicksGame + TravelUtil.ReturnTicksToArrive(evt.source, redirect.location);
            redirect.customDescription = "VOE_TaxRedirectedDesc".Translate(source is object ? source.Name : "Unknown", "VOE_PlayerTaxMap".Translate());
            redirect.hasCustomDescription = true;

            WorldComponent_VOETracker.SetRedirected(evt.source);
            FactionCache.FactionComp.AddEvent(redirect);
            return true;
        }
    }

    /// <summary>
    /// Postfix on PaymentUtil.GetSilver — adds silver from all distinct
    /// financing outposts to the total available silver count.
    /// </summary>
    [HarmonyPatch(typeof(PaymentUtil))]
    [HarmonyPatch("GetSilver")]
    public static class Patch_GetSilver
    {
        public static void Postfix(ref int __result)
        {
            if (EmpireVOESettings.disableIntegration) return;

            foreach (Outpost outpost in WorldComponent_VOETracker.GetAllDistinctFinancingOutposts())
            {
                if (Find.WorldObjects.WorldObjectAt<Outpost>(outpost.Tile) is null)
                    continue;

                foreach (Thing t in outpost.Things)
                {
                    if (t.def == ThingDefOf.Silver)
                        __result += t.stackCount;
                }
            }
        }
    }
}
