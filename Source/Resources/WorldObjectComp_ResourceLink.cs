using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
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
        public List<Outpost> linkedOutposts = new List<Outpost>();

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref linkedOutposts, "voeLinkedResourceOutposts", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (linkedOutposts is null) linkedOutposts = new List<Outpost>();
                ResourceLinkUtil.MarkDirty();
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

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            return null;
        }

        // --- Per-settlement link state / mutators (driven by the Outposts tab) ---

        internal bool IsLinkedHere(Outpost outpost) => linkedOutposts.Contains(outpost);

        /// <summary>Linkable outposts within range, sorted by distance. Used by the management tab.</summary>
        internal List<Outpost> GetLinkableOutpostsInRange()
        {
            return Find.WorldObjects.AllWorldObjects
                .OfType<Outpost>()
                .Where(o => ResourceLinkUtil.IsLinkable(o)
                            && ResourceLinkUtil.InLinkRange(o, parent.Tile))
                .OrderBy(o => Find.WorldGrid.ApproxDistanceInTiles(o.Tile, parent.Tile))
                .ToList();
        }

        internal void ToggleLink(Outpost outpost)
        {
            if (linkedOutposts.Contains(outpost))
                linkedOutposts.Remove(outpost);
            else
                linkedOutposts.Add(outpost);
            ResourceLinkUtil.MarkDirty();
            (parent as WorldSettlementFC)?.InvalidateResourceCaches();
            ResourceLinkUtil.NotifyLinkChanged(outpost);
        }

        internal void UnlinkAll()
        {
            if (linkedOutposts.Count == 0) return;
            List<Outpost> cleared = new List<Outpost>(linkedOutposts);
            linkedOutposts.Clear();
            ResourceLinkUtil.MarkDirty();
            (parent as WorldSettlementFC)?.InvalidateResourceCaches();
            foreach (Outpost outpost in cleared)
                ResourceLinkUtil.NotifyLinkChanged(outpost);
        }
    }
}
