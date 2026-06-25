using FactionColonies;
using FactionColonies.SupplyChain;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE.SupplyChain
{
    /// <summary>
    /// While founding is restricted to outposts, the Found-screen "Travel Time" should reflect the
    /// caravan trip from the player's own colony (the base-mod behavior), not R&amp;R's nearest-settlement
    /// distance model — in that mode the player sends a caravan from their colony to establish the
    /// outpost. No-op when the restriction is off, where R&amp;R's distance-based time is intended.
    /// </summary>
    [HarmonyPatch(typeof(SCSettlementTypeExtension))]
    [HarmonyPatch("GetCreationTime")]
    public static class Patch_CreationTimeFromCapital
    {
        private static bool Prefix(PlanetTile destination, ref int __result)
        {
            if (!VOESupplyChainCompat.RestrictionActive) return true;

            PlanetTile origin = FindFC.CapitalLocation;
            if (!origin.Valid && Find.AnyPlayerHomeMap != null)
                origin = Find.AnyPlayerHomeMap.Tile;

            __result = TravelUtil.ReturnTicksToArrive(origin, destination);
            return false;
        }
    }
}
