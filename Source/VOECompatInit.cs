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
        private static readonly TownConversionHandler _townConversionHandler = new TownConversionHandler();
        internal static readonly OutpostThreatContributor ThreatContributor = new OutpostThreatContributor();

        static VOECompatInit()
        {
            new Harmony("com.Matathias.EmpireVOE").PatchAll(Assembly.GetExecutingAssembly());
            SilverPaymentRegistry.Register(OutpostFinancer.Instance);
            LifecycleRegistry.Register(_townConversionHandler);
            ThreatScalingRegistry.Register(ThreatContributor);
            TaxDeliveryRegistry.Register(OutpostDeliveryInterceptor.Instance);
            EmpireCacheUtil.RegisterCacheInvalidator("EmpireVOE", () =>
            {
                // Re-register after InvalidateAll clears all registries
                SilverPaymentRegistry.Register(OutpostFinancer.Instance);
                LifecycleRegistry.Register(_townConversionHandler);
                ThreatScalingRegistry.Register(ThreatContributor);
                TaxDeliveryRegistry.Register(OutpostDeliveryInterceptor.Instance);
            });
            VOELog.MessageForce("Empire - Vanilla Outposts Expanded integration loaded.");
        }
    }
}
