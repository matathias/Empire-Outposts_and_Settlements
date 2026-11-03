using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VOE;

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
        public string OverviewTabName() => "FCVOE_OutpostsTab".Translate();

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

            Rect viewRect = ScrollUtil.BeginScrollView(boundingBox, ref scroll, contentHeight);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);
            ls.maxOneColumn = true;

            if (EmpireVOESettings.ResourceLinkActive) DrawResourceLinks(ls);
            if (EmpireVOESettings.DeliveryActive) DrawDelivery(ls);
            if (EmpireVOESettings.FinancingActive) DrawFinancing(ls);
            if (EmpireVOESettings.EncampmentActive) DrawEncampment(ls);
            if (EmpireVOESettings.DefensiveAuraActive) DrawDefensiveAura(ls);

            contentHeight = ls.CurHeight;
            ls.End();
            ScrollUtil.EndScrollView();
        }

        // --- Sections ---

        private void DrawResourceLinks(Listing_Standard ls)
        {
            WorldObjectComp_ResourceLink comp = ResourceLink;
            Header(ls, "FCVOE_TabSectionResourceLinks".Translate());
            if (comp is null) return;

            List<Outpost> candidates = comp.GetLinkableOutpostsInRange();
            if (candidates.Count == 0)
            {
                ls.Label("  " + "FCVOE_TabNoLinks".Translate());
                return;
            }

            int index = 0;
            foreach (Outpost outpost in candidates)
            {
                Rect row = ls.GetRect(OutpostLinkView.RowHeight);
                OutpostLinkView.DrawRowAlt(row, index++);
                Rect labelRect = new Rect(row.x, row.y, row.width * 0.42f, row.height);
                Rect buttonRect = new Rect(row.xMax - 70f, row.y + 2f, 68f, row.height - 4f);

                OutpostLinkView.DrawOutpostLabel(labelRect, outpost);

                bool linkedHere = comp.IsLinkedHere(outpost);
                bool linkedElsewhere = ResourceLinkUtil.IsLinkedToOther(outpost, comp);

                // When linked elsewhere the Link/Unlink button is hidden, so let the row fill that space.
                float detailRight = linkedElsewhere ? row.xMax - 4f : buttonRect.x - 8f;
                float detailX = labelRect.xMax + 4f;

                // The production bonus now lives inside the resource badge; the only status text is
                // "linked elsewhere" — give it generous width so it never clips.
                string status = linkedElsewhere ? (string)"FCVOE_TabLinkedElsewhere".Translate() : null;
                float statusW = status.NullOrEmpty() ? 0f : Mathf.Min(Text.CalcSize(status).x + 22f, (detailRight - detailX) * 0.6f);
                OutpostLinkView.DrawBadgeRow(new Rect(detailX, row.y, detailRight - detailX - statusW, row.height),
                    OutpostLinkView.ResourceBadges(outpost, uiSettlement));
                if (!status.NullOrEmpty())
                    OutpostLinkView.DrawDetail(new Rect(detailRight - statusW, row.y, statusW, row.height), status);

                if (linkedElsewhere)
                    continue;
                if (UIUtil.ButtonFlat(buttonRect, linkedHere ? "FCVOE_TabUnlink".Translate() : "FCVOE_TabLink".Translate()))
                    comp.ToggleLink(outpost);
            }
        }

        private void DrawDelivery(Listing_Standard ls)
        {
            WorldObjectComp_OutpostLinks comp = Links;
            Header(ls, "FCVOE_TabSectionTaxDelivery".Translate());
            if (comp is null) return;

            Outpost current = comp.GetDeliveryOutpost();
            Rect row = ls.GetRect(OutpostLinkView.RowHeight);
            DrawTargetRow(row, current, "FCVOE_PlayerTaxMap".Translate(),
                comp.OpenDeliveryMenu, comp.ClearDelivery, current is object);
        }

        private void DrawFinancing(Listing_Standard ls)
        {
            WorldObjectComp_OutpostLinks comp = Links;
            Header(ls, "FCVOE_TabSectionFinancing".Translate());
            if (comp is null) return;

            Outpost current = comp.GetFinancingOutpost();
            Rect row = ls.GetRect(OutpostLinkView.RowHeight);
            DrawTargetRow(row, current, "FCVOE_TabNoLinks".Translate(),
                comp.OpenFinancingMenu, comp.ClearFinancing, current is object);
        }

        private void DrawEncampment(Listing_Standard ls)
        {
            Header(ls, "FCVOE_TabSectionEncampment".Translate());
            EncampmentCacheEntry entry = EncampmentCache.GetOrBuild(uiSettlement);
            if (entry is null || entry.encampments.Count == 0)
            {
                ls.Label("  " + "FCVOE_TabNoLinks".Translate());
                return;
            }

            int index = 0;
            foreach (EncampmentData data in entry.encampments)
            {
                Rect row = ls.GetRect(OutpostLinkView.RowHeight);
                OutpostLinkView.DrawRowAlt(row, index++);
                OutpostLinkView.DrawOutpostLabel(row, data.encampment);
            }
        }

        private void DrawDefensiveAura(Listing_Standard ls)
        {
            DefensiveAuraEntry entry = DefensiveAuraCache.GetOrBuild(uiSettlement);
            string header = "FCVOE_TabSectionDefensive".Translate();
            if (entry is object && entry.outposts.Count > 0)
                header += "  (" + "FCVOE_AuraLevelValue".Translate(entry.militaryLevelBonus.ToString("0.#")) + ")";
            Header(ls, header);
            if (entry is null || entry.outposts.Count == 0)
            {
                ls.Label("  " + "FCVOE_TabNoLinks".Translate());
                return;
            }

            int index = 0;
            foreach (Outpost_Defensive defensive in entry.outposts)
            {
                Rect row = ls.GetRect(OutpostLinkView.RowHeight);
                OutpostLinkView.DrawRowAlt(row, index++);
                Rect labelRect = new Rect(row.x, row.y, row.width * 0.5f, row.height);
                Rect detailRect = new Rect(labelRect.xMax + 4f, row.y, row.width - labelRect.width - 4f, row.height);
                OutpostLinkView.DrawOutpostLabel(labelRect, defensive);
                OutpostLinkView.DrawDetail(detailRect,
                    "FCVOE_AuraLevelValue".Translate(DefensiveAuraEntry.OutpostBonus(defensive).ToString("0.#")));
            }
        }

        // --- Helpers ---

        private static void Header(Listing_Standard ls, string title)
        {
            OutpostLinkView.DrawSectionHeader(ls, title);
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
                OutpostLinkView.DrawOutpostLabel(labelRect, current);
            }
            else
            {
                TextAnchor prev = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, noneLabel);
                Text.Anchor = prev;
            }

            if (hasTarget && UIUtil.ButtonFlat(clearRect, "FCVOE_TabClear".Translate()))
                clear();
            if (UIUtil.ButtonFlat(changeRect, "FCVOE_TabChange".Translate()))
                openMenu();
        }
    }
}
