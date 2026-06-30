using System;
using System.Reflection;
using EmpireVOE;
using FactionColonies;
using HarmonyLib;
using Outposts;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VOEPowerGrid;

namespace EmpireVOE.PowerGrid
{
    /// <summary>
    /// Resource-link contribution for VOE power outposts. The base skill-sum makes no sense for a power
    /// generator, so this override turns the outpost's actual <c>ProducedPower</c> (watts) into an RTD_Power
    /// per-worker contribution, reusing Empire's own watts&lt;-&gt;units factor (x100) and VOE's distance falloff.
    /// <para>Per the Empire design rule, the value is a worker-count-independent per-worker figure (it never
    /// references the settlement's assigned-worker count). Power outposts out-produce settlements heavily, so
    /// the raw figure is scaled down by twice the outpost's pawn count and the player-facing
    /// <c>powerConversionMultiplier</c> setting.</para>
    /// </summary>
    public class OutpostResourceLinkExtension_Power : OutpostResourceLinkExtension
    {
        public override double Contribution(Outpost outpost, WorldSettlementFC settlement)
        {
            Outpost_PowerGrid pg = outpost as Outpost_PowerGrid;
            if (pg is null || settlement is null) return 0;

            int pawns = Math.Max(1, outpost.PawnCount);
            // ProducedPower is watts; /100 mirrors Empire's RTD_Power CreatePool(production*100). Divide by
            // twice the pawn count (the power mod has no per-pawn figure) and apply the player tuning knob.
            double units = (pg.ProducedPower / 100.0) / (2.0 * pawns);
            units *= EmpireVOESettings.powerConversionMultiplier;

            // VOE's exact distance falloff, using its live PowerLossPerTiles setting (outpost -> settlement).
            int per = VOEPowerGrid_Mod.Settings.PowerLossPerTiles;
            float dist = Find.WorldGrid.TraversalDistanceBetween(outpost.Tile, settlement.Tile);
            float loss = per > 0 ? Mathf.Clamp01((dist / (float)per - 1f) / 100f) : 0f;

            return units * (1.0 - loss);
        }

        /// <summary>
        /// A settlement may link to a power outpost only when it is within the outpost's transmission range
        /// (<c>PowerNetworkRange</c> = transmission towers x the grid mod's per-tower range). This replaces the
        /// flat global resourceLinkRange for power outposts, matching VOE's own outlet gating.
        /// </summary>
        public override bool InLinkRange(Outpost outpost, PlanetTile settlementTile)
        {
            Outpost_PowerGrid pg = outpost as Outpost_PowerGrid;
            if (pg is null) return false;
            return Find.WorldGrid.TraversalDistanceBetween(pg.Tile, settlementTile) <= pg.PowerNetworkRange;
        }
    }

    /// <summary>
    /// Loaded only when MrHydralisk.VOEPowerGrid is active (via LoadFolders.xml). Suppresses a power outpost's
    /// normal colony power delivery while it is resource-linked, and refreshes that suppression immediately
    /// when the link toggles.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VOEPowerGridCompat
    {
        static VOEPowerGridCompat()
        {
            new Harmony("com.Matathias.EmpireVOE.PowerGrid").PatchAll(Assembly.GetExecutingAssembly());

            // When a power outpost is linked/unlinked, re-run its outlet's power calc so the suppression
            // postfix applies (link) or lifts (unlink) immediately rather than waiting for the next VOE update.
            ResourceLinkUtil.LinkChanged += OnLinkChanged;

            VOELog.MessageForce("EmpireVOE - VOE Power Outposts resource-link compat loaded.");
        }

        private static void OnLinkChanged(Outpost outpost)
        {
            Outpost_PowerGrid pg = outpost as Outpost_PowerGrid;
            if (pg?.Outlet is null) return;
            pg.Outlet.GetComp<CompPowerGridOutlet>()?.UpdateDesiredPowerOutput();
        }
    }

    /// <summary>
    /// While a power outpost is resource-linked, its colony outlet delivers no power (the output is redirected
    /// to the settlement's RTD_Power instead). Non-destructive: the connection is preserved, and unlinking
    /// lets the next <c>UpdateDesiredPowerOutput</c> restore normal output.
    /// </summary>
    [HarmonyPatch(typeof(CompPowerGridOutlet), nameof(CompPowerGridOutlet.UpdateDesiredPowerOutput))]
    public static class Patch_CompPowerGridOutlet_Suppress
    {
        private static void Postfix(CompPowerGridOutlet __instance)
        {
            Outpost_PowerGrid pg = __instance.OutpostPowerGrid;
            if (pg != null && ResourceLinkUtil.IsLinked(pg))
                __instance.PowerOutput = 0f;
        }
    }
}
