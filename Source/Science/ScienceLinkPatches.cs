using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using HarmonyLib;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Adds the "Link to Settlement" gizmo on science outposts.
    /// Patches Outpost.GetGizmos since Outpost_Science does not override it.
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("GetGizmos")]
    public static class Patch_ScienceGizmos
    {
        private static void Postfix(Outpost __instance, ref IEnumerable<Gizmo> __result)
        {
            if (EmpireVOESettings.disableIntegration) return;
            Outpost_Science science = __instance as Outpost_Science;
            if (science is null) return;
            __result = AddScienceGizmo(__result, science);
        }

        private static IEnumerable<Gizmo> AddScienceGizmo(IEnumerable<Gizmo> original, Outpost_Science science)
        {
            foreach (Gizmo g in original)
                yield return g;
            yield return CreateScienceLinkGizmo(science);
        }

        private static Command_Action CreateScienceLinkGizmo(Outpost_Science science)
        {
            WorldSettlementFC current = WorldComponent_VOETracker.GetLinkedSettlement(science);
            string currentLabel = current != null
                ? current.Name
                : "None".Translate().ToString();

            return new Command_Action
            {
                defaultLabel = "VOE_ScienceLinkLabel".Translate(),
                defaultDesc = "VOE_ScienceLinkDesc".Translate(currentLabel),
                icon = TexCommand.Install,
                action = delegate
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    // Option: unlink
                    options.Add(new FloatMenuOption(
                        "None".Translate(),
                        delegate
                        {
                            WorldSettlementFC prev = WorldComponent_VOETracker.GetLinkedSettlement(science);
                            if (prev != null)
                            {
                                WorldComponent_VOETracker.SetScienceLink(prev, null);
                                prev.InvalidateResourceCaches();
                            }
                        }));

                    // List Empire settlements within range, sorted by distance
                    FactionFC faction = FactionCache.FactionComp;
                    if (faction != null)
                    {
                        List<WorldSettlementFC> sorted = faction.settlements
                            .Where(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, science.Tile)
                                        <= EmpireVOESettings.scienceLinkRange)
                            .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, science.Tile))
                            .ToList();

                        foreach (WorldSettlementFC settlement in sorted)
                        {
                            WorldSettlementFC s = settlement; // closure capture
                            float distance = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, science.Tile);

                            // Check if another outpost is already linked to this settlement
                            Outpost_Science existingLink = WorldComponent_VOETracker.GetLinkedScienceOutpost(s);
                            bool alreadyLinkedByOther = existingLink != null && existingLink != science;

                            string label = s.Name + " (" + distance.ToString("F1") + " " + "VOE_Tiles".Translate() + ")";
                            if (alreadyLinkedByOther)
                            {
                                label += " - " + "VOE_AlreadyLinked".Translate();
                            }

                            options.Add(new FloatMenuOption(
                                label,
                                alreadyLinkedByOther
                                    ? (Action)null
                                    : delegate
                                    {
                                        // Invalidate previous settlement if re-linking
                                        WorldSettlementFC prev = WorldComponent_VOETracker.GetLinkedSettlement(science);
                                        if (prev != null)
                                            prev.InvalidateResourceCaches();

                                        WorldComponent_VOETracker.SetScienceLink(s, science);
                                        s.InvalidateResourceCaches();
                                    }));
                        }
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }

    /// <summary>
    /// Static flag for research suppression. Set by Outpost_Science.Tick prefix,
    /// checked by ResearchManager.ResearchPerformed prefix, cleared by Tick postfix.
    /// Safe because RimWorld is single-threaded.
    /// </summary>
    internal static class ScienceResearchSuppression
    {
        internal static bool suppressResearch;
    }

    /// <summary>
    /// Sets/clears the suppression flag around Outpost_Science.Tick() so that
    /// ResearchPerformed calls within it are blocked for linked outposts.
    /// </summary>
    [HarmonyPatch(typeof(Outpost_Science))]
    [HarmonyPatch("Tick")]
    public static class Patch_ScienceTick
    {
        private static void Prefix(Outpost_Science __instance)
        {
            if (EmpireVOESettings.disableIntegration) return;
            if (WorldComponent_VOETracker.IsLinkedToSettlement(__instance))
            {
                ScienceResearchSuppression.suppressResearch = true;
            }
        }

        private static void Postfix()
        {
            ScienceResearchSuppression.suppressResearch = false;
        }
    }

    /// <summary>
    /// Skips ResearchPerformed when the suppression flag is set, preventing
    /// linked science outposts from contributing to the vanilla research pool.
    /// </summary>
    [HarmonyPatch(typeof(ResearchManager))]
    [HarmonyPatch("ResearchPerformed")]
    public static class Patch_ResearchPerformed
    {
        private static bool Prefix()
        {
            return !ScienceResearchSuppression.suppressResearch;
        }
    }
}
