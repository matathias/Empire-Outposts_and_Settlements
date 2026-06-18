using HarmonyLib;
using Outposts;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Suppresses a production outpost's physical delivery while it is resource-linked to a settlement —
    /// its output is contributed abstractly through <see cref="WorldObjectComp_ResourceLink"/> instead of
    /// dropping crates to a colony map.
    /// <para>Patches the base <c>Outpost.Produce()</c>, which every production type uses (only
    /// <c>ProducedThings()</c> is overridden by subclasses). <c>TickInterval</c> resets the production timer
    /// before calling <c>Produce()</c>, so skipping it accrues no production debt and unlinking resumes
    /// cleanly. Science outposts feed research via a separate Tick/ResearchPerformed suppression and are
    /// left untouched here.</para>
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("Produce")]
    public static class Patch_OutpostProduce
    {
        private static bool Prefix(Outpost __instance)
        {
            if (!EmpireVOESettings.ResourceLinkActive) return true;
            if (__instance is Outpost_Science) return true;
            return !ResourceLinkUtil.IsLinked(__instance);
        }
    }
}
