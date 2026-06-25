using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FactionColonies;
using FactionColonies.util;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Single synchronous entry point for turning a VOE outpost into an Empire settlement.
    /// Replaces the old Harmony-patch + ISettlementListener approach.
    /// </summary>
    public static class OutpostConversionUtil
    {
        /// <summary>
        /// Set by the Specialists compat DLL when Matathias.Empire.Specialists is active.
        /// Receives the new settlement and the outpost pawns to assign as specialists/residents.
        /// Null when the Specialists submod is not loaded.
        /// </summary>
        public static Action<WorldSettlementFC, List<Pawn>> SpecialistsCallback;

        /// <summary>
        /// Set by the Routes &amp; Resources compat DLL when Matathias.Empire.SupplyChain is active.
        /// Invoked right after the conversion type-picker opens; the compat docks a companion window
        /// beside the picker that previews the (reduced) R&amp;R resource cost for the hovered type.
        /// Null when the SupplyChain submod is not loaded.
        /// </summary>
        public static Action<Outpost> ConversionCostCompanionOpener;

        /// <summary>
        /// True while <see cref="ConvertOutpost"/> runs its founding-cost validation. The
        /// founding-restriction validator honors this so a conversion — the intended way to found a
        /// settlement when "require outpost" is on — isn't vetoed by that same restriction.
        /// Single-threaded (RimWorld main thread), so a plain static flag is safe.
        /// </summary>
        public static bool IsConverting;

        // --- Convertible-type resolution ---

        /// <summary>
        /// Settlement types the given outpost can currently be converted into: unlocked and
        /// compatible with the outpost's planet layer. Empty if the outpost has no conversion mapping.
        /// </summary>
        public static List<WorldSettlementDef> GetConvertibleTypes(Outpost outpost)
        {
            List<WorldSettlementDef> result = new List<WorldSettlementDef>();
            if (outpost?.def is null) return result;

            OutpostConversionExtension ext = outpost.def.GetModExtension<OutpostConversionExtension>();
            if (ext is null) return result;

            IEnumerable<WorldSettlementDef> candidates = ext.allowAnySettlementType
                ? FactionCache.AvailableWorldSettlementDefs
                : ext.GetExplicitTypes();

            foreach (WorldSettlementDef d in candidates)
            {
                if (d is null) continue;
                if (!d.available) continue;
                if (!d.IsUnlocked()) continue;
                if (!d.AllowsTileLayer(outpost.Tile)) continue;
                result.Add(d);
            }
            return result;
        }

        /// <summary>
        /// Reverse lookup: outpost defs that can be converted into the given settlement type.
        /// Used by the founding-requirements companion window.
        /// </summary>
        public static List<WorldObjectDef> GetOutpostTypesFor(WorldSettlementDef type)
        {
            List<WorldObjectDef> result = new List<WorldObjectDef>();
            if (type is null) return result;

            foreach (WorldObjectDef wod in DefDatabase<WorldObjectDef>.AllDefsListForReading)
            {
                OutpostConversionExtension ext = wod.GetModExtension<OutpostConversionExtension>();
                if (ext is null) continue;
                if (ext.allowAnySettlementType || ext.GetExplicitTypes().Contains(type))
                    result.Add(wod);
            }
            return result;
        }

        // --- Availability (delay + busy) ---

        public static int DaysSinceEstablished(Outpost outpost)
        {
            if (outpost.creationGameTicks < 0) return int.MaxValue;
            return (Find.TickManager.TicksGame - outpost.creationGameTicks) / GenDate.TicksPerDay;
        }

        public static int DelayDaysRemaining(Outpost outpost)
        {
            return VOEFormulas.DelayDaysRemaining(
                DaysSinceEstablished(outpost),
                EmpireVOESettings.conversionDelayDays,
                EmpireVOESettings.enableConversionDelay);
        }

        /// <summary>
        /// True if the outpost can be converted right now. On false, <paramref name="reason"/> explains why
        /// (used as the gizmo's disabledReason and as a final guard in <see cref="ConvertOutpost"/>).
        /// </summary>
        public static bool CanConvertNow(Outpost outpost, out string reason)
        {
            reason = null;

            if (EmpireVOESettings.enableConversionDelay && DelayDaysRemaining(outpost) > 0)
            {
                reason = "FCVOE_ConvertAvailableInDays".Translate(DelayDaysRemaining(outpost).ToString());
                return false;
            }
            if (outpost.PawnCount == 0)
            {
                reason = "FCVOE_ConvertNoPawns".Translate();
                return false;
            }
            if (outpost.Packing)
            {
                reason = "FCVOE_ConvertBusyPacking".Translate();
                return false;
            }

            WorldObjectComp_EmpireOutpost eo = outpost.GetComponent<WorldObjectComp_EmpireOutpost>();
            if (eo?.raidTarget is object && eo.raidTarget.IsUnderAttack)
            {
                reason = "FCVOE_ConvertBusyAttacked".Translate();
                return false;
            }

            WorldObjectComp_EmpireDefensive def = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
            if (def?.defender is object && def.defender.Busy)
            {
                reason = "FCVOE_ConvertBusyDefending".Translate(def.defender.DefendingTargetName);
                return false;
            }

            return true;
        }

        // --- Conversion ---

        /// <summary>
        /// Converts the outpost into a settlement of the given type. Validates availability and cost,
        /// charges the reduced silver + (if Routes &amp; Resources is active) scaled resource cost,
        /// folds outpost pawns into the settlement, applies bonuses, then destroys the outpost.
        /// </summary>
        public static bool ConvertOutpost(Outpost outpost, WorldSettlementDef type)
        {
            if (outpost is null || type is null) return false;

            if (!CanConvertNow(outpost, out string busyReason))
            {
                Messages.Message(busyReason, MessageTypeDefOf.RejectInput);
                return false;
            }
            if (!GetConvertibleTypes(outpost).Contains(type))
            {
                Messages.Message("FCVOE_ConvertInvalidType".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            FactionFC faction = FindFC.FactionComp;
            PlanetTile tile = outpost.Tile;
            float factor = EmpireVOESettings.reducedFoundingCostFactor;

            // Cost gate (factor scales both silver and R&R resources)
            BiomeResourceDef biome = ResolveBiome(type, tile);
            int reducedSilver = (int)(ColonyUtil.GetFoundingCost(type, biome, faction) * factor);

            StringBuilder reason = new StringBuilder();
            bool canFound;
            IsConverting = true;
            try
            {
                canFound = FoundingValidatorRegistry.CanFound(tile, type, reason, factor);
            }
            finally
            {
                IsConverting = false;
            }
            if (!canFound)
            {
                Messages.Message(reason.ToString(), MessageTypeDefOf.RejectInput);
                return false;
            }
            if (PaymentUtil.GetSilver() < reducedSilver)
            {
                Messages.Message("FCNotEnoughSilverToSettle".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            // Pay
            if (!PaymentUtil.TryPaySilver(reducedSilver, PaymentUtil.Reason_SettlementCreation))
            {
                Messages.Message("FCNotEnoughSilverToSettle".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }
            FoundingValidatorRegistry.NotifyFounded(tile, type, factor);

            // Create the settlement on the outpost's tile, then fold pawns + apply bonuses.
            WorldSettlementFC settlement = ColonyUtil.CreatePlayerColonySettlement(tile, type);

            string outpostName = outpost.Name;
            int pawnCount = 0;
            if (EmpireVOESettings.convertOutpostPawns)
            {
                pawnCount = FoldOutpostPawns(outpost, settlement);
            }

            OutpostConversionExtension ext = outpost.def.GetModExtension<OutpostConversionExtension>();
            if (ext != null && ext.grantsTownAdditive && EmpireVOESettings.townFlatAdditive > 0)
            {
                WorldObjectComp_OutpostBonus bonusComp = settlement.GetComponent<WorldObjectComp_OutpostBonus>();
                if (bonusComp != null) bonusComp.SetTownFlatAdditive(EmpireVOESettings.townFlatAdditive);
            }

            outpost.Destroy();

            Find.LetterStack.ReceiveLetter(
                "FCVOE_OutpostIncorporated".Translate(outpostName),
                "FCVOE_OutpostIncorporatedDesc".Translate(outpostName, settlement.Name, pawnCount),
                LetterDefOf.PositiveEvent,
                new LookTargets(settlement));
            return true;
        }

        /// <summary>
        /// Moves the outpost's pawns into the new settlement: via the Specialists callback if loaded,
        /// otherwise transferred to the Empire faction and converted into skill-based production bonuses.
        /// Returns the number of pawns folded.
        /// </summary>
        private static int FoldOutpostPawns(Outpost outpost, WorldSettlementFC settlement)
        {
            List<Pawn> pawns = outpost.AllPawns.ToList();
            int count = pawns.Count;

            if (SpecialistsCallback != null)
            {
                try
                {
                    SpecialistsCallback(settlement, pawns);
                }
                catch (Exception e)
                {
                    VOELog.Error("OutpostConversionUtil: Specialists callback failed: " + e);
                }
                foreach (Pawn pawn in pawns)
                    outpost.RemovePawn(pawn);
                return count;
            }

            // No Specialists submod — apply skill bonuses and transfer pawns to the Empire faction.
            Dictionary<string, double> bonuses = CalculateSkillBonuses(pawns);
            Faction empireFaction = FindFC.EmpireFaction;
            foreach (Pawn pawn in pawns)
            {
                pawn.SetFaction(empireFaction);
                if (!pawn.IsWorldPawn())
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                outpost.RemovePawn(pawn);
            }

            if (bonuses.Count > 0)
            {
                WorldObjectComp_OutpostBonus bonusComp = settlement.GetComponent<WorldObjectComp_OutpostBonus>();
                if (bonusComp != null)
                {
                    foreach (KeyValuePair<string, double> kvp in bonuses)
                        bonusComp.SetBonus(kvp.Key, kvp.Value);
                }
                else
                {
                    VOELog.Warning("OutpostConversionUtil: Settlement missing WorldObjectComp_OutpostBonus. Skill bonuses not applied.");
                }
            }
            return count;
        }

        /// <summary>
        /// Per-resource production bonus from pawn skills (same per-skill-level formula as Specialists).
        /// </summary>
        private static Dictionary<string, double> CalculateSkillBonuses(List<Pawn> pawns)
        {
            Dictionary<string, double> bonuses = new Dictionary<string, double>();

            foreach (ResourceTypeDef rtd in DefDatabase<ResourceTypeDef>.AllDefsListForReading)
            {
                if (rtd.associatedSkills is null || rtd.associatedSkills.Count == 0) continue;

                double bonus = 0;
                foreach (Pawn pawn in pawns)
                {
                    if (pawn.skills is null) continue;
                    foreach (SkillDef skillDef in rtd.associatedSkills)
                    {
                        SkillRecord record = pawn.skills.GetSkill(skillDef);
                        if (record is null || record.TotallyDisabled) continue;
                        bonus += VOEFormulas.SkillContribution(
                            record.Level, EmpireVOESettings.skillFloor, EmpireVOESettings.additivePerLevel);
                    }
                }

                if (bonus > 0)
                    bonuses[rtd.defName] = bonus;
            }

            return bonuses;
        }

        private static BiomeResourceDef ResolveBiome(WorldSettlementDef type, PlanetTile tile)
        {
            if (type.biomeResourceOverride != null)
            {
                if (FactionCache.BiomeResourceDefSet.Contains(type.biomeResourceOverride))
                    return type.biomeResourceOverride;
                return BiomeResourceDefOf.defaultBiome;
            }
            BiomeResourceDef biome = DefDatabase<BiomeResourceDef>.GetNamed(tile.Tile.PrimaryBiome.defName, false);
            return biome ?? BiomeResourceDefOf.defaultBiome;
        }
    }
}
