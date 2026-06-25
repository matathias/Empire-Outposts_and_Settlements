using System.Collections.Generic;
using FactionColonies;
using FactionColonies.SupplyChain;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EmpireVOE.SupplyChain
{
    /// <summary>
    /// Shared renderer for the Routes &amp; Resources conversion-cost preview, used by both the
    /// Found-screen companion (<see cref="FCWindow_VOEFoundingConversionCost"/>) and the conversion
    /// type-picker companion (<see cref="FCWindow_VOEPickerConversionCost"/>).
    ///
    /// The displayed cost is the R&amp;R founding cost reduced by <c>reducedFoundingCostFactor</c> — the
    /// same factor <see cref="OutpostConversionUtil.ConvertOutpost"/> charges at conversion time — and
    /// is checked against the stockpile that conversion would actually draw from (faction stockpile in
    /// Simple mode, nearest source in Complex mode). No source picker: a preview selection wouldn't
    /// bind to the later conversion, so showing the nearest/faction source is the honest estimate.
    /// </summary>
    public static class VOEConversionCostView
    {
        private const float RowHeight = 22f;
        private const float TitleHeight = 30f;
        private const float Padding = 8f;

        /// <summary>
        /// True if a cost preview is meaningful for this type: R&amp;R is loaded, the type has founding
        /// resource costs, and the empire is at/above R&amp;R's free-founding threshold (below which
        /// founding — and therefore conversion — is free).
        /// </summary>
        public static bool ShouldShow(WorldSettlementDef type)
        {
            if (type is null) return false;
            if (SupplyChainCache.Comp is null) return false;

            List<FCResourceCost> costs = FoundingCostUtil.GetFoundingResourceCosts(type);
            if (costs is null || costs.Count == 0) return false;

            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return false;
            int count = faction.settlements.Count + faction.settlementCaravansList.Count;
            return count >= SupplyChainSettings.freeSettlementThreshold;
        }

        /// <summary>
        /// Draws the cost preview into <paramref name="inRect"/> starting at y=0 and returns the
        /// content height used (so the caller can size its window).
        /// </summary>
        public static float Draw(Rect inRect, PlanetTile tile, WorldSettlementDef type)
        {
            WorldComponent_SupplyChain wc = SupplyChainCache.Comp;
            List<FCResourceCost> costs = FoundingCostUtil.GetFoundingResourceCosts(type);
            if (wc is null || costs is null || costs.Count == 0) return 0f;

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float curY = 0f;

            // Title
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect titleRect = new Rect(0, curY, inRect.width, TitleHeight);
            UIUtil.DrawColoredHighlight(titleRect, type.accentColor ?? Color.white);
            Widgets.Label(titleRect, "FCVOE_ConversionCostTitle".Translate());
            curY += TitleHeight + 5f;

            // "Paid at conversion" note
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            string note = "FCVOE_ConversionCostNote".Translate();
            float noteH = Text.CalcHeight(note, inRect.width);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(0, curY, inRect.width, noteH), note);
            GUI.color = Color.white;
            curY += noteH + Padding;

            Widgets.DrawLineHorizontal(0, curY, inRect.width);
            curY += Padding;

            // Resource rows: reduced cost vs effective-source availability
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            double distMult = FoundingCostUtil.ComputeDistanceMultiplier(tile);
            float factor = EmpireVOESettings.reducedFoundingCostFactor;
            IStockpile stockpile = GetStockpile(wc, tile);

            for (int i = 0; i < costs.Count; i++)
            {
                FCResourceCost entry = costs[i];
                double needed = FormulaUtil.ResourceCost(entry.amount, distMult) * factor;
                double have = stockpile?.GetAmount(entry.resource) ?? 0;
                bool sufficient = have >= needed;

                Rect iconRect = new Rect(2f, curY, RowHeight, RowHeight);
                Widgets.ButtonImage(iconRect, entry.resource.Icon);
                GUI.color = sufficient ? AccentUtil.Income : AccentUtil.Expense;
                Widgets.Label(new Rect(iconRect.xMax + Padding, curY, inRect.width * 0.45f, RowHeight), entry.resource.LabelCap);

                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(inRect.width * 0.45f, curY, inRect.width * 0.55f - Padding, RowHeight),
                    have.ToString("F0") + " / " + needed.ToString("F0"));
                Text.Anchor = TextAnchor.MiddleLeft;

                GUI.color = Color.white;
                curY += RowHeight;
            }

            curY += Padding;

            // Distance multiplier line
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0, curY, inRect.width, RowHeight),
                "FCVOE_ConversionCostDistanceMult".Translate(distMult.ToString("F1")));
            GUI.color = Color.white;
            curY += RowHeight + Padding;

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
            return curY;
        }

        private static IStockpile GetStockpile(WorldComponent_SupplyChain wc, PlanetTile tile)
        {
            if (wc.Mode == SupplyChainMode.Simple)
                return wc.Stockpile;

            WorldSettlementFC source = FoundingCostUtil.FindNearestSettlement(tile);
            if (source is null) return null;

            WorldObjectComp_SupplyChain comp = SupplyChainCache.GetSettlementComp(source);
            return comp?.GetStockpile();
        }
    }
}
