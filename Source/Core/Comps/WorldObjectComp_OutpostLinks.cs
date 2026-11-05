using Outposts;

namespace EmpireVOE;

public class WorldObjectCompProperties_OutpostLinks : WorldObjectCompProperties
{
    public WorldObjectCompProperties_OutpostLinks()
    {
        compClass = typeof(WorldObjectComp_OutpostLinks);
    }
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
    public Outpost GetDeliveryOutpost()
    {
        if (deliveryOutpost is not null && deliveryOutpost.Destroyed)
        {
            deliveryOutpost = null;
        }
        return deliveryOutpost;
    }

    /// <summary>
    /// Returns the financing outpost if it still exists, clearing stale references.
    /// </summary>
    public Outpost GetFinancingOutpost()
    {
        if (financingOutpost is not null && financingOutpost.Destroyed)
        {
            financingOutpost = null;
        }
        return financingOutpost;
    }

    private Command_Action CreateDeliveryGizmo()
    {
        Outpost current = GetDeliveryOutpost();
        string currentLabel = current is not null
            ? current.LabelCap
            : "FCVOE_PlayerTaxMap".Translate().ToString();

        return new Command_Action
        {
            defaultLabel = "FCVOE_DeliveryOutpostLabel".Translate(),
            defaultDesc = "FCVOE_DeliveryOutpostDesc".Translate(currentLabel),
            icon = current is not null
                ? current.ExpandingIcon
                : TexCommand.Install,
            action = OpenDeliveryMenu
        };
    }

    private Command_Action CreateFinancingGizmo()
    {
        Outpost current = GetFinancingOutpost();
        string currentLabel = current is not null
            ? current.LabelCap
            : "None".Translate().ToString();

        return new Command_Action
        {
            defaultLabel = "FCVOE_FinancingOutpostLabel".Translate(),
            defaultDesc = "FCVOE_FinancingOutpostDesc".Translate(currentLabel),
            icon = current is not null
                ? current.ExpandingIcon
                : TexCommand.Install,
            action = OpenFinancingMenu
        };
    }

    // --- Shared menu builders (reused by both the world-map gizmos and the Outposts tab) ---

    /// <summary>Opens the float menu for choosing this settlement's tax-delivery destination.</summary>
    internal void OpenDeliveryMenu()
    {
        List<FloatMenuOption> options =
        [
            new FloatMenuOption(
                "FCVOE_PlayerTaxMap".Translate(),
                delegate { deliveryOutpost = null; }),
        ];

        // ExpandingIcon goes through WorldObject: VOE's Outposts.dll is built against pre-1.7 Unity,
        // so the compiler can't read its override signatures. The calls are still virtual.
        foreach (Outpost outpost in OutpostsByDistance())
        {
            Outpost op = outpost;
            options.Add(new FloatMenuOption(
                op.LabelCap,
                delegate { deliveryOutpost = op; },
                ((WorldObject)op).ExpandingIcon,
                ((WorldObject)op).ExpandingIconColor));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>Opens the float menu for choosing this settlement's financing outpost.</summary>
    internal void OpenFinancingMenu()
    {
        List<FloatMenuOption> options =
        [
            new FloatMenuOption(
                "None".Translate(),
                delegate { financingOutpost = null; }),
        ];

        foreach (Outpost outpost in OutpostsByDistance())
        {
            Outpost op = outpost;
            options.Add(new FloatMenuOption(
                op.LabelCap,
                delegate { financingOutpost = op; },
                ((WorldObject)op).ExpandingIcon,
                ((WorldObject)op).ExpandingIconColor));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    internal void ClearDelivery() => deliveryOutpost = null;
    internal void ClearFinancing() => financingOutpost = null;

    private IEnumerable<Outpost> OutpostsByDistance()
    {
        // Same-layer outposts first, ordered by true distance; cross-layer ones sink to the
        // bottom (ApproxDistanceInTiles is layer-local, so its cross-layer value is meaningless).
        return Find.WorldObjects.AllWorldObjects
            .OfType<Outpost>()
            .OrderBy(o => o.Tile.Layer == parent.Tile.Layer ? 0 : 1)
            .ThenBy(o => o.Tile.Layer == parent.Tile.Layer
                ? Find.WorldGrid.ApproxDistanceInTiles(o.Tile, parent.Tile)
                : 0f);
    }
}
