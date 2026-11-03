using HarmonyLib;
using Verse;
using FactionColonies.util;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Postfix on Outpost_Defensive.ReinforcementsDisabled — disables the "Send Reinforcements"
    /// gizmo when the outpost is on Empire military cooldown.
    /// </summary>
    [HarmonyPatch(typeof(Outpost_Defensive))]
    [HarmonyPatch("ReinforcementsDisabled")]
    public static class Patch_ReinforcementsDisabled
    {
        public static void Postfix(Outpost_Defensive __instance, ref bool __result, ref string reason)
        {
            if (__result) return;
            if (!EmpireVOESettings.MilitaryActive) return;

            WorldObjectComp_EmpireDefensive comp = __instance.GetComponent<WorldObjectComp_EmpireDefensive>();
            if (comp is null || !comp.IsOnCooldown) return;

            __result = true;
            reason = "FCVOE_ReinforcementsCooldown".Translate(comp.CooldownTicksLeft.ToTimeString());
        }
    }
}
