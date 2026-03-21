using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FactionColonies;
using HarmonyLib;
using Outposts;
using Verse;
using VOE;

namespace EmpireVOE
{
    [StaticConstructorOnStartup]
    public static class VOECompatInit
    {
        static VOECompatInit()
        {
            new Harmony("com.Matathias.EmpireVOE").PatchAll(Assembly.GetExecutingAssembly());
            SilverPaymentRegistry.Register(OutpostFinancer.Instance);
            LifecycleRegistry.Register(new TownConversionHandler());
            LogUtil.MessageForce("Empire - Vanilla Outposts Expanded integration loaded.");
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
            if (VOETracker.GetRaidTarget(__instance) != null) return;

            OutpostRaidTarget target = new OutpostRaidTarget(__instance);
            VOETracker.RegisterOutpost(__instance, target);

            Outpost_Defensive defensive = __instance as Outpost_Defensive;
            if (defensive != null)
            {
                DefensiveAutoDefender defender = new DefensiveAutoDefender(defensive);
                DefensiveTabEntry tab = new DefensiveTabEntry(defensive, defender);
                VOETracker.RegisterDefensive(defensive, defender, tab);
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
            VOETracker.UnregisterOutpost(__instance);
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

            FactionFC faction = FactionCache.FactionComp;
            if (faction == null) return;

            FCEvent evt = faction.events.FirstOrDefault(e =>
                e.def == FCEventDefOf.settlementBeingAttacked
                && e.settlementFCDefending == __instance);

            if (evt == null) return;

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

            OutpostRaidTarget target = VOETracker.GetRaidTarget(__instance);
            if (target == null) return;

            if (!__result.NullOrEmpty())
                __result += "\n";
            __result += "VOE_MilitaryLevel".Translate(target.MilitaryLevel * FCSettings.defenderAdvantage);
        }
    }
}
