using FactionColonies.SupplyChain;
using HarmonyLib;

namespace EmpireVOE.SupplyChain
{
    /// <summary>
    /// While founding is restricted to outposts, suppress R&amp;R's own Found-screen cost window
    /// (<see cref="FCWindow_FoundingSource"/>). It shows the full cost framed as paid-now-to-found;
    /// our <see cref="FCWindow_VOEFoundingConversionCost"/> replaces it with the reduced,
    /// paid-at-conversion preview. No-op when the restriction is off (R&amp;R behaves as normal).
    /// </summary>
    [HarmonyPatch(typeof(FCWindow_FoundingSource))]
    [HarmonyPatch("TryOpen")]
    public static class Patch_SuppressFoundingSource
    {
        private static bool Prefix()
        {
            return !VOESupplyChainCompat.RestrictionActive;
        }
    }
}
