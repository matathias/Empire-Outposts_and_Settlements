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
        private const float CellHeight = 48f;
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
                Outpost rep = group.First();
                OutpostLinkView.DrawSectionHeader(ls, group.Key.LabelCap, rep.ExpandingIcon, rep.ExpandingIconColor);

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

        /// <summary>Two-line cell: icon+name (jump to map) and link/unlink on top, relationships below.</summary>
        private void DrawOutpostCell(Rect cell, Outpost outpost)
        {
            float lineH = cell.height / 2f;
            Rect line1 = new Rect(cell.x, cell.y, cell.width, lineH);

            bool linkable = ResourceLinkUtil.IsLinkable(outpost);
            Rect labelRect = new Rect(line1.x, line1.y, linkable ? line1.width - 66f : line1.width, line1.height);
            OutpostLinkView.DrawOutpostLabel(labelRect, outpost, jumpOnClick: true);

            if (linkable)
            {
                Rect btnRect = new Rect(line1.xMax - 64f, line1.y + 1f, 62f, line1.height - 2f);
                bool linked = LinkedSettlement(outpost) is object;
                if (UIUtil.ButtonFlat(btnRect, linked ? "VOE_TabUnlink".Translate() : "VOE_TabLink".Translate()))
                    ToggleOutpostLink(outpost);
            }

            Rect line2 = new Rect(cell.x + 16f, cell.y + lineH, cell.width - 16f, lineH);
            OutpostLinkView.DrawOutpostRelations(line2, OutpostRelations(outpost));
        }

        /// <summary>The settlement an outpost is currently resource-linked to, or null.</summary>
        private WorldSettlementFC LinkedSettlement(Outpost outpost)
        {
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
