using System.Linq;
using HarmonyLib;
using Outposts;
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
                __result = "VOE_TownNearbySettlements".Translate(EmpireVOESettings.townMinSettlements, EmpireVOESettings.townRange);
            else if (outposts < EmpireVOESettings.townMinOutposts)
                __result = "VOE_TownNearbyOutposts".Translate(EmpireVOESettings.townMinOutposts, EmpireVOESettings.townRange);
            else if (settlements + outposts < EmpireVOESettings.townMinTotal)
                __result = "VOE_TownNearbyTotal".Translate(EmpireVOESettings.townMinTotal, EmpireVOESettings.townRange);
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

            __result = "VOE_TownRequirements".Translate(
                EmpireVOESettings.townMinTotal, EmpireVOESettings.townRange,
                EmpireVOESettings.townMinSettlements, EmpireVOESettings.townMinOutposts).Requirement(passed);

            return false;
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
