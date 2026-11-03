using System;
using FactionColonies;
using Outposts;
using RimWorld.Planet;
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

        /// <summary>
        /// The contribution an outpost makes to the given settlement, resolving its extension. Settlement-aware
        /// because some links (e.g. power) scale their contribution by distance to the settlement.
        /// </summary>
        public static double ContributionOf(Outpost outpost, WorldSettlementFC settlement)
        {
            OutpostResourceLinkExtension ext = outpost?.def.GetModExtension<OutpostResourceLinkExtension>();
            return ext is null ? 0 : ext.Contribution(outpost, settlement);
        }

        /// <summary>Whether the settlement at <paramref name="settlementTile"/> is within the outpost's link range.</summary>
        public static bool InLinkRange(Outpost outpost, PlanetTile settlementTile)
        {
            OutpostResourceLinkExtension ext = outpost?.def.GetModExtension<OutpostResourceLinkExtension>();
            return ext is object && ext.InLinkRange(outpost, settlementTile);
        }

        /// <summary>
        /// Fired when an outpost's link state changes (linked or unlinked). Compat layers subscribe to react
        /// immediately — e.g. the VOEPowerGrid compat refreshes a connected outlet's suppressed output.
        /// </summary>
        public static event Action<Outpost> LinkChanged;

        internal static void NotifyLinkChanged(Outpost outpost) => LinkChanged?.Invoke(outpost);

        /*-*-*- Outpost -> settlement reverse index (one outpost -> one settlement) -*-*-*/

        // The settlement-side WorldObjectComp_ResourceLink.linkedOutposts list is authoritative; the reverse
        // pointer lives (runtime-only) on each outpost's WorldObjectComp_EmpireOutpost.linkedSettlement,
        // maintained on link/unlink and rebuilt on load. This makes "is it linked?" and "which settlement?"
        // both O(1) with no scan over all settlements.

        /// <summary>The settlement that resource-links this outpost, or null. O(1) via the reverse index;
        /// a destroyed settlement reads as unlinked.</summary>
        public static WorldSettlementFC LinkedSettlementOf(Outpost outpost)
        {
            WorldSettlementFC s = outpost?.GetComponent<WorldObjectComp_EmpireOutpost>()?.linkedSettlement;
            return (s is object && !s.Destroyed) ? s : null;
        }

        /// <summary>Whether the outpost is linked to any settlement.</summary>
        public static bool IsLinked(Outpost outpost) => LinkedSettlementOf(outpost) is object;

        /// <summary>Whether the outpost is linked to a settlement other than <paramref name="excluding"/>.</summary>
        public static bool IsLinkedToOther(Outpost outpost, WorldObjectComp_ResourceLink excluding)
        {
            WorldSettlementFC s = LinkedSettlementOf(outpost);
            return s is object && s.GetComponent<WorldObjectComp_ResourceLink>() != excluding;
        }

        /// <summary>
        /// Invalidate the resource caches of the settlement that links this outpost, so its contribution is
        /// recomputed from the outpost's current state (pawn skills, power output, etc.). Cheap no-op when the
        /// outpost isn't linked.
        /// </summary>
        public static void InvalidateLinkedSettlements(Outpost outpost)
        {
            LinkedSettlementOf(outpost)?.InvalidateResourceCaches();
        }

        /// <summary>
        /// When a linked outpost's pawn roster changes, refresh the owning settlement's resource caches so
        /// the skill-scaled contribution recomputes.
        /// </summary>
        public static void NotifyOutpostRosterChanged(Outpost outpost) => InvalidateLinkedSettlements(outpost);
    }
}
