using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FactionColonies;
using FactionColonies.util;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Harmony prefix that replaces tile validation when the "Require Town" setting is enabled.
    /// Bypasses the normal TileFinder check (which would reject tiles with existing world objects)
    /// and instead requires an Outpost_Town on the target tile.
    /// </summary>
    [HarmonyPatch(typeof(WorldTileChecker))]
    [HarmonyPatch("IsValidTileForNewSettlement")]
    public static class Patch_IsValidTileForNewSettlement
    {
        static bool Prefix(PlanetTile tile, WorldSettlementDef settlementdef, StringBuilder reason, ref bool __result)
        {
            if (!EmpireVOESettings.requireTownForSettlement) return true;

            if (tile == -1)
            {
                reason?.Append("selectedInvalidTile".Translate());
                __result = false;
                return false;
            }

            // Planet layer check (copied from original)
            if (settlementdef.planetLayers.Count == 0)
            {
                if (tile.Layer != Find.WorldGrid.Surface)
                {
                    reason?.Append("InvalidPlanetLayer".Translate());
                    __result = false;
                    return false;
                }
            }
            else
            {
                if (!settlementdef.planetLayers.Contains(tile.Layer.Def))
                {
                    reason?.Append("InvalidPlanetLayer".Translate());
                    __result = false;
                    return false;
                }
            }

            // Require Outpost_Town on tile
            Outpost_Town town = TownSettlementUtil.FindTownOnTile(tile);
            if (town is null)
            {
                reason?.Append("VOE_NotATown".Translate());
                __result = false;
                return false;
            }

            // Biome restrictions from settlement type
            if (settlementdef.allowedBiomes != null && settlementdef.allowedBiomes.Count > 0)
            {
                bool foundAllowed = false;
                foreach (BiomeDef biome in tile.Tile.Biomes)
                {
                    if (settlementdef.allowedBiomes.Contains(biome))
                    {
                        foundAllowed = true;
                        break;
                    }
                }
                if (!foundAllowed)
                {
                    reason?.Append("NotAllowedBiome".Translate(settlementdef.LabelCap));
                    __result = false;
                    return false;
                }
            }
            if (settlementdef.blockedBiomes != null && settlementdef.blockedBiomes.Count > 0)
            {
                foreach (BiomeDef biome in tile.Tile.Biomes)
                {
                    if (settlementdef.blockedBiomes.Contains(biome))
                    {
                        reason?.Append("NotAllowedBiome".Translate(settlementdef.LabelCap));
                        __result = false;
                        return false;
                    }
                }
            }

            __result = true;
            return false;
        }
    }

    /// <summary>
    /// Utility methods for Town-related lookups.
    /// </summary>
    public static class TownSettlementUtil
    {
        public static Outpost_Town FindTownOnTile(PlanetTile tile)
        {
            foreach (WorldObject obj in Find.WorldObjects.AllWorldObjects)
            {
                if (obj is Outpost_Town town && town.Tile == tile)
                {
                    return town;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Lifecycle handler that converts a Town outpost into an Empire settlement on creation.
    /// When the Specialists compat DLL is loaded, it sets <see cref="SpecialistsCallback"/>
    /// to handle pawn assignment directly. Otherwise, falls back to skill-based production bonuses.
    /// </summary>
    public class TownConversionHandler : LifecycleParticipantBase
    {
        /// <summary>
        /// Set by the Specialists compat DLL when Matathias.Empire.Specialists is active.
        /// Receives the new settlement and the list of town pawns to assign.
        /// Null when the Specialists submod is not loaded.
        /// </summary>
        public static Action<WorldSettlementFC, List<Pawn>> SpecialistsCallback;

        public override void OnSettlementCreated(WorldSettlementFC settlement)
        {
            if (!EmpireVOESettings.requireTownForSettlement) return;

            Outpost_Town town = TownSettlementUtil.FindTownOnTile(settlement.Tile);
            if (town is null) return;

            string townName = town.Name;
            int pawnCount = 0;

            if (EmpireVOESettings.convertTownPawns)
            {
                List<Pawn> townPawns = town.AllPawns.ToList();
                pawnCount = townPawns.Count;

                if (SpecialistsCallback != null)
                {
                    // Specialists submod handles pawn assignment
                    try
                    {
                        SpecialistsCallback(settlement, townPawns);
                    }
                    catch (Exception e)
                    {
                        VOELog.Error("TownConversionHandler: Specialists callback failed: " + e);
                    }
                    // Remove pawns from town (AssignPawn already handles faction + world pawn)
                    foreach (Pawn pawn in townPawns)
                    {
                        town.RemovePawn(pawn);
                    }
                }
                else
                {
                    // No Specialists submod — apply skill bonuses if enabled
                    Dictionary<string, double> bonuses = null;
                    if (EmpireVOESettings.pawnSkillBonuses)
                    {
                        bonuses = CalculateSkillBonuses(townPawns);
                    }

                    // Transfer pawns to Empire faction
                    Faction empireFaction = FactionCache.PlayerColonyFaction;
                    foreach (Pawn pawn in townPawns)
                    {
                        pawn.SetFaction(empireFaction);
                        if (!pawn.IsWorldPawn())
                        {
                            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                        }
                        town.RemovePawn(pawn);
                    }

                    // Apply bonuses to the TownBonus comp
                    if (bonuses != null && bonuses.Count > 0)
                    {
                        WorldObjectComp_TownBonus bonusComp = settlement.GetComponent<WorldObjectComp_TownBonus>();
                        if (bonusComp != null)
                        {
                            foreach (KeyValuePair<string, double> kvp in bonuses)
                            {
                                bonusComp.SetBonus(kvp.Key, kvp.Value);
                            }
                        }
                        else
                        {
                            VOELog.Warning("TownConversionHandler: Settlement missing WorldObjectComp_TownBonus. Skill bonuses not applied.");
                        }
                    }
                }
            }

            town.Destroy();

            Find.LetterStack.ReceiveLetter(
                "VOE_TownIncorporated".Translate(townName),
                "VOE_TownIncorporatedDesc".Translate(townName, settlement.Name, pawnCount),
                LetterDefOf.PositiveEvent,
                new LookTargets(settlement));
        }

        /// <summary>
        /// Calculates per-resource production bonuses from town pawn skills.
        /// Uses each ResourceTypeDef's associatedSkills field for dynamic mapping.
        /// </summary>
        private Dictionary<string, double> CalculateSkillBonuses(List<Pawn> pawns)
        {
            Dictionary<string, double> bonuses = new Dictionary<string, double>();
            int threshold = EmpireVOESettings.skillThreshold;
            if (threshold < 1) threshold = 1;

            foreach (ResourceTypeDef rtd in DefDatabase<ResourceTypeDef>.AllDefsListForReading)
            {
                if (rtd.associatedSkills is null || rtd.associatedSkills.Count == 0) continue;

                // Sum total skill levels across all pawns for each associated skill, then average
                double totalSkill = 0;
                foreach (SkillDef skillDef in rtd.associatedSkills)
                {
                    int skillTotal = 0;
                    foreach (Pawn pawn in pawns)
                    {
                        if (pawn.skills == null) continue;
                        SkillRecord record = pawn.skills.GetSkill(skillDef);
                        if (record != null && !record.TotallyDisabled)
                        {
                            skillTotal += record.Level;
                        }
                    }
                    totalSkill += skillTotal;
                }
                totalSkill /= rtd.associatedSkills.Count;

                int thresholds = (int)(totalSkill / threshold);
                if (thresholds < 1) continue;

                double bonus;
                if (EmpireVOESettings.scalingBonus)
                {
                    bonus = EmpireVOESettings.additiveBonus * thresholds;
                }
                else
                {
                    bonus = EmpireVOESettings.additiveBonus;
                }

                if (bonus > 0)
                {
                    bonuses[rtd.defName] = bonus;
                }
            }

            return bonuses;
        }
    }
}
