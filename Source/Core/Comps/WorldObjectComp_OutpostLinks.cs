using Outposts;

namespace EmpireVOE;

public class WorldObjectCompProperties_OutpostLinks : WorldObjectCompProperties
{
    public WorldObjectCompProperties_OutpostLinks()
    {
        compClass = typeof(WorldObjectComp_OutpostLinks);
    }
}

/// <summary>Which of a settlement's two outpost links a member reads or writes.</summary>
public enum OutpostLinkSlot
{
    Delivery,
    Financing,
}

/// <summary>
/// Stores per-settlement delivery destination and financing outpost assignments.
/// Provides gizmos for configuring these relationships on the world map.
/// </summary>
public class WorldObjectComp_OutpostLinks : WorldObjectComp
{
    public Outpost deliveryOutpost;
    public Outpost financingOutpost;

    private WorldSettlementFC Settlement => (WorldSettlementFC)parent;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_References.Look(ref deliveryOutpost, "voeDeliveryOutpost");
        Scribe_References.Look(ref financingOutpost, "voeFinancingOutpost");
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        if (EmpireVOESettings.disableIntegration) yield break;

        bool hasOutposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>().Any();
        if (!hasOutposts) yield break;

        if (EmpireVOESettings.enableDelivery)
            yield return CreateDeliveryGizmo();
        if (EmpireVOESettings.enableFinancing)
            yield return CreateFinancingGizmo();
    }

    /// <summary>
    /// Returns the delivery outpost if it still exists, clearing stale references.
    /// </summary>
    public Outpost GetDeliveryOutpost() => GetLink(OutpostLinkSlot.Delivery);

    /// <summary>
    /// Returns the financing outpost if it still exists, clearing stale references.
    /// </summary>
    public Outpost GetFinancingOutpost() => GetLink(OutpostLinkSlot.Financing);

    /// <summary>The stored outpost for <paramref name="slot"/>, without the stale-reference check.</summary>
    internal Outpost StoredLink(OutpostLinkSlot slot) => LinkField(slot);

    private ref Outpost LinkField(OutpostLinkSlot slot)
    {
        if (slot == OutpostLinkSlot.Delivery) return ref deliveryOutpost;
        return ref financingOutpost;
    }

    private Outpost GetLink(OutpostLinkSlot slot)
    {
        ref Outpost link = ref LinkField(slot);
        if (link is not null && link.Destroyed)
        {
            link = null;
        }
        return link;
    }

    /// <summary>The menu entry and gizmo label for an unset link: the player's tax map, or "None".</summary>
    private static string UnsetLabel(OutpostLinkSlot slot) => slot == OutpostLinkSlot.Delivery
        ? "FCVOE_PlayerTaxMap".Translate().ToString()
        : "None".Translate().ToString();

    private Command_Action CreateDeliveryGizmo()
        => CreateLinkGizmo(OutpostLinkSlot.Delivery, "FCVOE_DeliveryOutpostLabel", "FCVOE_DeliveryOutpostDesc", OpenDeliveryMenu);

    private Command_Action CreateFinancingGizmo()
        => CreateLinkGizmo(OutpostLinkSlot.Financing, "FCVOE_FinancingOutpostLabel", "FCVOE_FinancingOutpostDesc", OpenFinancingMenu);

    private Command_Action CreateLinkGizmo(OutpostLinkSlot slot, string labelKey, string descKey, Action openMenu)
    {
        Outpost current = GetLink(slot);
        string currentLabel = current is not null
            ? current.DisplayLabelCap()
            : UnsetLabel(slot);

        // ExpandingIcon goes through WorldObject: VOE's Outposts.dll is built against pre-1.7 Unity,
        // so the compiler can't read its override signatures. The calls are still virtual.
        return new Command_Action
        {
            defaultLabel = labelKey.Translate(),
            defaultDesc = descKey.Translate(currentLabel),
            icon = current is not null
                ? ((WorldObject)current).ExpandingIcon
                : TexCommand.Install,
            action = openMenu
        };
    }

    // --- Shared menu builders (reused by both the world-map gizmos and the Outposts tab) ---

    /// <summary>Opens the float menu for choosing this settlement's tax-delivery destination.</summary>
    internal void OpenDeliveryMenu() => OpenLinkMenu(OutpostLinkSlot.Delivery);

    /// <summary>Opens the float menu for choosing this settlement's financing outpost.</summary>
    internal void OpenFinancingMenu() => OpenLinkMenu(OutpostLinkSlot.Financing);

    private void OpenLinkMenu(OutpostLinkSlot slot)
    {
        List<FloatMenuOption> options =
        [
            new FloatMenuOption(
                UnsetLabel(slot),
                delegate { LinkField(slot) = null; }),
        ];

        foreach (Outpost outpost in OutpostsByDistance())
        {
            Outpost op = outpost;
            options.Add(new FloatMenuOption(
                op.DisplayLabelCap(),
                delegate { LinkField(slot) = op; },
                ((WorldObject)op).ExpandingIcon,
                ((WorldObject)op).ExpandingIconColor));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    internal void ClearDelivery() => deliveryOutpost = null;
    internal void ClearFinancing() => financingOutpost = null;

    private IEnumerable<Outpost> OutpostsByDistance()
    {
        // Same-layer outposts first, ordered by distance; cross-layer ones (infinitely far) sink
        // to the bottom.
        return Find.WorldObjects.AllWorldObjects
            .OfType<Outpost>()
            .OrderBy(o => o.Tile.ApproxDistanceSameLayer(parent.Tile));
    }
}
