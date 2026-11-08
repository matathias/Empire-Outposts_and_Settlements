namespace EmpireVOE;

/// <summary>
/// Delivery-interceptor bookkeeping: the saved set of tax events redirected to an outpost, plus
/// transient redirect flags that prevent re-redirect loops within a single tick.
/// Auto-discovered by RimWorld (WorldComponent subclasses are instantiated automatically).
/// </summary>
public class WorldComponent_VOETracker : WorldComponent
{
    // Short-lived redirect flags — not serialized. The redirect handshake normally clears a flag
    // the same tick it is consumed (OutpostDeliveryInterceptor.OnTaxEventCreated), but a foreign
    // ITaxDeliveryInterceptor could consume the re-queued event first and strand the flag, which
    // would make the settlement's next genuine tax event skip its outpost redirect. Storing an
    // expiry tick makes the flag self-clear so it can never leak past its intended one-cycle life.
    private const int RedirectFlagLifetimeTicks = 2500; // ~1 in-game hour; a redirect resolves well within this.
    private static readonly Dictionary<PlanetTile, int> redirectExpiryTick = [];

    // loadIDs of tax events OnTaxEventCreated redirected to an outpost. Delivery trusts only these
    // (never "not bound for the capital"), so a stale or unset capital can't hijack ordinary taxes.
    private HashSet<int> outpostBoundEventIds = [];

    public WorldComponent_VOETracker(World world) : base(world)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            // Drop ids of events that left the queue without reaching the interceptor.
            HashSet<int> live = [];
            if (FindFC.Events is { } events)
                foreach (FCEvent evt in events)
                    live.Add(evt.loadID);
            outpostBoundEventIds.IntersectWith(live);
        }
        Scribe_Collections.Look(ref outpostBoundEventIds, "outpostBoundEventIds", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            outpostBoundEventIds ??= [];
    }

    private static WorldComponent_VOETracker Instance => Find.World?.GetComponent<WorldComponent_VOETracker>();

    public static void MarkOutpostBound(FCEvent evt)
    {
        if (evt is not null) Instance?.outpostBoundEventIds.Add(evt.loadID);
    }

    /// <summary>True (and forgets the event) if <see cref="MarkOutpostBound"/> stamped it.</summary>
    public static bool ConsumeOutpostBound(FCEvent evt)
    {
        return evt is not null && (Instance?.outpostBoundEventIds.Remove(evt.loadID) ?? false);
    }

    public override void FinalizeInit(bool fromload)
    {
        base.FinalizeInit(fromload);
        redirectExpiryTick.Clear();
    }

    public static bool IsRedirected(PlanetTile settlementTile)
    {
        if (!redirectExpiryTick.TryGetValue(settlementTile, out int expiry))
            return false;
        if (Find.TickManager.TicksGame > expiry)
        {
            redirectExpiryTick.Remove(settlementTile);
            return false;
        }
        return true;
    }

    public static void SetRedirected(PlanetTile settlementTile)
    {
        redirectExpiryTick[settlementTile] = Find.TickManager.TicksGame + RedirectFlagLifetimeTicks;
    }

    public static void ClearRedirected(PlanetTile settlementTile)
    {
        redirectExpiryTick.Remove(settlementTile);
    }
}
