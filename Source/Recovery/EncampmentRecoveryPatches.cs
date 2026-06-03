using System;
using FactionColonies;
using HarmonyLib;
using Outposts;
using RimWorld;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Reduces military cooldown duration when an Outpost_Encampment is nearby.
    /// Patches CooldownMilitaryFinal postfix so the cooldown event already exists.
    /// Uses EncampmentCache for precomputed Medicine skill data.
    /// </summary>
    [HarmonyPatch(typeof(WorldObjectComp_SettlementMilitary))]
    [HarmonyPatch("CooldownMilitaryFinal")]
    public static class Patch_CooldownMilitaryFinal
    {
        private static void Postfix(WorldObjectComp_SettlementMilitary __instance)
        {
            if (!EmpireVOESettings.EncampmentActive) return;

            WorldSettlementFC settlement = __instance.WorldSettlement;
            if (settlement is null) return;

            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return;

            EncampmentCacheEntry entry = EncampmentCache.GetOrBuild(settlement);
            if (entry is null || entry.encampments.Count == 0) return;

            // Find the just-created cooldown event (highest timeTillTrigger at this tile)
            FCEvent cooldownEvent = null;
            int highestTrigger = -1;
            foreach (FCEvent evt in faction.Events)
            {
                if (evt.def != FCEventDefOf.cooldownMilitary) continue;
                if (evt.location != settlement.Tile) continue;
                if (evt.timeTillTrigger > highestTrigger)
                {
                    highestTrigger = evt.timeTillTrigger;
                    cooldownEvent = evt;
                }
            }

            if (cooldownEvent is null) return;

            // Calculate reduction from cached per-encampment Medicine data
            double totalReduction = 0;
            foreach (EncampmentData data in entry.encampments)
            {
                double encReduction = EmpireVOESettings.encampmentBaseReduction
                                     + (data.avgMedicine * EmpireVOESettings.encampmentMedicineScaling);
                totalReduction += encReduction;
            }

            totalReduction = Math.Min(totalReduction, EmpireVOESettings.encampmentMaxReduction);
            if (totalReduction <= 0) return;

            int currentCooldown = cooldownEvent.timeTillTrigger - Find.TickManager.TicksGame;
            if (currentCooldown <= 0) return;
            int reduction = (int)(currentCooldown * totalReduction);
            cooldownEvent.timeTillTrigger -= reduction;

            string daysReduced = (reduction / (float)GenDate.TicksPerDay).ToString("F1");
            Find.LetterStack.ReceiveLetter(
                "VOE_EncampmentRecovery".Translate(settlement.Name),
                "VOE_EncampmentRecoveryDesc".Translate(
                    settlement.Name,
                    daysReduced,
                    entry.encampments.Count),
                LetterDefOf.PositiveEvent,
                new LookTargets(settlement));

            VOELog.Message("Encampment recovery reduced cooldown for " + settlement.Name
                           + " by " + daysReduced + " days (" + (totalReduction * 100).ToString("F0") + "%)");
        }
    }

    /// <summary>
    /// Invalidates the encampment cache when any encampment's pawn roster changes.
    /// RecachePawnTraits is called by AddPawn, RemovePawn, SpawnSetup, and ExposeData.
    /// </summary>
    [HarmonyPatch(typeof(Outpost))]
    [HarmonyPatch("RecachePawnTraits")]
    public static class Patch_RecachePawnTraits
    {
        private static void Postfix(Outpost __instance)
        {
            if (__instance is Outpost_Encampment)
                EncampmentCache.Invalidate();
        }
    }
}
