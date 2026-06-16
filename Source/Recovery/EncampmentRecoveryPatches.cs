using HarmonyLib;
using Outposts;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Invalidates the encampment cache when any encampment's pawn roster changes.
    /// RecachePawnTraits is called by AddPawn, RemovePawn, SpawnSetup, and ExposeData.
    /// The cache backs the medicine-scaled merc heal-rate bonus (WorldObjectComp_EncampmentBonus).
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("RecachePawnTraits")]
    public static class Patch_RecachePawnTraits
    {
        private static void Postfix(Outpost __instance)
        {
            if (__instance is Outpost_Encampment)
                EncampmentCache.Invalidate();
        }
    }
}
