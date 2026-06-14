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
            EmpireRegistry.Register(OutpostFinancer.Instance);
            EmpireRegistry.Register(_townConversionHandler);
            EmpireRegistry.Register(ThreatContributor);
            EmpireRegistry.Register(OutpostDeliveryInterceptor.Instance);
            EmpireRegistry.Register(VOERoadNodeProvider.Instance);
            EmpireCacheUtil.RegisterCacheInvalidator("EmpireVOE", () =>
            {
                // Re-register after InvalidateAll clears all registries
                EmpireRegistry.Register(OutpostFinancer.Instance);
                EmpireRegistry.Register(_townConversionHandler);
                EmpireRegistry.Register(ThreatContributor);
                EmpireRegistry.Register(OutpostDeliveryInterceptor.Instance);
                EmpireRegistry.Register(VOERoadNodeProvider.Instance);
            });
            VOELog.MessageForce("Empire - Vanilla Outposts Expanded integration loaded.");
        }
    }
}
