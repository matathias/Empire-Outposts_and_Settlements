using System.Collections.Generic;
using System.Reflection;
using FactionColonies;
using FactionColonies.util;
using HarmonyLib;
using Outposts;
using Verse;
using VOE;

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
            EmpireCacheUtil.RegisterCacheInvalidator("EmpireVOE", () =>
            {
                // Re-register after InvalidateAll clears all registries
                SilverPaymentRegistry.Register(OutpostFinancer.Instance);
                LifecycleRegistry.Register(_townConversionHandler);
                ThreatScalingRegistry.Register(ThreatContributor);
            });
            VOELog.MessageForce("Empire - Vanilla Outposts Expanded integration loaded.");
        }
    }

    /// <summary>
    /// Registers outpost wrappers with Empire's registries when an outpost spawns.
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("SpawnSetup")]
    public static class Patch_SpawnSetup
    {
        private static void Postfix(Outpost __instance)
        {
            if (EmpireVOESettings.disableIntegration) return;
            if (WorldComponent_VOETracker.GetRaidTarget(__instance) != null) return;

            OutpostRaidTarget target = new OutpostRaidTarget(__instance);
            WorldComponent_VOETracker.RegisterOutpost(__instance, target);

            if (__instance is Outpost_Encampment)
                EncampmentCache.Invalidate();

            if (__instance is Outpost_Defensive defensive)
            {
                DefensiveAutoDefender defender = new DefensiveAutoDefender(defensive);
                DefensiveTabEntry tab = new DefensiveTabEntry(defensive, defender);
                WorldComponent_VOETracker.RegisterDefensive(defensive, defender, tab);
            }
        }
    }

    /// <summary>
    /// Unregisters outpost wrappers from Empire's registries when an outpost is removed.
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("PostRemove")]
    public static class Patch_PostRemove
    {
        private static void Postfix(Outpost __instance)
        {
            WorldComponent_VOETracker.UnregisterOutpost(__instance);

            if (__instance is Outpost_Encampment)
                EncampmentCache.Invalidate();
        }
    }

    /// <summary>
    /// Adds the "Change Defender" gizmo when the outpost is under attack.
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("GetGizmos")]
    public static class Patch_GetGizmos
    {
        private static void Postfix(Outpost __instance, ref IEnumerable<Gizmo> __result)
        {
            if (EmpireVOESettings.disableIntegration) return;

            FCEvent evt = null;
            IReadOnlyList<FCEvent> attackEvents = FactionCache.FactionComp?.GetEventsByDef(FCEventDefOf.settlementBeingAttacked);
            if (attackEvents != null)
            {
                for (int i = 0; i < attackEvents.Count; i++)
                {
                    if (attackEvents[i].settlementFCDefending == __instance)
                    {
                        evt = attackEvents[i];
                        break;
                    }
                }
            }

            if (evt is null) return;

            __result = AddGizmo(__result, __instance);
        }

        private static IEnumerable<Gizmo> AddGizmo(IEnumerable<Gizmo> original, Outpost outpost)
        {
            foreach (Gizmo g in original)
                yield return g;
            yield return OutpostDefenderGizmo.CreateGizmo(outpost);
        }
    }

    /// <summary>
    /// Appends Military Level to the outpost inspect string on the world map.
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("GetInspectString")]
    public static class Patch_GetInspectString
    {
        private static void Postfix(Outpost __instance, ref string __result)
        {
            if (EmpireVOESettings.disableIntegration) return;

            OutpostRaidTarget target = WorldComponent_VOETracker.GetRaidTarget(__instance);
            if (target is null) return;

            if (!__result.NullOrEmpty())
                __result += "\n";
            __result += "VOE_MilitaryLevel".Translate(target.MilitaryLevel * FCSettings.defenderAdvantage);

            // Science link status
            if (__instance is Outpost_Science science)
            {
                WorldSettlementFC linked = WorldComponent_VOETracker.GetLinkedSettlement(science);
                __result += "\n" + (linked != null
                    ? "VOE_LinkedTo".Translate(linked.Name)
                    : "VOE_NotLinked".Translate());
            }

            // Defensive outpost status
            if (__instance is Outpost_Defensive defensive)
            {
                if (target.IsUnderAttack)
                {
                    __result += "\n" + "VOE_DefenseStatusAttacked".Translate();
                }
                else if (WorldComponent_VOETracker.IsOnCooldown(defensive))
                {
                    int ticksLeft = WorldComponent_VOETracker.GetCooldownTicksLeft(defensive);
                    __result += "\n" + "VOE_DefenseStatusCooldown".Translate(ticksLeft.ToTimeString());
                }
                else
                {
                    __result += "\n" + "VOE_DefenseStatusReady".Translate();
                }
            }

            // Financing info (reverse lookup)
            List<string> financedNames = WorldComponent_VOETracker.GetFinancedSettlementNames(__instance);
            if (financedNames.Count > 0)
            {
                __result += "\n" + "VOE_FinancingFor".Translate(string.Join(", ", financedNames));
            }

            // Delivery source info (reverse lookup)
            List<string> deliveryNames = WorldComponent_VOETracker.GetDeliverySourceNames(__instance);
            if (deliveryNames.Count > 0)
            {
                __result += "\n" + "VOE_ReceivingTaxes".Translate(string.Join(", ", deliveryNames));
            }
        }
    }
}
