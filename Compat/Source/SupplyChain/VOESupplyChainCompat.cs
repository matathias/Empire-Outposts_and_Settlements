using System.Reflection;
using FactionColonies;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE.SupplyChain
{
    /// <summary>
    /// Routes &amp; Resources (Matathias.Empire.SupplyChain) integration for EmpireVOE. Adds conversion-cost
    /// previews to the Found screen and the outpost conversion type-picker, and suppresses R&amp;R's own
    /// founding-cost window while founding is restricted to outposts (its full, "paid now" cost would be
    /// misleading there).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VOESupplyChainCompat
    {
        static VOESupplyChainCompat()
        {
            new Harmony("com.Matathias.EmpireVOE.SupplyChain").PatchAll(Assembly.GetExecutingAssembly());

            // Requirement 1: manage the Found-screen conversion-cost companion via the base hook.
            FoundingScreenHooks.SelectionChanged += OnSelectionChanged;

            // Requirement 2: dock a cost-preview companion beside the conversion type-picker.
            OutpostConversionUtil.ConversionCostCompanionOpener = OpenPickerCompanion;

            VOELog.MessageForce("EmpireVOE - Routes & Resources compat loaded.");
        }

        internal static bool RestrictionActive =>
            EmpireVOESettings.OutpostConversionActive && EmpireVOESettings.requireOutpostForSettlement;

        private static void OnSelectionChanged(PlanetTile tile, WorldSettlementDef type)
        {
            if (RestrictionActive && VOEConversionCostView.ShouldShow(type))
                FCWindow_VOEFoundingConversionCost.Refresh();
            else
                FCWindow_VOEFoundingConversionCost.TryClose();
        }

        private static void OpenPickerCompanion(Outposts.Outpost outpost)
        {
            if (outpost is null) return;
            Find.WindowStack.Add(new FCWindow_VOEPickerConversionCost(outpost));
        }
    }
}
