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
            if (!EmpireVOESettings.TownConversionActive || !EmpireVOESettings.requireTownForSettlement) return true;

            if (tile == -1)
            {
                reason?.Append("FCSelectedInvalidTile".Translate());
                __result = false;
                return false;
            }

            // Planet layer check (mirrors WorldTileChecker)
            if (settlementdef.planetLayers.Count == 0)
            {
                if (tile.Layer != Find.WorldGrid.Surface)
                {
                    reason?.Append("FCInvalidPlanetLayer".Translate());
                    __result = false;
                    return false;
                }
            }
            else
            {
                if (!settlementdef.planetLayers.Contains(tile.Layer.Def))
                {
                    reason?.Append("FCInvalidPlanetLayer".Translate());
                    __result = false;
                    return false;
                }
            }

            // Require Outpost_Town only on surface tiles. VOE Towns don't exist off-surface,
            // so settlement types that allow non-surface layers can be placed there freely.
            if (tile.Layer == Find.WorldGrid.Surface)
            {
                Outpost_Town town = TownSettlementUtil.FindTownOnTile(tile);
                if (town is null)
                {
                    bool allowsOffSurface = settlementdef.planetLayers.Any(l => l != Find.WorldGrid.Surface.Def);
                    reason?.Append((allowsOffSurface ? "VOE_NotATownOrbitAllowed" : "VOE_NotATown").Translate());
                    __result = false;
                    return false;
                }
            }

            // Delegate biome and settlement-type-specific validation to SettlementTypeExtension
            // (same chain WorldTileChecker uses after its own planet-layer check).
            SettlementTypeExtension ext = settlementdef.GetSettlementTypeExtension();
            if (ext != null && !ext.TileIsValidForSettlement(tile, reason))
            {
                __result = false;
                return false;
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
    public class TownConversionHandler : ISettlementListener
    {
        /// <summary>
        /// Set by the Specialists compat DLL when Matathias.Empire.Specialists is active.
        /// Receives the new settlement and the list of town pawns to assign.
        /// Null when the Specialists submod is not loaded.
        /// </summary>
        public static Action<WorldSettlementFC, List<Pawn>> SpecialistsCallback;

        public void OnSettlementRemoved(WorldSettlementFC settlement) { }
        public void OnSettlementUpgraded(WorldSettlementFC settlement, int oldLevel, int newLevel) { }
        public void OnSettlementTypeChanged(WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef) { }
        public void OnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot) { }
        public void OnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot) { }

        public void OnSettlementCreated(WorldSettlementFC settlement)
        {
            if (!EmpireVOESettings.TownConversionActive || !EmpireVOESettings.requireTownForSettlement) return;

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
                    // No Specialists submod — apply skill bonuses
                    Dictionary<string, double> bonuses = CalculateSkillBonuses(townPawns);

                    // Transfer pawns to Empire faction
                    Faction empireFaction = FindFC.EmpireFaction;
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
                    if (bonuses.Count > 0)
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
        /// Uses the same per-skill-level formula as the Specialists submod.
        /// </summary>
        private Dictionary<string, double> CalculateSkillBonuses(List<Pawn> pawns)
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
                        int level = record.Level;
                        if (level >= EmpireVOESettings.skillFloor)
                            bonus += level * EmpireVOESettings.additivePerLevel;
                    }
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
