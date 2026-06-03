using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
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
            if (deliveryOutpost is object && deliveryOutpost.Destroyed)
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
            if (financingOutpost is object && financingOutpost.Destroyed)
            {
                financingOutpost = null;
            }
            return financingOutpost;
        }

        /// <summary>
        /// Collects all distinct financing outposts across all Empire settlements.
        /// </summary>
        public static IEnumerable<Outpost> GetAllDistinctFinancingOutposts()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) yield break;

            HashSet<Outpost> seen = new HashSet<Outpost>();
            foreach (WorldSettlementFC settlement in faction.settlements)
            {
                WorldObjectComp_OutpostLinks comp = settlement.GetComponent<WorldObjectComp_OutpostLinks>();
                if (comp is null) continue;
                Outpost outpost = comp.GetFinancingOutpost();
                if (outpost is object && seen.Add(outpost))
                    yield return outpost;
            }
        }

        private Command_Action CreateDeliveryGizmo()
        {
            Outpost current = GetDeliveryOutpost();
            string currentLabel = current is object
                ? current.LabelCap
                : "VOE_PlayerTaxMap".Translate().ToString();

            return new Command_Action
            {
                defaultLabel = "VOE_DeliveryOutpostLabel".Translate(),
                defaultDesc = "VOE_DeliveryOutpostDesc".Translate(currentLabel),
                icon = current is object
                    ? current.ExpandingIcon
                    : TexCommand.Install,
                action = delegate
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    // Option: deliver to player tax map (default)
                    options.Add(new FloatMenuOption(
                        "VOE_PlayerTaxMap".Translate(),
                        delegate { deliveryOutpost = null; }));

                    // List all outposts ordered by distance
                    IEnumerable<Outpost> outposts = Find.WorldObjects.AllWorldObjects
                        .OfType<Outpost>()
                        .OrderBy(o => Find.WorldGrid.ApproxDistanceInTiles(o.Tile, parent.Tile));

                    foreach (Outpost outpost in outposts)
                    {
                        Outpost op = outpost;
                        options.Add(new FloatMenuOption(
                            op.LabelCap,
                            delegate { deliveryOutpost = op; },
                            op.ExpandingIcon,
                            op.ExpandingIconColor));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        private Command_Action CreateFinancingGizmo()
        {
            Outpost current = GetFinancingOutpost();
            string currentLabel = current is object
                ? current.LabelCap
                : "None".Translate().ToString();

            return new Command_Action
            {
                defaultLabel = "VOE_FinancingOutpostLabel".Translate(),
                defaultDesc = "VOE_FinancingOutpostDesc".Translate(currentLabel),
                icon = current is object
                    ? current.ExpandingIcon
                    : TexCommand.Install,
                action = delegate
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    // Option: no financing outpost
                    options.Add(new FloatMenuOption(
                        "None".Translate(),
                        delegate { financingOutpost = null; }));

                    // List all outposts ordered by distance
                    IEnumerable<Outpost> outposts = Find.WorldObjects.AllWorldObjects
                        .OfType<Outpost>()
                        .OrderBy(o => Find.WorldGrid.ApproxDistanceInTiles(o.Tile, parent.Tile));

                    foreach (Outpost outpost in outposts)
                    {
                        Outpost op = outpost;
                        options.Add(new FloatMenuOption(
                            op.LabelCap,
                            delegate { financingOutpost = op; },
                            op.ExpandingIcon,
                            op.ExpandingIconColor));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }
}
