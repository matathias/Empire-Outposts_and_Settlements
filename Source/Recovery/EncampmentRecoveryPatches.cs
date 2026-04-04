using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using HarmonyLib;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Reduces military cooldown duration when an Outpost_Encampment is nearby.
    /// Patches CooldownMilitaryFinal postfix so the cooldown event already exists.
    /// </summary>
    [HarmonyPatch(typeof(WorldObjectComp_SettlementMilitary))]
    [HarmonyPatch("CooldownMilitaryFinal")]
    public static class Patch_CooldownMilitaryFinal
    {
        private static void Postfix(WorldObjectComp_SettlementMilitary __instance)
        {
            if (EmpireVOESettings.disableIntegration) return;

            WorldSettlementFC settlement = __instance.WorldSettlement;
            if (settlement is null) return;

            // Find encampments within range
            List<Outpost_Encampment> nearbyEncampments = new List<Outpost_Encampment>();
            foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
            {
                Outpost_Encampment enc = wo as Outpost_Encampment;
                if (enc is null) continue;
                if (enc.PawnCount == 0) continue;
                float distance = Find.WorldGrid.ApproxDistanceInTiles(enc.Tile, settlement.Tile);
                if (distance <= EmpireVOESettings.encampmentRange)
                {
                    nearbyEncampments.Add(enc);
                }
            }

            if (nearbyEncampments.Count == 0) return;

            // Find the just-created cooldown event (highest timeTillTrigger at this tile)
            FactionFC faction = FactionCache.FactionComp;
            if (faction is null) return;

            FCEvent cooldownEvent = null;
            int highestTrigger = -1;
            foreach (FCEvent evt in faction.events)
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

            // Calculate reduction from all nearby encampments
            double totalReduction = 0;
            foreach (Outpost_Encampment enc in nearbyEncampments)
            {
                double avgMedicine = enc.CapablePawns
                    .Where(p => p.skills != null)
                    .Select(p => (double)p.skills.GetSkill(SkillDefOf.Medicine).Level)
                    .DefaultIfEmpty(0)
                    .Average();
                double encReduction = EmpireVOESettings.encampmentBaseReduction
                                     + (avgMedicine * EmpireVOESettings.encampmentMedicineScaling);
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
                    nearbyEncampments.Count),
                LetterDefOf.PositiveEvent,
                new LookTargets(settlement));

            VOELog.Message("Encampment recovery reduced cooldown for " + settlement.Name
                           + " by " + daysReduced + " days (" + (totalReduction * 100).ToString("F0") + "%)");
        }
    }
}
