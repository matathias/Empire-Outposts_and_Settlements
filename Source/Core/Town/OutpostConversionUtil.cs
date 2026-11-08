using System.Text;
using Outposts;

namespace EmpireVOE;

/// <summary>
/// Single synchronous entry point for turning a VOE outpost into an Empire settlement.
/// Replaces the old Harmony-patch + ISettlementListener approach.
/// </summary>
public static class OutpostConversionUtil
{
    /// <summary>
    /// Set by the Specialists compat DLL when Matathias.Empire.Specialists is active.
    /// Receives the new settlement and the outpost pawns to assign as specialists/residents, and
    /// returns the pawns it did NOT place. The caller folds those leftovers into the world so no
    /// pawn is orphaned. Null when the Specialists submod is not loaded.
    /// </summary>
    public static Func<WorldSettlementFC, List<Pawn>, List<Pawn>> SpecialistsCallback;

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
        List<WorldSettlementDef> result = [];
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
            // Full tile validation (layer + impassable hilliness + allowed/blocked biomes), matching
            // what normal founding enforces. Falls back to the layer-only check if the type has no
            // SettlementTypeExtension. Adjacency is intentionally not checked — a type transition
            // happens on a tile that already hosts this settlement.
            SettlementTypeExtension typeExt = d.GetSettlementTypeExtension();
            if (typeExt is not null)
            {
                if (!typeExt.TileIsValidForTypeTransition(outpost.Tile)) continue;
            }
            else if (!d.AllowsTileLayer(outpost.Tile)) continue;
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
        List<WorldObjectDef> result = [];
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
        if (eo?.raidTarget is not null && eo.raidTarget.IsUnderAttack)
        {
            reason = "FCVOE_ConvertBusyAttacked".Translate();
            return false;
        }

        WorldObjectComp_EmpireDefensive def = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
        if (def?.defender is not null && def.defender.Busy)
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

        // Re-validate the tile against the target type (layer + hilliness + biomes) so conversion
        // can't produce a state normal founding forbids, surfacing the specific reason.
        SettlementTypeExtension typeExt = type.GetSettlementTypeExtension();
        if (typeExt is not null)
        {
            StringBuilder tileReason = new();
            if (!typeExt.TileIsValidForTypeTransition(tile, tileReason))
            {
                Messages.Message(tileReason.ToString(), MessageTypeDefOf.RejectInput);
                return false;
            }
        }

        // Cost gate (factor scales both silver and R&R resources)
        BiomeResourceDef biome = ResolveBiome(type, tile);
        int reducedSilver = (int)(ColonyUtil.GetFoundingCost(type, biome, faction) * factor);

        StringBuilder reason = new();
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

        // Pay. The silver actually taken from storage is measured so it can be refunded exactly
        // (payment modifiers may finance part of the cost from elsewhere).
        int silverBefore = PaymentUtil.GetSilver();
        if (!PaymentUtil.TryPaySilver(reducedSilver, PaymentUtil.Reason_SettlementCreation))
        {
            Messages.Message("FCNotEnoughSilverToSettle".Translate(), MessageTypeDefOf.RejectInput);
            return false;
        }
        int silverPaid = silverBefore - PaymentUtil.GetSilver();
        FoundingValidatorRegistry.NotifyFounded(tile, type, factor);

