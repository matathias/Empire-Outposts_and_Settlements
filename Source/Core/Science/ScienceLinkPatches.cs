using HarmonyLib;
using VOE;

namespace EmpireVOE;

/// <summary>
/// Static flag for research suppression. Set by Outpost_Science.Tick prefix,
/// checked by ResearchManager.ResearchPerformed prefix, cleared by Tick finalizer.
/// Safe because RimWorld is single-threaded.
/// </summary>
internal static class ScienceResearchSuppression
{
    internal static bool suppressResearch;
}

/// <summary>
/// Sets/clears the suppression flag around Outpost_Science.Tick() so that
/// ResearchPerformed calls within it are blocked for linked outposts.
/// </summary>
[HarmonyPatch(typeof(Outpost_Science))]
[HarmonyPatch("Tick")]
public static class Patch_ScienceTick
{
    private static void Prefix(Outpost_Science __instance)
    {
        if (!EmpireVOESettings.ResourceLinkActive) return;
        if (ResourceLinkUtil.IsLinked(__instance))
        {
            ScienceResearchSuppression.suppressResearch = true;
        }
    }

    // Finalizer (not Postfix) so the flag is always cleared even if Tick() throws — a Postfix
    // would be skipped on exception, leaving research suppressed for every colony until another
    // science outpost ticks cleanly.
    private static Exception Finalizer(Exception __exception)
    {
        ScienceResearchSuppression.suppressResearch = false;
        return __exception;
    }
}

/// <summary>
/// Skips ResearchPerformed when the suppression flag is set, preventing
/// linked science outposts from contributing to the vanilla research pool.
/// </summary>
[HarmonyPatch(typeof(ResearchManager))]
[HarmonyPatch("ResearchPerformed")]
public static class Patch_ResearchPerformed
{
    private static bool Prefix()
    {
        return !ScienceResearchSuppression.suppressResearch;
    }
}
