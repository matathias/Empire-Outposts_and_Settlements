using System.Collections.Generic;
using FactionColonies;
using Outposts;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Stateless helpers plus the cross-settlement "claimed outpost" registry behind resource linking.
    /// </summary>
    public static class ResourceLinkUtil
    {
        /*-*-*- Pure helpers (outpost + its extension only) -*-*-*/

        /// <summary>Whether an outpost def can be resource-linked at all.</summary>
        public static bool IsLinkable(Outpost outpost)
        {
            return outpost?.def.GetModExtension<OutpostResourceLinkExtension>()?.resources?.Count > 0;
        }

        /// <summary>Total additive contribution of an outpost: sum over CapablePawns of skill*rate.</summary>
        public static double SkillSum(Outpost outpost, OutpostResourceLinkExtension ext)
        {
            if (outpost is null || ext is null) return 0;
            double sum = 0;
            foreach (Pawn pawn in outpost.CapablePawns)
            {
                if (pawn.skills is null) continue;
                int level = ext.skill is null ? 0 : (pawn.skills.GetSkill(ext.skill)?.Level ?? 0);
                if (ext.secondarySkill is object)
                {
                    int secondary = pawn.skills.GetSkill(ext.secondarySkill)?.Level ?? 0;
                    if (secondary > level) level = secondary;
                }
                sum += VOEFormulas.SkillContribution(level, EmpireVOESettings.skillFloor, EmpireVOESettings.additivePerLevel);
            }
            return sum;
        }

        /// <summary>Convenience: the contribution an outpost would make, resolving its extension.</summary>
        public static double ContributionOf(Outpost outpost)
        {
            OutpostResourceLinkExtension ext = outpost?.def.GetModExtension<OutpostResourceLinkExtension>();
            return ext is null ? 0 : SkillSum(outpost, ext);
        }

        /*-*-*- Cross-settlement claimed-outpost registry (one outpost -> one settlement) -*-*-*/

        // Set of all outposts linked by some settlement. Used by the delivery-suppression patches for O(1)
        // lookup and by the management tab to grey out already-claimed outposts. Rebuilt lazily when dirty.
        private static readonly HashSet<Outpost> claimedOutposts = new HashSet<Outpost>();
        private static bool dirty = true;

        internal static void MarkDirty()
        {
            dirty = true;
        }

        /// <summary>Whether the outpost is linked to any settlement.</summary>
        public static bool IsLinked(Outpost outpost)
        {
            EnsureFresh();
            return claimedOutposts.Contains(outpost);
        }

        /// <summary>Whether the outpost is linked to a settlement other than <paramref name="excluding"/>.</summary>
        public static bool IsLinkedToOther(Outpost outpost, WorldObjectComp_ResourceLink excluding)
        {
            EnsureFresh();
            return claimedOutposts.Contains(outpost)
                   && (excluding?.linkedOutposts is null || !excluding.linkedOutposts.Contains(outpost));
        }

        private static void EnsureFresh()
        {
            if (!dirty) return;
            Rebuild();
            dirty = false;
        }

        private static void Rebuild()
        {
            claimedOutposts.Clear();
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_ResourceLink comp = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (comp?.linkedOutposts is null) continue;
                foreach (Outpost outpost in comp.linkedOutposts)
                {
                    if (outpost is object && !outpost.Destroyed)
                        claimedOutposts.Add(outpost);
                }
            }
        }

        /// <summary>
        /// When a linked outpost's pawn roster changes, refresh the owning settlement's resource caches so
        /// the skill-scaled contribution recomputes.
        /// </summary>
        public static void NotifyOutpostRosterChanged(Outpost outpost)
        {
            if (outpost is null || !IsLinked(outpost)) return;
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_ResourceLink comp = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (comp?.linkedOutposts is object && comp.linkedOutposts.Contains(outpost))
                    s.InvalidateResourceCaches();
            }
        }
    }
}
