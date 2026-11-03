using HarmonyLib;
using Outposts;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Invalidates the caches that depend on an outpost's pawn roster when it changes.
    /// RecachePawnTraits is called by AddPawn, RemovePawn, SpawnSetup, and ExposeData.
    /// Backs the medicine-scaled merc heal-rate bonus (encampments), the passive defensive aura
    /// (defensive outposts), and skill-scaled resource production (linked production outposts).
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("RecachePawnTraits")]
    public static class Patch_RecachePawnTraits
    {
        private static void Postfix(Outpost __instance)
        {
            if (__instance is Outpost_Encampment)
                EncampmentCache.Invalidate();
            if (__instance is Outpost_Defensive)
                DefensiveAuraCache.Invalidate();
            // A linked production outpost's skills feed its settlement's resource output; refresh it.
            ResourceLinkUtil.NotifyOutpostRosterChanged(__instance);
        }
    }
}
