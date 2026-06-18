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
    /// Provides an additive Research production bonus to the settlement based on the
    /// linked Outpost_Science(s) pawn count and Intellectual skill. A settlement may
    /// link several science outposts; an outpost may be linked by at most one
    /// settlement (its own research contribution is redirected while linked, so
    /// counting it from two settlements would double-dip). Also provides the gizmo
    /// for linking/unlinking science outposts.
    /// </summary>
    public class WorldObjectComp_ScienceLink : WorldObjectComp, IResourceProductionModifier
    {
        public List<Outpost_Science> linkedOutposts = new List<Outpost_Science>();

        // Static set of all outposts that have at least one settlement linked to them.
        // Used by Patch_ScienceTick for O(1) lookup. Rebuilt lazily when dirty.
        private static readonly HashSet<Outpost_Science> globallyLinkedOutposts = new HashSet<Outpost_Science>();
        private static bool linkedSetDirty = true;

        public static bool IsAnySettlementLinked(Outpost_Science outpost)
        {
            EnsureLinkedSetFresh();
            return globallyLinkedOutposts.Contains(outpost);
        }

        private static void MarkLinkedSetDirty()
        {
            linkedSetDirty = true;
        }

        private static void EnsureLinkedSetFresh()
        {
            if (!linkedSetDirty) return;
            RebuildLinkedSet();
            linkedSetDirty = false;
        }

        private static void RebuildLinkedSet()
        {
            globallyLinkedOutposts.Clear();
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_ScienceLink comp = s.GetComponent<WorldObjectComp_ScienceLink>();
                if (comp?.linkedOutposts is null) continue;
                foreach (Outpost_Science outpost in comp.linkedOutposts)
                {
                    if (outpost is object && !outpost.Destroyed)
                        globallyLinkedOutposts.Add(outpost);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref linkedOutposts, "voeLinkedScienceOutposts", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (linkedOutposts is null) linkedOutposts = new List<Outpost_Science>();
                // Rebuild the static set after all comps have loaded
                MarkLinkedSetDirty();
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!EmpireVOESettings.ScienceLinkActive) yield break;

            // Only show gizmo if there are science outposts on the map
            bool hasScienceOutposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost_Science>().Any();
            if (!hasScienceOutposts) yield break;

            yield return CreateScienceLinkGizmo();
        }

        // --- IResourceProductionModifier ---

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            if (!EmpireVOESettings.ScienceLinkActive) return 0;
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
                return TextUtil.AdditiveBonusLine(bonus, "VOE_ScienceLinkBonusDesc".Translate());
            return null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            return null;
        }

        // --- Private helpers ---

        private double CalculateBonus()
        {
            double bonus = 0;
            foreach (Outpost_Science outpost in linkedOutposts)
            {
                if (outpost is null || outpost.Destroyed) continue;
                foreach (Pawn pawn in outpost.CapablePawns)
                {
                    if (pawn.skills is null) continue;
                    SkillRecord skill = pawn.skills.GetSkill(SkillDefOf.Intellectual);
                    if (skill is null) continue;
                    int level = skill.Level;
                    if (level >= EmpireVOESettings.skillFloor)
                        bonus += level * EmpireVOESettings.additivePerLevel;
                }
            }

            return bonus;
        }

        private void ToggleLink(Outpost_Science outpost)
        {
            WorldSettlementFC settlement = parent as WorldSettlementFC;
            if (linkedOutposts.Contains(outpost))
                linkedOutposts.Remove(outpost);
            else
                linkedOutposts.Add(outpost);
            MarkLinkedSetDirty();
            settlement?.InvalidateResourceCaches();
        }

        private Command_Action CreateScienceLinkGizmo()
        {
            return new Command_Action
            {
                defaultLabel = "VOE_ScienceLinkLabel".Translate(),
                defaultDesc = "VOE_ScienceLinkDesc".Translate(linkedOutposts.Count),
                icon = TexCommand.Install,
                action = delegate
                {
                    EnsureLinkedSetFresh();
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    // Option: unlink all
                    if (linkedOutposts.Count > 0)
                    {
                        options.Add(new FloatMenuOption(
                            "VOE_UnlinkAll".Translate(),
                            delegate
                            {
                                if (linkedOutposts.Count == 0) return;
                                linkedOutposts.Clear();
                                MarkLinkedSetDirty();
                                (parent as WorldSettlementFC)?.InvalidateResourceCaches();
                            }));
                    }

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
                        bool linkedHere = linkedOutposts.Contains(op);
                        bool linkedElsewhere = !linkedHere && globallyLinkedOutposts.Contains(op);
                        string baseLabel = (op.Name ?? op.def.label) + " (" + distance.ToString("F1") + " " + "VOE_Tiles".Translate() + ")";

                        if (linkedElsewhere)
                        {
                            // Claimed by another settlement - show disabled.
                            options.Add(new FloatMenuOption(
                                baseLabel + " - " + "VOE_AlreadyLinked".Translate(), null)
                            {
                                Disabled = true
                            });
                            continue;
                        }

                        string label = linkedHere ? baseLabel + " - " + (string)"VOE_Linked".Translate() : baseLabel;
                        options.Add(new FloatMenuOption(label, delegate { ToggleLink(op); }));
                    }

                    if (options.Count == 0)
                        options.Add(new FloatMenuOption("VOE_NoOutpostsInRange".Translate(), null) { Disabled = true });

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }
}
