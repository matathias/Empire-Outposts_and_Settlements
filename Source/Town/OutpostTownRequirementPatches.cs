using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /* Absorbs the standalone "VOE Towns Count Outposts" mod (matathias.voetownscountoutposts) */

    /// <summary>
    /// Makes VOE's Town spawn requirement configurable (minimum nearby settlements /
    /// outposts / total within a range, optionally excluding other Towns) AND fixes VOE's
    /// planet-layer bug: Outpost_Town.CanSpawnOnWith / RequirementsString measured nearby
    /// settlements and outposts with Find.WorldGrid.ApproxDistanceInTiles without restricting
    /// the search to the target tile's planet layer. With Odyssey, objects on other layers
    /// (orbit) have tile ids out of range for the surface layer's tile array, producing
    /// "Attempted to access a tile with ID X, but it is out of range" errors. Every nearby
    /// count below is filtered to the target tile's layer.
    /// </summary>
    [HarmonyPatch(typeof(Outpost_Town), nameof(Outpost_Town.CanSpawnOnWith))]
    public static class Patch_OutpostTown_CanSpawnOnWith
    {
        static bool Prefix(PlanetTile tile, ref string __result)
        {
            int settlements = OutpostTownRequirements.NearbySettlements(tile);
            int outposts = OutpostTownRequirements.NearbyOutposts(tile);

            if (settlements < EmpireVOESettings.townMinSettlements)
                __result = "FCVOE_TownNearbySettlements".Translate(EmpireVOESettings.townMinSettlements, EmpireVOESettings.townRange);
            else if (outposts < EmpireVOESettings.townMinOutposts)
                __result = "FCVOE_TownNearbyOutposts".Translate(EmpireVOESettings.townMinOutposts, EmpireVOESettings.townRange);
            else if (settlements + outposts < EmpireVOESettings.townMinTotal)
                __result = "FCVOE_TownNearbyTotal".Translate(EmpireVOESettings.townMinTotal, EmpireVOESettings.townRange);
            else
                __result = null;

            return false;
        }
    }

    [HarmonyPatch(typeof(Outpost_Town), nameof(Outpost_Town.RequirementsString))]
    public static class Patch_OutpostTown_RequirementsString
    {
        static bool Prefix(PlanetTile tile, ref string __result)
        {
            int settlements = OutpostTownRequirements.NearbySettlements(tile);
            int outposts = OutpostTownRequirements.NearbyOutposts(tile);
            bool passed = settlements >= EmpireVOESettings.townMinSettlements
                       && outposts >= EmpireVOESettings.townMinOutposts
                       && settlements + outposts >= EmpireVOESettings.townMinTotal;

            __result = "FCVOE_TownRequirements".Translate(
                EmpireVOESettings.townMinTotal, EmpireVOESettings.townRange,
                EmpireVOESettings.townMinSettlements, EmpireVOESettings.townMinOutposts).Requirement(passed);

            return false;
        }
    }

    /// <summary>
    /// Per-town "xenotype-pure recruiting" mode. When a town's WorldObjectComp_TownRecruiting
    /// toggle is on, this prefix replaces VOE's Outpost_Town.Produce: each recruit is forced to
    /// match the recruiting resident's xenotype (humans) or race (non-humans, implied by reusing
    /// the recruiter's kindDef), and the per-roll recruit chance is scaled by the configurable
    /// global multiplier EmpireVOESettings.townXenotypePureChanceMult. When the toggle is off (or
    /// the comp is absent) the original method runs untouched, so there is no behavior change and
    /// full compatibility with other Produce patches.
    /// </summary>
    [HarmonyPatch(typeof(Outpost_Town), nameof(Outpost_Town.Produce))]
    public static class Patch_OutpostTown_Produce
    {
        static bool Prefix(Outpost_Town __instance)
        {
            WorldObjectComp_TownRecruiting comp = __instance.GetComponent<WorldObjectComp_TownRecruiting>();
            if (comp is null || !comp.xenotypePure) return true;

            float mult = EmpireVOESettings.townXenotypePureChanceMult;
            List<Pawn> newPawns = new List<Pawn>();
            foreach (Pawn pawn in __instance.CapablePawns)
                if (Rand.Chance(pawn.skills.GetSkill(SkillDefOf.Social).Level * __instance.Chance / 100f * mult))
                {
                    Pawn newPawn = GenerateMatching(pawn);
                    newPawn.SetFaction(pawn.Faction, pawn);
                    newPawns.Add(newPawn);
                }

            if (newPawns.Any())
                Find.LetterStack.ReceiveLetter("Outposts.Letters.Recruit.Label".Translate(__instance.Name),
                    "Outposts.Letters.Recruit.Desc".Translate(__instance.Name, newPawns.Select(p => p.NameFullColored.ToString()).ToLineList("  - ")),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(Gen.YieldSingle(__instance)));

            foreach (Pawn pawn in newPawns) __instance.AddPawn(pawn);
            return false;
        }

        /// <summary>
        /// Generates a recruit matching the recruiter's xenotype. For non-human recruiters or
        /// when Biotech is inactive, the recruiter's kindDef already fixes the race, so plain
        /// generation suffices (the recruit shares the recruiter's race).
        /// </summary>
        static Pawn GenerateMatching(Pawn recruiter)
        {
            if (ModsConfig.BiotechActive && recruiter.genes is object)
            {
                bool unique = recruiter.genes.UniqueXenotype;
                PawnGenerationRequest req = new PawnGenerationRequest(
                    recruiter.kindDef, recruiter.Faction, PawnGenerationContext.NonPlayer,
                    forcedXenotype: unique ? null : recruiter.genes.Xenotype,
                    forcedCustomXenotype: unique ? recruiter.genes.CustomXenotype : null);
                return PawnGenerator.GeneratePawn(req);
            }

            return PawnGenerator.GeneratePawn(recruiter.kindDef, recruiter.Faction);
        }
    }

    internal static class OutpostTownRequirements
    {
        /// <summary>
        /// Settlements within townRange of the target tile, counting only same-layer
        /// settlements so ApproxDistanceInTiles never crosses planet layers (the source of
        /// the "tile out of range" error).
        /// </summary>
        internal static int NearbySettlements(PlanetTile tile) =>
            Find.WorldObjects.Settlements.Count(s =>
                s.Tile.Layer == tile.Layer &&
                Find.WorldGrid.ApproxDistanceInTiles(s.Tile, tile) < EmpireVOESettings.townRange);

        /// <summary>
        /// Outposts within townRange of the target tile (same layer only). When
        /// townExcludeTowns is set, other Town outposts are not counted.
        /// </summary>
        internal static int NearbyOutposts(PlanetTile tile) =>
            Find.WorldObjects.AllWorldObjects.OfType<Outpost>().Count(o =>
                o.Tile.Layer == tile.Layer &&
                (!EmpireVOESettings.townExcludeTowns || !(o is Outpost_Town)) &&
                Find.WorldGrid.ApproxDistanceInTiles(o.Tile, tile) < EmpireVOESettings.townRange);
    }
}
