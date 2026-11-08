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
        new Harmony("com.Matathias.EmpireVOE").PatchEachClass(Assembly.GetExecutingAssembly(), VOELog.Error);
        EmpireRegistry.RegisterPersistent(OutpostFinancer.Instance);
        EmpireRegistry.RegisterPersistent(_foundingRestrictionValidator);
        EmpireRegistry.RegisterPersistent(OutpostDeliveryInterceptor.Instance);
        EmpireRegistry.RegisterPersistent(VOERoadNodeProvider.Instance);
        EmpireRegistry.RegisterPersistent(DefensiveAuraRaidWeightProvider.Instance);
        OutpostMainTab.SetRegistered(EmpireVOESettings.OutpostMainTabActive);
        EmpireCacheUtil.RegisterCacheInvalidator("EmpireVOE", () =>
        {
            // Drop the PlanetTile-keyed static caches — they collide across worlds, so
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
