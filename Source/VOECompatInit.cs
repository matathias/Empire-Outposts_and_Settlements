using System.Reflection;
using FactionColonies;
using FactionColonies.util;
using HarmonyLib;
using Verse;

namespace EmpireVOE
{
    [StaticConstructorOnStartup]
    public static class VOECompatInit
    {
        private static readonly FoundingRestrictionValidator _foundingRestrictionValidator = new FoundingRestrictionValidator();

        static VOECompatInit()
        {
            new Harmony("com.Matathias.EmpireVOE").PatchAll(Assembly.GetExecutingAssembly());
            EmpireRegistry.Register(OutpostFinancer.Instance);
            EmpireRegistry.Register(_foundingRestrictionValidator);
            EmpireRegistry.Register(OutpostDeliveryInterceptor.Instance);
            EmpireRegistry.Register(VOERoadNodeProvider.Instance);
            EmpireCacheUtil.RegisterCacheInvalidator("EmpireVOE", () =>
            {
                // Re-register after InvalidateAll clears all registries
                EmpireRegistry.Register(OutpostFinancer.Instance);
                EmpireRegistry.Register(_foundingRestrictionValidator);
                EmpireRegistry.Register(OutpostDeliveryInterceptor.Instance);
                EmpireRegistry.Register(VOERoadNodeProvider.Instance);
            });

            // Found-screen integration: replace the Settle button with "Send a Caravan" and drive the
            // outpost-requirements companion window when founding is restricted to outposts.
            OutpostFoundingScreen.Install();

            VOELog.MessageForce("Empire - Vanilla Outposts Expanded integration loaded.");
        }
    }
}
