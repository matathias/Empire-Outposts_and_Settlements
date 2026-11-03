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

    /// <summary>
    /// Blanks the outpost's <c>ProductionString()</c> while it is resource-linked, so the world-map infobox
    /// no longer shows a misleading "Will produce …" line for output that is being contributed abstractly
    /// rather than physically delivered. VOE treats an empty production string as "no line" (see
    /// <c>Utils.Line</c>), so returning "" cleanly removes it — and, as a deliberate consequence, also hides
    /// the now-irrelevant "Deliver to colony" gizmo (gated on the same string). Both reappear on unlink.
    /// <para>Unlike <see cref="Patch_OutpostProduce"/> there is no science carve-out: a linked science
    /// outpost's research is suppressed, so its line should be hidden too.</para>
    /// </summary>
    internal static class ProductionStringSuppression
    {
        /// <summary>Returns true if the line was suppressed, in which case the caller should skip the original.</summary>
        internal static bool TrySuppress(Outpost outpost, ref string result)
        {
            if (!EmpireVOESettings.ResourceLinkActive) return false;
            if (!ResourceLinkUtil.IsLinked(outpost)) return false;
            result = "";
            return true;
        }
    }

    /// <summary>Base patch — covers every linkable outpost type that inherits <c>Outpost.ProductionString</c>.</summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("ProductionString")]
    public static class Patch_OutpostProductionString
    {
        private static bool Prefix(Outpost __instance, ref string __result)
            => !ProductionStringSuppression.TrySuppress(__instance, ref __result);
    }

    /// <summary>
    /// Drilling can override <c>ProductionString</c> with its own progress branch (not always routed through
    /// base), so it gets its own patch to guarantee suppression.
    /// </summary>
    [HarmonyPatch(typeof(Outpost_Drilling))]
    [HarmonyPatch("ProductionString")]
    public static class Patch_OutpostDrillingProductionString
    {
        private static bool Prefix(Outpost __instance, ref string __result)
            => !ProductionStringSuppression.TrySuppress(__instance, ref __result);
    }
}
