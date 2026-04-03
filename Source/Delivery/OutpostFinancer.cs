using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// ISilverPaymentModifier that drains silver from a settlement's designated
    /// financing outpost before falling through to normal player coffers.
    /// Registered with SilverPaymentRegistry during init.
    /// </summary>
    public class OutpostFinancer : ISilverPaymentModifier
    {
        public static readonly OutpostFinancer Instance = new OutpostFinancer();

        public void ModifyPayment(SilverPaymentContext context)
        {
            if (EmpireVOESettings.disableIntegration) return;
            if (context.Settlement is null) return;

            Outpost outpost = WorldComponent_VOETracker.GetFinancingOutpost(context.Settlement);
            if (outpost is null) return;

            // Validate outpost still exists on the world map
            if (Find.WorldObjects.WorldObjectAt<Outpost>(outpost.Tile) == null)
            {
                DeliveryUtil.DebugLog("Financing outpost no longer exists, clearing for " + context.Settlement.Name);
                WorldComponent_VOETracker.SetFinancingOutpost(context.Settlement, null);
                return;
            }

            int remaining = context.Amount;
            bool deducted = false;

            // Drain silver stacks from outpost inventory
            List<Thing> things = outpost.Things.ToList();
            foreach (Thing t in things)
            {
                if (remaining <= 0) break;
                if (t.def != ThingDefOf.Silver) continue;

                if (t.stackCount <= remaining)
                {
                    remaining -= t.stackCount;
                    outpost.TakeItem(t).Destroy(DestroyMode.Vanish);
                    deducted = true;
                }
                else
                {
                    outpost.TakeItem(t.SplitOff(remaining)).Destroy(DestroyMode.Vanish);
                    remaining = 0;
                    deducted = true;
                }
            }

            if (deducted && remaining > 0)
            {
                DeliveryUtil.DebugLog("Financing outpost " + outpost.LabelCap + " partially depleted. Remaining: " + remaining);
                Messages.Message("VOE_FinancingPartiallyDepleted".Translate(outpost.LabelCap, remaining), MessageTypeDefOf.NeutralEvent);
            }

            context.Amount = remaining;
        }
    }
}
