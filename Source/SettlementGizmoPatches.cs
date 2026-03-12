using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using HarmonyLib;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Postfix on WorldObject.GetGizmos — adds "Tax Delivery Location" and
    /// "Financing Location" gizmos to Empire settlements when selected on the world map.
    /// </summary>
    [HarmonyPatch(typeof(WorldObject))]
    [HarmonyPatch("GetGizmos")]
    public static class Patch_SettlementGetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, WorldObject __instance)
        {
            foreach (Gizmo gizmo in __result)
                yield return gizmo;

            if (EmpireVOESettings.disableIntegration) yield break;

            WorldSettlementFC settlement = __instance as WorldSettlementFC;
            if (settlement == null) yield break;

            // Only show if there are outposts on the map
            bool hasOutposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>().Any();
            if (!hasOutposts) yield break;

            yield return CreateDeliveryGizmo(settlement);
            yield return CreateFinancingGizmo(settlement);
        }

        private static Command_Action CreateDeliveryGizmo(WorldSettlementFC settlement)
        {
            Outpost current = VOETracker.GetDeliveryDestination(settlement);
            string currentLabel = current != null
                ? current.LabelCap
                : "VOE_PlayerTaxMap".Translate().ToString();

            return new Command_Action
            {
                defaultLabel = "VOE_DeliveryOutpostLabel".Translate(),
                defaultDesc = "VOE_DeliveryOutpostDesc".Translate(currentLabel),
                icon = current != null
                    ? current.ExpandingIcon
                    : TexCommand.Install,
                action = delegate
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    // Option: deliver to player tax map (default)
                    options.Add(new FloatMenuOption(
                        "VOE_PlayerTaxMap".Translate(),
                        delegate { VOETracker.SetDeliveryDestination(settlement, null); }));

                    // List all outposts ordered by distance
                    IEnumerable<Outpost> outposts = Find.WorldObjects.AllWorldObjects
                        .OfType<Outpost>()
                        .OrderBy(o => Find.WorldGrid.ApproxDistanceInTiles(o.Tile, settlement.Tile));

                    foreach (Outpost outpost in outposts)
                    {
                        Outpost op = outpost; // closure capture
                        options.Add(new FloatMenuOption(
                            op.LabelCap,
                            delegate { VOETracker.SetDeliveryDestination(settlement, op); },
                            op.ExpandingIcon,
                            op.ExpandingIconColor));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        private static Command_Action CreateFinancingGizmo(WorldSettlementFC settlement)
        {
            Outpost current = VOETracker.GetFinancingOutpost(settlement);
            string currentLabel = current != null
                ? current.LabelCap
                : "None".Translate().ToString();

            return new Command_Action
            {
                defaultLabel = "VOE_FinancingOutpostLabel".Translate(),
                defaultDesc = "VOE_FinancingOutpostDesc".Translate(currentLabel),
                icon = current != null
                    ? current.ExpandingIcon
                    : TexCommand.Install,
                action = delegate
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    // Option: no financing outpost (use player coffers)
                    options.Add(new FloatMenuOption(
                        "None".Translate(),
                        delegate { VOETracker.SetFinancingOutpost(settlement, null); }));

                    // List all outposts ordered by distance
                    IEnumerable<Outpost> outposts = Find.WorldObjects.AllWorldObjects
                        .OfType<Outpost>()
                        .OrderBy(o => Find.WorldGrid.ApproxDistanceInTiles(o.Tile, settlement.Tile));

                    foreach (Outpost outpost in outposts)
                    {
                        Outpost op = outpost;
                        options.Add(new FloatMenuOption(
                            op.LabelCap,
                            delegate { VOETracker.SetFinancingOutpost(settlement, op); },
                            op.ExpandingIcon,
                            op.ExpandingIconColor));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }
}