        // Create the settlement on the outpost's tile, then fold pawns + apply bonuses.
        int settlementCountBefore = faction.settlements.Count;
        WorldSettlementFC settlement;
        try
        {
            settlement = ColonyUtil.CreatePlayerColonySettlement(tile, type);
        }
        catch (Exception e)
        {
            VOELog.Error("OutpostConversionUtil: settlement creation failed: " + e);
            // If the settlement was registered before the throw it exists and works; finish the
            // conversion with it. Otherwise refund the silver and keep the outpost.
            settlement = faction.settlements.Count > settlementCountBefore ? faction.settlements[^1] : null;
            if (settlement is null)
            {
                RefundSilver(silverPaid);
                Messages.Message("FCVOE_ConvertFailed".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }
        }

        string outpostName = outpost.Name;
        if (EmpireVOESettings.convertOutpostPawns)
        {
            FoldOutpostPawns(outpost, settlement);
        }

        OutpostConversionExtension ext = outpost.def.GetModExtension<OutpostConversionExtension>();
        if (ext is not null && ext.grantsTownAdditive && EmpireVOESettings.townFlatAdditive > 0)
        {
            WorldObjectComp_OutpostBonus bonusComp = settlement.GetComponent<WorldObjectComp_OutpostBonus>();
            if (bonusComp is not null) bonusComp.SetTownFlatAdditive(EmpireVOESettings.townFlatAdditive);
        }

        // Ship the outpost's stored goods back to the player before it is destroyed
        // (containedItems were previously lost on Destroy).
        List<Thing> storedGoods = outpost.Things.ToList();
        foreach (Thing t in storedGoods)
            outpost.TakeItem(t);            // detach from the outpost's containedItems list

        if (storedGoods.Count > 0)
        {
            Letter goodsLetter = LetterMaker.MakeLetter(
                "FCVOE_OutpostGoodsShipped".Translate(outpostName),
                "FCVOE_OutpostGoodsShippedDesc".Translate(
                    outpostName, settlement.Name, DeliveryUtil.GoodsToString(storedGoods)),
                LetterDefOf.PositiveEvent);
            DeliveryEvent.CreateDeliveryEvent(storedGoods, tile, goodsLetter);
        }

        outpost.Destroy();

        // CreatePlayerColonySettlement already sends the "FCSettlementFormed" letter; a second
        // incorporation letter here just duplicated that notification.
        return true;
    }

    private static void RefundSilver(int amount)
    {
        while (amount > 0)
        {
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = Math.Min(amount, ThingDefOf.Silver.stackLimit);
            amount -= silver.stackCount;
            PaymentUtil.PlaceThing(silver);
        }
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

        if (SpecialistsCallback is not null)
        {
            Faction empire = FindFC.EmpireFaction;
            List<Pawn> leftovers;
            try
            {
                leftovers = SpecialistsCallback(settlement, pawns) ?? [];
            }
            catch (Exception e)
            {
                VOELog.Error("OutpostConversionUtil: Specialists callback failed: " + e);
                leftovers = pawns; // callback aborted mid-way — fold everyone the standard way
            }
            // VEF RemovePawn only drops the pawn from the outpost roster (no PassToWorld), so any
            // pawn the callback didn't place must be folded into the world first or it vanishes.
            foreach (Pawn pawn in leftovers)
                FoldPawnToWorld(pawn, empire);
            foreach (Pawn pawn in pawns)
                outpost.RemovePawn(pawn);
            return count;
        }

        // No Specialists submod — apply skill bonuses and transfer pawns to the Empire faction.
        Dictionary<string, double> bonuses = CalculateSkillBonuses(pawns);
        Faction empireFaction = FindFC.EmpireFaction;
        foreach (Pawn pawn in pawns)
        {
            FoldPawnToWorld(pawn, empireFaction);
            outpost.RemovePawn(pawn);
        }

        if (bonuses.Count > 0)
        {
            WorldObjectComp_OutpostBonus bonusComp = settlement.GetComponent<WorldObjectComp_OutpostBonus>();
            if (bonusComp is not null)
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
    /// Transfers a pawn to the Empire faction and registers it as a world pawn so it survives the
    /// outpost's destruction (VEF RemovePawn does not do this itself).
    /// </summary>
    private static void FoldPawnToWorld(Pawn pawn, Faction empireFaction)
    {
        if (pawn is null) return;
        pawn.SetFaction(empireFaction);
        if (!pawn.IsWorldPawn())
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
    }

    /// <summary>
    /// Per-resource production bonus from pawn skills (same per-skill-level formula as Specialists).
    /// </summary>
    private static Dictionary<string, double> CalculateSkillBonuses(List<Pawn> pawns)
    {
        Dictionary<string, double> bonuses = [];

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
        if (type.biomeResourceOverride is not null)
        {
            if (FactionCache.BiomeResourceDefSet.Contains(type.biomeResourceOverride))
                return type.biomeResourceOverride;
            return BiomeResourceDefOf.defaultBiome;
        }
        BiomeResourceDef biome = DefDatabase<BiomeResourceDef>.GetNamed(tile.Tile.PrimaryBiome.defName, false);
        return biome ?? BiomeResourceDefOf.defaultBiome;
    }
}
