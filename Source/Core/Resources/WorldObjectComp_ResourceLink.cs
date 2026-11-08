using Outposts;

namespace EmpireVOE;

public class WorldObjectCompProperties_ResourceLink : WorldObjectCompProperties
{
    public WorldObjectCompProperties_ResourceLink()
    {
        compClass = typeof(WorldObjectComp_ResourceLink);
    }
}

/// <summary>
/// Provides an additive production bonus to a settlement based on the pawn skills of linked production
/// outposts. Each linkable outpost def carries an <see cref="OutpostResourceLinkExtension"/> declaring
/// which resource(s) it feeds and which skill(s) drive the bonus (Mining->Mining, Farming->Plants,
/// Hunting->Shooting/Animals, Logging->Plants, Drilling->Construction, Science->Intellectual).
/// <para>A settlement may link several outposts; an outpost may be linked by at most one settlement (its
/// own physical/research delivery is suppressed while linked, so counting it twice would double-dip).
/// Management lives in the settlement window's Outposts tab — this comp holds the per-settlement link
/// state and the mutators that tab drives. The faction-wide claimed-outpost registry and the pure
/// skill/mapping helpers live in <see cref="ResourceLinkUtil"/>.</para>
/// </summary>
public class WorldObjectComp_ResourceLink : WorldObjectComp, IResourceProductionModifier
{
    public List<Outpost> linkedOutposts = [];

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref linkedOutposts, "voeLinkedResourceOutposts", LookMode.Reference);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            linkedOutposts ??= [];
            // Rebuild the outpost -> settlement reverse index from this authoritative list (cross-refs are
            // resolved by now, so the outposts and their comps exist). Runtime-only; not serialized.
            foreach (Outpost outpost in linkedOutposts)
            {
                if (outpost is null || outpost.Destroyed) continue;
                WorldObjectComp_EmpireOutpost oc = outpost.GetComponent<WorldObjectComp_EmpireOutpost>();
                if (oc is not null) oc.linkedSettlement = parent as WorldSettlementFC;
            }
        }
    }

    // --- IResourceProductionModifier ---

    public double GetResourceAdditiveModifier(ResourceFC resource)
    {
        if (!EmpireVOESettings.ResourceLinkActive) return 0;
        if (resource?.def is null) return 0;
        double bonus = 0;
        foreach (Outpost outpost in linkedOutposts)
        {
            if (outpost is null || outpost.Destroyed) continue;
            OutpostResourceLinkExtension ext = outpost.def.GetModExtension<OutpostResourceLinkExtension>();
            if (ext?.resources is null || !ext.resources.Contains(resource.def)) continue;
            bonus += ext.Contribution(outpost, parent as WorldSettlementFC);
        }
        return bonus;
    }

    public double GetResourceMultiplierModifier(ResourceFC resource)
    {
        return 1.0;
    }

    public string GetResourceAdditiveDesc(ResourceFC resource)
    {
        double bonus = GetResourceAdditiveModifier(resource);
        if (bonus > 0)
            return TextUtil.AdditiveBonusLine(bonus, "FCVOE_ResourceLinkBonusDesc".Translate(resource.def.LabelCap));
        return null;
    }

    // --- Per-settlement link state / mutators (driven by the Outposts tab) ---

    internal bool IsLinkedHere(Outpost outpost) => linkedOutposts.Contains(outpost);

    // The management tab asks for the candidates every frame, and a range check can path-find,
    // so the list is rebuilt at most this often (and whenever a link here changes).
    private const int CandidateRefreshTicks = 250;
    [Unsaved] private List<Outpost> cachedCandidates;
    [Unsaved] private int cachedCandidatesTick = -1;
    [Unsaved] private int cachedCandidatesLinkVersion = -1;

    /// <summary>Linkable outposts within range, sorted by distance. Used by the management tab.</summary>
    internal List<Outpost> GetLinkableOutpostsInRange()
    {
        int now = Find.TickManager.TicksGame;
        if (cachedCandidates is not null && cachedCandidatesTick >= 0 && now - cachedCandidatesTick < CandidateRefreshTicks
            && cachedCandidatesLinkVersion == ResourceLinkUtil.LinkVersion && !cachedCandidates.Any(o => o.Destroyed))
            return cachedCandidates;

        cachedCandidatesTick = now;
        cachedCandidatesLinkVersion = ResourceLinkUtil.LinkVersion;
        cachedCandidates = Find.WorldObjects.AllWorldObjects
            .OfType<Outpost>()
            .Where(o => ResourceLinkUtil.IsLinkable(o)
                        && ResourceLinkUtil.InLinkRange(o, parent.Tile))
            .OrderBy(o => o.Tile.ApproxDistanceSameLayer(parent.Tile))
            .ToList();
        return cachedCandidates;
    }

    internal void ToggleLink(Outpost outpost)
    {
        WorldObjectComp_EmpireOutpost oc = outpost.GetComponent<WorldObjectComp_EmpireOutpost>();
        if (linkedOutposts.Contains(outpost))
        {
            linkedOutposts.Remove(outpost);
            if (oc is not null && oc.linkedSettlement == parent) oc.linkedSettlement = null;
        }
        else
        {
            // An outpost may be linked by at most one settlement. If it's already linked elsewhere,
            // cleanly remove that link first (drops its entry and invalidates the old settlement's
            // caches) rather than silently overwriting the back-pointer and leaking a stale link.
            if (oc is not null && oc.linkedSettlement is not null && oc.linkedSettlement != parent)
                oc.linkedSettlement.GetComponent<WorldObjectComp_ResourceLink>()?.ToggleLink(outpost);

            linkedOutposts.Add(outpost);
            if (oc is not null) oc.linkedSettlement = parent as WorldSettlementFC;
        }
        (parent as WorldSettlementFC)?.InvalidateResourceCaches();
        ResourceLinkUtil.NotifyLinkChanged(outpost);
    }
}
