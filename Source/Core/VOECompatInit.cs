using System.Reflection;
using HarmonyLib;

namespace EmpireVOE;

[StaticConstructorOnStartup]
public static class VOECompatInit
{
    private static readonly FoundingRestrictionValidator _foundingRestrictionValidator = new();

    static VOECompatInit()
    {
        EmpireVOECompat.CheckForMods();
        new Harmony("com.Matathias.EmpireVOE").PatchAll(Assembly.GetExecutingAssembly());
        EmpireRegistry.Register(OutpostFinancer.Instance);
        EmpireRegistry.Register(_foundingRestrictionValidator);
        EmpireRegistry.Register(OutpostDeliveryInterceptor.Instance);
        EmpireRegistry.Register(VOERoadNodeProvider.Instance);
        EmpireRegistry.Register(DefensiveAuraRaidWeightProvider.Instance);
        if (EmpireVOESettings.OutpostMainTabActive)
            EmpireRegistry.Register(OutpostMainTab.Instance);
        EmpireCacheUtil.RegisterCacheInvalidator("EmpireVOE", () =>
        {
            // Re-register after InvalidateAll clears all registries
            EmpireRegistry.Register(OutpostFinancer.Instance);
            EmpireRegistry.Register(_foundingRestrictionValidator);
            EmpireRegistry.Register(OutpostDeliveryInterceptor.Instance);
            EmpireRegistry.Register(VOERoadNodeProvider.Instance);
            EmpireRegistry.Register(DefensiveAuraRaidWeightProvider.Instance);
            if (EmpireVOESettings.OutpostMainTabActive)
                EmpireRegistry.Register(OutpostMainTab.Instance);
            // Drop the PlanetTile-keyed static caches too — they collide across worlds, so
            // loading another save would otherwise inherit the previous world's auras/heal
            // bonuses and pin its outpost objects.
            DefensiveAuraCache.Invalidate();
            EncampmentCache.Invalidate();
        });

        // Found-screen integration: replace the Settle button with "Send a Caravan" and drive the
        // outpost-requirements companion window when founding is restricted to outposts.
        OutpostFoundingScreen.Install();

        VOELog.MessageForce("Empire - Vanilla Outposts Expanded integration loaded.");
    }
}
