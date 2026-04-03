using FactionColonies;
using HarmonyLib;
using Outposts;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Prefix on FactionFC.AddEvent — redirects taxColony events to the
    /// settlement's designated delivery outpost if one is set.
    /// </summary>
    [HarmonyPatch(typeof(FactionFC))]
    [HarmonyPatch("AddEvent")]
    public static class Patch_AddEvent
    {
        public static void Prefix(ref FCEvent fcevent)
        {
            if (EmpireVOESettings.disableIntegration) return;
            if (fcevent is null || fcevent.def != FCEventDefOf.taxColony) return;
            if (fcevent.source == -1) return;

            WorldSettlementFC settlement = FactionCache.FactionComp.ReturnSettlementByLocation(fcevent.source);
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

            DeliveryUtil.DebugLog($"Redirected tax event from {settlement.Name} to outpost {outpost.LabelCap}");
        }
    }

    /// <summary>
    /// Prefix on PaymentUtil.DeliverThings(FCEvent, Letter, Message) — intercepts
    /// delivery of taxColony events targeting outpost tiles. Adds goods directly
    /// to the outpost instead of spawning on the player's map.
    /// </summary>
    [HarmonyPatch(typeof(PaymentUtil))]
    [HarmonyPatch("DeliverThings", typeof(FCEvent), typeof(Letter), typeof(Message))]
    public static class Patch_DeliverThings
    {
        public static bool Prefix(ref FCEvent evt)
        {
            if (EmpireVOESettings.disableIntegration) return true;
            if (evt is null || evt.def != FCEventDefOf.taxColony) return true;
            if (evt.source == -1) return true;

            Map taxMap = FactionCache.FactionComp.TaxMap;
            if (taxMap != null && evt.location == taxMap.Tile) return true;

            Outpost destination = Find.WorldObjects.WorldObjectAt<Outpost>(evt.location);
            WorldSettlementFC source =
                FactionCache.FactionComp.ReturnSettlementByLocation(evt.source);

            if (destination != null)
            {
                // Deliver goods to outpost
                foreach (Thing t in evt.goods)
                {
                    if (t is Pawn p)
                        destination.AddPawn(p);
                    else
                        destination.AddItem(t);
                }

                string sourceName = source != null ? source.Name : "Unknown";
                Find.LetterStack.ReceiveLetter(
                    "VOE_TaxDeliveredToOutpost".Translate(sourceName, destination.LabelCap),
                    "VOE_TaxDeliveredToOutpostDesc".Translate(
                        sourceName, destination.LabelCap,
                        DeliveryUtil.GoodsToString(evt.goods)),
                    LetterDefOf.PositiveEvent);

                DeliveryUtil.DebugLog("Delivered taxes from " + sourceName + " to outpost " + destination.LabelCap);
                return false;
            }

            // Outpost no longer exists — redirect to tax map
            DeliveryUtil.DebugLog("Delivery outpost at tile " + evt.location + " no longer exists. Redirecting to tax map.");
            Messages.Message("VOE_TaxRedirectedDesc".Translate(
                source != null ? source.Name : "Unknown",
                "VOE_PlayerTaxMap".Translate()),
                MessageTypeDefOf.NeutralEvent);

            // Clear the stale delivery destination
            if (source != null)
                WorldComponent_VOETracker.SetDeliveryDestination(source, null);

            // Create redirect event to tax map
            FCEvent redirect = FCEventMaker.MakeEvent(FCEventDefOf.taxColony);
            redirect.source = evt.source;
            redirect.goods = evt.goods;
            redirect.location = taxMap?.Tile ?? evt.source;
            redirect.timeTillTrigger = Find.TickManager.TicksGame + TravelUtil.ReturnTicksToArrive(evt.source, redirect.location);
            redirect.customDescription = "VOE_TaxRedirectedDesc".Translate(source != null ? source.Name : "Unknown", "VOE_PlayerTaxMap".Translate());
            redirect.hasCustomDescription = true;

            WorldComponent_VOETracker.SetRedirected(evt.source);
            FactionCache.FactionComp.AddEvent(redirect);
            return false;
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
