using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_ScienceLink : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_ScienceLink()
        {
            compClass = typeof(WorldObjectComp_ScienceLink);
        }
    }

    /// <summary>
    /// Provides an additive Research production bonus to the settlement based on a
    /// linked Outpost_Science's pawn count and Intellectual skill. Also provides
    /// the gizmo for linking/unlinking science outposts.
    /// </summary>
    public class WorldObjectComp_ScienceLink : WorldObjectComp, IResourceProductionModifier
    {
        public Outpost_Science linkedOutpost;

        // Static set of all outposts that have at least one settlement linked to them.
        // Used by Patch_ScienceTick for O(1) lookup. Rebuilt lazily when dirty.
        private static readonly HashSet<Outpost_Science> linkedOutposts = new HashSet<Outpost_Science>();
        private static bool linkedSetDirty = true;

        public static bool IsAnySettlementLinked(Outpost_Science outpost)
        {
            if (linkedSetDirty)
            {
                RebuildLinkedSet();
                linkedSetDirty = false;
            }
            return linkedOutposts.Contains(outpost);
        }

        private static void MarkLinkedSetDirty()
        {
            linkedSetDirty = true;
        }

        private static void RebuildLinkedSet()
        {
            linkedOutposts.Clear();
            FactionFC faction = FactionCache.FactionComp;
            if (faction is null) return;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_ScienceLink comp = s.GetComponent<WorldObjectComp_ScienceLink>();
                if (comp?.linkedOutpost is object && !comp.linkedOutpost.Destroyed)
                    linkedOutposts.Add(comp.linkedOutpost);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref linkedOutpost, "voeLinkedScienceOutpost");

            // Rebuild the static set after all comps have loaded
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                MarkLinkedSetDirty();
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (EmpireVOESettings.disableIntegration) yield break;

            // Only show gizmo if there are science outposts on the map
            bool hasScienceOutposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost_Science>().Any();
            if (!hasScienceOutposts) yield break;

            yield return CreateScienceLinkGizmo();
        }

        // --- IResourceProductionModifier ---

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            if (EmpireVOESettings.disableIntegration) return 0;
            if (resource?.def is null || resource.def != ResourceTypeDefOf.RTD_Research) return 0;
            return CalculateBonus();
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            return 1.0;
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            double bonus = GetResourceAdditiveModifier(resource);
            if (bonus > 0)
                return "VOE_ScienceLinkBonusDesc".Translate() + ": +" + bonus.ToString("F2");
            return null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            return null;
        }

        // --- Private helpers ---

        private double CalculateBonus()
        {
            if (linkedOutpost is null || linkedOutpost.Destroyed)
                return 0;

            double bonus = 0;
            foreach (Pawn pawn in linkedOutpost.CapablePawns)
            {
                if (pawn.skills is null) continue;
                SkillRecord skill = pawn.skills.GetSkill(SkillDefOf.Intellectual);
                if (skill is null) continue;
                int level = skill.Level;
                if (level >= EmpireVOESettings.skillFloor)
                    bonus += level * EmpireVOESettings.additivePerLevel;
            }

            return bonus;
        }

        private Command_Action CreateScienceLinkGizmo()
        {
            string currentLabel = linkedOutpost is object && !linkedOutpost.Destroyed
                ? linkedOutpost.Name ?? linkedOutpost.def.label
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
                            if (linkedOutpost is object)
                            {
                                linkedOutpost = null;
                                MarkLinkedSetDirty();
                                WorldSettlementFC settlement = parent as WorldSettlementFC;
                                settlement?.InvalidateResourceCaches();
                            }
                        }));

                    // List science outposts within range, sorted by distance
                    List<Outpost_Science> sorted = Find.WorldObjects.AllWorldObjects
                        .OfType<Outpost_Science>()
                        .Where(o => Find.WorldGrid.ApproxDistanceInTiles(o.Tile, parent.Tile)
                                    <= EmpireVOESettings.scienceLinkRange)
                        .OrderBy(o => Find.WorldGrid.ApproxDistanceInTiles(o.Tile, parent.Tile))
                        .ToList();

                    foreach (Outpost_Science outpost in sorted)
                    {
                        Outpost_Science op = outpost;
                        float distance = Find.WorldGrid.ApproxDistanceInTiles(op.Tile, parent.Tile);
                        string label = (op.Name ?? op.def.label) + " (" + distance.ToString("F1") + " " + "VOE_Tiles".Translate() + ")";

                        options.Add(new FloatMenuOption(
                            label,
                            delegate
                            {
                                // Invalidate previous if changing
                                WorldSettlementFC settlement = parent as WorldSettlementFC;
                                if (linkedOutpost != op)
                                {
                                    linkedOutpost = op;
                                    MarkLinkedSetDirty();
                                    settlement?.InvalidateResourceCaches();
                                }
                            }));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }
}
