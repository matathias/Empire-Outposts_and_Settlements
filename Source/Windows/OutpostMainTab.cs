using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Faction-wide "Outposts" tab in the Empire+ window. Surfaces every outpost-settlement relationship at
    /// once through two internal sub-tabs: by outpost (outposts grouped by type in a 2-column grid, each with
    /// link/unlink) and by settlement (each settlement and the outposts feeding / shielding it, one
    /// consolidated row per outpost). Names jump to the world map / open the settlement window. Registered as
    /// a singleton through the EmpireRegistry facade.
    /// </summary>
    public class OutpostMainTab : IMainTabWindowOverview
    {
        public static readonly OutpostMainTab Instance = new OutpostMainTab();

        private const float HeaderHeight = 35f;
        private const float Margin = 5f;
        private const float CellHeight = 56f;
        private const float CellGutter = 12f;

        private FactionFC uiFaction;
        private int subTab;
        private Vector2 scroll = Vector2.zero;
        private float contentHeight = 400f;

        private static readonly List<string> subTabs = new List<string>();

        /// <summary>Register/unregister the tab when its setting is toggled at runtime.</summary>
        internal static void SetRegistered(bool on)
        {
            if (on) EmpireRegistry.Register(Instance);
            else EmpireRegistry.Unregister(Instance);
        }

        public string TabName() => "VOE_OutpostsMainTab".Translate();

        public void PreOpenWindow(FactionFC faction)
        {
            uiFaction = faction;
            scroll = Vector2.zero;
        }

        public void OnTabSwitch() { }
        public void PostCloseWindow() { uiFaction = null; }

        public void DrawOverviewTab(Rect boundingBox)
        {
            if (uiFaction is null) return;

            // Header bar (no buttons), then the sub-tab row + content inset below it.
            Rect headerRect = new Rect(boundingBox.x + Margin, boundingBox.y + Margin, boundingBox.width - Margin * 2f, HeaderHeight);
            DrawHeader(headerRect);

            subTabs.Clear();
            subTabs.Add("VOE_MainTabByOutpost".Translate());
            subTabs.Add("VOE_MainTabBySettlement".Translate());

            float bodyY = headerRect.yMax + Margin;
            Rect subtabBox = new Rect(boundingBox.x + Margin, bodyY, boundingBox.width - Margin * 2f, boundingBox.yMax - bodyY - Margin);
            if (subtabBox.height <= 0f) return;

            subTab = UIUtil.DrawTabRowButtonFlat(subtabBox, subTabs, subTab, out Rect contentRect);
            Rect inner = contentRect.ContractedBy(2f);

            Rect viewRect = ScrollUtil.BeginScrollView(inner, ref scroll, contentHeight);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);
            ls.maxOneColumn = true;

            if (subTab == 0) DrawByOutpost(ls);
            else DrawBySettlement(ls);

            contentHeight = ls.CurHeight;
            ls.End();
            ScrollUtil.EndScrollView();
        }

        /// <summary>"&lt;faction&gt;: Outposts" title bar with the Empire emblem (no header-level buttons).</summary>
        private void DrawHeader(Rect rect)
        {
            Widgets.DrawHighlight(rect);

            float x = rect.x + 5f;
            Texture2D icon = FindFC.FactionComp?.factionIcon;
            if (icon != null)
            {
                float iconSz = rect.height - 6f;
                GUI.DrawTexture(new Rect(x, rect.y + 3f, iconSz, iconSz), icon);
                x += iconSz + 6f;
            }

            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, rect.y, rect.xMax - x - 5f, rect.height), uiFaction.name + ": " + TabName());
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
        }

        // --- By outpost (grouped by type, 2-column grid) ---

        private void DrawByOutpost(Listing_Standard ls)
        {
            List<Outpost> outposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>().ToList();
            if (outposts.Count == 0)
            {
                ls.Label("  " + "VOE_MainTabNoOutposts".Translate());
                return;
            }

            foreach (var group in outposts.GroupBy(o => o.def))
            {
                OutpostLinkView.DrawSectionHeader(ls, group.Key.LabelCap);

                List<Outpost> items = group.ToList();
                for (int i = 0; i < items.Count; i += 2)
                {
                    Rect row = ls.GetRect(CellHeight);
                    float half = (row.width - CellGutter) / 2f;
                    DrawOutpostCell(new Rect(row.x, row.y, half, row.height), items[i]);
                    if (i + 1 < items.Count)
                        DrawOutpostCell(new Rect(row.x + half + CellGutter, row.y, half, row.height), items[i + 1]);
                }
            }
        }

        /// <summary>
        /// Dark card with a full-height type icon on the left (jump to map). Right of the icon: outpost name +
        /// link/unlink on line 1; role text (left) and the linked settlement(s) under the button on line 2.
        /// </summary>
        private void DrawOutpostCell(Rect cell, Outpost outpost)
        {
            const float pad = 4f;
            const float btnW = 62f;

            Rect card = cell.ContractedBy(2f);
            Widgets.DrawBoxSolid(card, ColorUtil.Gray1);

            // Full-height type icon (the two-row grouping cue).
            float iconSz = card.height - pad * 2f;
            OutpostLinkView.DrawOutpostIcon(new Rect(card.x + pad, card.y + pad, iconSz, iconSz), outpost, jumpOnClick: true);

            float contentX = card.x + pad + iconSz + 6f;
            float contentW = card.xMax - pad - contentX;
            float lineH = (card.height - pad * 2f) / 2f;
            float line1Y = card.y + pad;
            float line2Y = line1Y + lineH;

            bool linkable = ResourceLinkUtil.IsLinkable(outpost);

            // Line 1: name (left) + Link/Unlink (right).
            float nameW = linkable ? contentW - btnW - 6f : contentW;
            OutpostLinkView.DrawOutpostName(new Rect(contentX, line1Y, nameW, lineH), outpost, jumpOnClick: true);
            if (linkable)
            {
                Rect btnRect = new Rect(card.xMax - pad - btnW, line1Y + 1f, btnW, lineH - 2f);
                bool linked = LinkedSettlement(outpost) is object;
                if (UIUtil.ButtonFlat(btnRect, linked ? "VOE_TabUnlink".Translate() : "VOE_TabLink".Translate()))
                    ToggleOutpostLink(outpost);
            }

            // Line 2: role text (left) + settlement(s) under the button (right).
            List<OutpostRelation> relations = OutpostRelations(outpost);
            float settleW = Mathf.Max(btnW, contentW * 0.42f);
            Rect roleRect = new Rect(contentX, line2Y, contentW - settleW - 6f, lineH);
            Rect settleRect = new Rect(card.xMax - pad - settleW, line2Y, settleW, lineH);

            string roleText = relations.Count > 0
                ? string.Join("  -  ", relations.Select(r => r.role).ToArray())
                : (string)"VOE_MainTabOutpostUnlinked".Translate();
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(roleRect, Text.ClampTextWithEllipsis(roleRect, roleText));
            GUI.color = prevColor;
            Text.Anchor = prevAnchor;

            // Distinct related settlements, laid out right-to-left so the nearest sits under the button.
            List<WorldSettlementFC> settlements = new List<WorldSettlementFC>();
            foreach (OutpostRelation r in relations)
                if (!settlements.Contains(r.settlement)) settlements.Add(r.settlement);
            float rx = settleRect.xMax;
            foreach (WorldSettlementFC st in settlements)
            {
                if (rx <= settleRect.x) break;
                float w = Mathf.Min(Text.CalcSize(st.Name).x + 4f, rx - settleRect.x);
                OutpostLinkView.DrawClickableSettlementName(new Rect(rx - w, settleRect.y, w, settleRect.height),
                    st, AccentUtil.GetSettlementAccent(st), TextAnchor.MiddleRight);
                rx -= w + 6f;
            }
        }

        /// <summary>The settlement an outpost is currently resource-linked to, or null.</summary>
        private WorldSettlementFC LinkedSettlement(Outpost outpost)
        {
            if (uiFaction is null) return null;
            foreach (WorldSettlementFC s in uiFaction.settlements)
            {
                WorldObjectComp_ResourceLink rl = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (rl is object && rl.IsLinkedHere(outpost)) return s;
            }
            return null;
        }

        /// <summary>Unlinks if already linked; otherwise opens a float menu of in-range settlements to link to.</summary>
        private void ToggleOutpostLink(Outpost outpost)
        {
            WorldSettlementFC linkedTo = LinkedSettlement(outpost);
            if (linkedTo is object)
            {
                linkedTo.GetComponent<WorldObjectComp_ResourceLink>()?.ToggleLink(outpost);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (WorldSettlementFC s in uiFaction.settlements)
            {
                if (Find.WorldGrid.ApproxDistanceInTiles(outpost.Tile, s.Tile) > EmpireVOESettings.resourceLinkRange) continue;
                if (s.GetComponent<WorldObjectComp_ResourceLink>() is null) continue;
                WorldSettlementFC captured = s;
                options.Add(new FloatMenuOption(s.Name,
                    delegate { captured.GetComponent<WorldObjectComp_ResourceLink>().ToggleLink(outpost); }));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("VOE_NoSettlementsInRange".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>The settlement relationships an outpost participates in (settlement objects + role text).</summary>
        private List<OutpostRelation> OutpostRelations(Outpost outpost)
        {
            List<OutpostRelation> relations = new List<OutpostRelation>();
            foreach (WorldSettlementFC s in uiFaction.settlements)
            {
                WorldObjectComp_ResourceLink rl = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (rl is object && rl.IsLinkedHere(outpost))
                    relations.Add(new OutpostRelation(s, "VOE_TabFeeds".Translate(ResourcesFed(outpost))));

                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                if (links is object)
                {
                    if (links.GetDeliveryOutpost() == outpost)
                        relations.Add(new OutpostRelation(s, "VOE_TabSectionTaxDelivery".Translate()));
                    if (links.GetFinancingOutpost() == outpost)
                        relations.Add(new OutpostRelation(s, "VOE_TabSectionFinancing".Translate()));
                }
            }
            return relations;
        }

        // --- By settlement (one consolidated row per outpost) ---

        private void DrawBySettlement(Listing_Standard ls)
        {
            if (!uiFaction.settlements.Any())
            {
                ls.Label("  " + "VOE_MainTabNoSettlements".Translate());
                return;
            }

            foreach (WorldSettlementFC s in uiFaction.settlements)
            {
                OutpostLinkView.DrawSettlementHeader(ls, s);

                // Collect each related outpost once, gathering all the roles it plays for this settlement.
                List<Outpost> order = new List<Outpost>();
                Dictionary<Outpost, List<string>> roles = new Dictionary<Outpost, List<string>>();
                void AddRole(Outpost o, string role)
                {
                    if (o is null || o.Destroyed) return;
                    if (!roles.TryGetValue(o, out List<string> list)) { list = new List<string>(); roles[o] = list; order.Add(o); }
                    list.Add(role);
                }

                WorldObjectComp_ResourceLink rl = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (rl?.linkedOutposts is object)
                    foreach (Outpost o in rl.linkedOutposts)
                        AddRole(o, "VOE_TabFeeds".Translate(ResourcesFed(o)));

                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                AddRole(links?.GetDeliveryOutpost(), "VOE_TabSectionTaxDelivery".Translate());
                AddRole(links?.GetFinancingOutpost(), "VOE_TabSectionFinancing".Translate());

                if (order.Count == 0)
                {
                    ls.Label("  " + "VOE_TabNoLinks".Translate());
                    continue;
                }

                foreach (Outpost o in order)
                    DrawSettlementOutpostRow(ls, s, o, roles[o], rl);
            }
        }

        private void DrawSettlementOutpostRow(Listing_Standard ls, WorldSettlementFC settlement, Outpost outpost,
            List<string> roleTags, WorldObjectComp_ResourceLink rl)
        {
            Rect row = ls.GetRect(OutpostLinkView.RowHeight);
            bool canUnlink = rl is object && rl.IsLinkedHere(outpost);
            float rightEdge = canUnlink ? row.xMax - 70f : row.xMax;

            Rect labelRect = new Rect(row.x + 8f, row.y, (rightEdge - row.x) * 0.4f, row.height);
            Rect detailRect = new Rect(labelRect.xMax + 4f, row.y, rightEdge - labelRect.xMax - 8f, row.height);

            OutpostLinkView.DrawOutpostLabel(labelRect, outpost, jumpOnClick: true);
            string detail = string.Join("  -  ", roleTags.ToArray()) + "   " + OutpostLinkView.DistanceLabel(settlement, outpost);
            OutpostLinkView.DrawDetail(detailRect, detail);

            if (canUnlink)
            {
                Rect btnRect = new Rect(row.xMax - 66f, row.y + 2f, 64f, row.height - 4f);
                if (UIUtil.ButtonFlat(btnRect, "VOE_TabUnlink".Translate()))
                    rl.ToggleLink(outpost);
            }
        }

        private static string ResourcesFed(Outpost outpost)
        {
            OutpostResourceLinkExtension ext = outpost.def.GetModExtension<OutpostResourceLinkExtension>();
            if (ext?.resources is null) return "";
            return string.Join(", ", ext.resources.Select(r => r.LabelCap.ToString()).ToArray());
        }
    }
}
