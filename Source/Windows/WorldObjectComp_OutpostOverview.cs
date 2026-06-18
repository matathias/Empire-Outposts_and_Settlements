using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_OutpostOverview : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_OutpostOverview()
        {
            compClass = typeof(WorldObjectComp_OutpostOverview);
        }
    }

    /// <summary>
    /// Settlement-window "Outposts" subtab: the management surface for every outpost link of this settlement
    /// (resource links, tax-delivery target, financing) plus passive status (encampment heal bonus, defensive
    /// aura). Replaces the scattered world-map float-menu gizmos. Pure view/controller — reads and mutates the
    /// sibling link comps via <c>parent.GetComponent</c>.
    /// </summary>
    public class WorldObjectComp_OutpostOverview : WorldObjectComp, ISettlementWindowOverview
    {
        private WorldSettlementFC uiSettlement;
        private Vector2 scroll = Vector2.zero;
        private float contentHeight = 400f;

        private WorldObjectComp_ResourceLink ResourceLink => parent.GetComponent<WorldObjectComp_ResourceLink>();
        private WorldObjectComp_OutpostLinks Links => parent.GetComponent<WorldObjectComp_OutpostLinks>();

        public void PreOpenWindow(WorldSettlementFC settlement)
        {
            uiSettlement = settlement;
            scroll = Vector2.zero;
        }

        public void OnTabSwitch() { }
        public void PostCloseWindow() { uiSettlement = null; }
        public string OverviewTabName() => "VOE_OutpostsTab".Translate();

        public bool ShouldShowOverviewTab(WorldSettlementFC settlement)
        {
            return EmpireVOESettings.ResourceLinkActive
                   || EmpireVOESettings.DeliveryActive
                   || EmpireVOESettings.FinancingActive
                   || EmpireVOESettings.EncampmentActive
                   || EmpireVOESettings.DefensiveAuraActive;
        }

        public void DrawOverviewTab(Rect boundingBox)
        {
            if (uiSettlement is null) return;

            Rect viewRect = new Rect(0f, 0f, boundingBox.width - 16f, contentHeight);
            Widgets.BeginScrollView(boundingBox, ref scroll, viewRect);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);

            if (EmpireVOESettings.ResourceLinkActive) DrawResourceLinks(ls);
            if (EmpireVOESettings.DeliveryActive) DrawDelivery(ls);
            if (EmpireVOESettings.FinancingActive) DrawFinancing(ls);
            if (EmpireVOESettings.EncampmentActive) DrawEncampment(ls);
            if (EmpireVOESettings.DefensiveAuraActive) DrawDefensiveAura(ls);

            contentHeight = ls.CurHeight;
            ls.End();
            Widgets.EndScrollView();
        }

        // --- Sections ---

        private void DrawResourceLinks(Listing_Standard ls)
        {
            WorldObjectComp_ResourceLink comp = ResourceLink;
            Header(ls, "VOE_TabSectionResourceLinks".Translate());
            if (comp is null) return;

            List<Outpost> candidates = comp.GetLinkableOutpostsInRange();
            if (candidates.Count == 0)
            {
                ls.Label("  " + "VOE_TabNoLinks".Translate());
                return;
            }

            foreach (Outpost outpost in candidates)
            {
                Rect row = ls.GetRect(OutpostLinkView.RowHeight);
                Rect labelRect = new Rect(row.x, row.y, row.width * 0.42f, row.height);
                Rect buttonRect = new Rect(row.xMax - 70f, row.y + 2f, 68f, row.height - 4f);
                Rect detailRect = new Rect(labelRect.xMax + 4f, row.y, buttonRect.x - labelRect.xMax - 8f, row.height);

                OutpostLinkView.DrawOutpostLabel(labelRect, outpost);

                bool linkedHere = comp.IsLinkedHere(outpost);
                bool linkedElsewhere = ResourceLinkUtil.IsLinkedToOther(outpost, comp);

                string feeds = "VOE_TabFeeds".Translate(ResourcesFed(outpost));
                string detail = OutpostLinkView.DistanceLabel(uiSettlement, outpost) + "  -  " + feeds;
                if (linkedHere)
                {
                    double contribution = ResourceLinkUtil.ContributionOf(outpost);
                    detail += "  (" + "VOE_TabContribution".Translate(contribution.ToString("0.##")) + ")";
                }
                else if (linkedElsewhere)
                {
                    detail += "  -  " + "VOE_TabLinkedElsewhere".Translate();
                }
                OutpostLinkView.DrawDetail(detailRect, detail);

                if (linkedElsewhere)
                {
                    Widgets.Label(buttonRect, "");
                    continue;
                }
                if (Widgets.ButtonText(buttonRect, linkedHere ? "VOE_TabUnlink".Translate() : "VOE_TabLink".Translate()))
                    comp.ToggleLink(outpost);
            }
        }

        private void DrawDelivery(Listing_Standard ls)
        {
            WorldObjectComp_OutpostLinks comp = Links;
            Header(ls, "VOE_TabSectionTaxDelivery".Translate());
            if (comp is null) return;

            Outpost current = comp.GetDeliveryOutpost();
            Rect row = ls.GetRect(OutpostLinkView.RowHeight);
            DrawTargetRow(row, current, "VOE_PlayerTaxMap".Translate(),
                comp.OpenDeliveryMenu, comp.ClearDelivery, current is object);
        }

        private void DrawFinancing(Listing_Standard ls)
        {
            WorldObjectComp_OutpostLinks comp = Links;
            Header(ls, "VOE_TabSectionFinancing".Translate());
            if (comp is null) return;

            Outpost current = comp.GetFinancingOutpost();
            Rect row = ls.GetRect(OutpostLinkView.RowHeight);
            DrawTargetRow(row, current, "VOE_TabNoLinks".Translate(),
                comp.OpenFinancingMenu, comp.ClearFinancing, current is object);
        }

        private void DrawEncampment(Listing_Standard ls)
        {
            Header(ls, "VOE_TabSectionEncampment".Translate());
            EncampmentCacheEntry entry = EncampmentCache.GetOrBuild(uiSettlement);
            if (entry is null || entry.encampments.Count == 0)
            {
                ls.Label("  " + "VOE_TabNoLinks".Translate());
                return;
            }

            foreach (EncampmentData data in entry.encampments)
            {
                Rect row = ls.GetRect(OutpostLinkView.RowHeight);
                Rect labelRect = new Rect(row.x, row.y, row.width * 0.5f, row.height);
                Rect detailRect = new Rect(labelRect.xMax + 4f, row.y, row.width - labelRect.width - 4f, row.height);
                OutpostLinkView.DrawOutpostLabel(labelRect, data.encampment);
                OutpostLinkView.DrawDetail(detailRect, OutpostLinkView.DistanceLabel(uiSettlement, data.encampment));
            }
        }

        private void DrawDefensiveAura(Listing_Standard ls)
        {
            Header(ls, "VOE_TabSectionDefensive".Translate());
            DefensiveAuraEntry entry = DefensiveAuraCache.GetOrBuild(uiSettlement);
            if (entry is null || entry.outposts.Count == 0)
            {
                ls.Label("  " + "VOE_TabNoLinks".Translate());
                return;
            }

            foreach (Outpost defensive in entry.outposts)
            {
                Rect row = ls.GetRect(OutpostLinkView.RowHeight);
                Rect labelRect = new Rect(row.x, row.y, row.width * 0.5f, row.height);
                Rect detailRect = new Rect(labelRect.xMax + 4f, row.y, row.width - labelRect.width - 4f, row.height);
                OutpostLinkView.DrawOutpostLabel(labelRect, defensive);
                OutpostLinkView.DrawDetail(detailRect, OutpostLinkView.DistanceLabel(uiSettlement, defensive));
            }
        }

        // --- Helpers ---

        private static void Header(Listing_Standard ls, string title)
        {
            ls.GapLine(6f);
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.85f, 0.85f, 0.7f);
            ls.Label(title);
            GUI.color = Color.white;
        }

        private void DrawTargetRow(Rect row, Outpost current, string noneLabel,
            System.Action openMenu, System.Action clear, bool hasTarget)
        {
            float btnW = 64f;
            Rect changeRect = new Rect(row.xMax - btnW, row.y + 2f, btnW, row.height - 4f);
            Rect clearRect = new Rect(changeRect.x - btnW - 4f, row.y + 2f, btnW, row.height - 4f);
            Rect labelRect = new Rect(row.x, row.y, clearRect.x - row.x - 6f, row.height);

            if (current is object)
            {
                OutpostLinkView.DrawOutpostLabel(labelRect, current, OutpostLinkView.DistanceLabel(uiSettlement, current));
            }
            else
            {
                TextAnchor prev = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, noneLabel);
                Text.Anchor = prev;
            }

            if (hasTarget && Widgets.ButtonText(clearRect, "VOE_TabClear".Translate()))
                clear();
            if (Widgets.ButtonText(changeRect, "VOE_TabChange".Translate()))
                openMenu();
        }

        private static string ResourcesFed(Outpost outpost)
        {
            OutpostResourceLinkExtension ext = outpost.def.GetModExtension<OutpostResourceLinkExtension>();
            if (ext?.resources is null) return "";
            return string.Join(", ", ext.resources.Select(r => r.LabelCap.ToString()).ToArray());
        }
    }
}
