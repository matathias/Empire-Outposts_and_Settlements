using Outposts;

namespace EmpireVOE;

/// <summary>
/// ISilverPaymentModifier that lets a settlement's designated financing outpost cover part of a
/// silver payment before it falls through to player coffers. Reports how much the outpost can
/// cover (reducing the effective amount) but defers the actual drain to a commit action, so the
/// outpost is only touched once the payment is confirmed affordable and actually commits.
/// Registered with SilverPaymentRegistry during init.
/// </summary>
public class OutpostFinancer : ISilverPaymentModifier
{
    public static readonly OutpostFinancer Instance = new();

    public void ModifyPayment(SilverPaymentContext context)
    {
        if (!EmpireVOESettings.FinancingActive) return;
        if (context.Settlement is null) return;

        WorldObjectComp_OutpostLinks links = context.Settlement.GetComponent<WorldObjectComp_OutpostLinks>();
        Outpost outpost = links?.GetFinancingOutpost();
        if (outpost is null) return;

        // Validate outpost still exists on the world map
        if (Find.WorldObjects.WorldObjectAt<Outpost>(outpost.Tile) is null)
        {
            VOELog.Message($"Financing outpost no longer exists, clearing for {context.Settlement.Name}");
            links.financingOutpost = null;
            return;
        }

        // Sum available silver WITHOUT touching the outpost, so a pure affordability query stays
        // side-effect-free and a payment that turns out unaffordable never drains the outpost.
        int available = 0;
        foreach (Thing t in outpost.Things)
        {
            if (t.def == ThingDefOf.Silver) available += t.stackCount;
        }

        int cover = Math.Min(available, context.Amount);
        if (cover <= 0) return;

        context.Amount -= cover;
        context.Commit(() => DrainOutpost(outpost, cover));
    }

    // Destroys exactly <paramref name="amount"/> silver from the outpost. cover never exceeds the
    // amount counted above, so this always fully drains what it promised; the guard is defensive.
    private static void DrainOutpost(Outpost outpost, int amount)
    {
        int remaining = amount;

        // Copy the items so removing from the outpost's inventory doesn't disturb iteration.
        List<Thing> things = new(outpost.Things);
        foreach (Thing t in things)
        {
            if (remaining <= 0) break;
            if (t.def != ThingDefOf.Silver) continue;

            if (t.stackCount <= remaining)
            {
                remaining -= t.stackCount;
                outpost.TakeItem(t).Destroy(DestroyMode.Vanish);
            }
            else
            {
                outpost.TakeItem(t.SplitOff(remaining)).Destroy(DestroyMode.Vanish);
                remaining = 0;
            }
        }

        if (remaining > 0)
        {
            VOELog.Message($"Financing outpost {outpost.LabelCap} drained less than promised (short by {remaining})");
        }
    }
}
