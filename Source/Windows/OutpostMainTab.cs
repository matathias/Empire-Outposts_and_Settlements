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
    /// once through two internal sub-tabs: by outpost (each outpost and what it's linked to / affecting) and
    /// by settlement (each settlement and the outposts feeding / shielding it). Read-only — management lives in
    /// the per-settlement Outposts subtab. Registered as a singleton through the EmpireRegistry facade.
    /// </summary>
    public class OutpostMainTab : IMainTabWindowOverview
    {
        public static readonly OutpostMainTab Instance = new OutpostMainTab();

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

            subTabs.Clear();
            subTabs.Add("VOE_MainTabByOutpost".Translate());
            subTabs.Add("VOE_MainTabBySettlement".Translate());

            subTab = UIUtil.DrawTabRowButtonFlat(boundingBox, subTabs, subTab, out Rect contentRect);
            Rect inner = contentRect.ContractedBy(8f);

            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, contentHeight);
            Widgets.BeginScrollView(inner, ref scroll, viewRect);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);

            if (subTab == 0) DrawByOutpost(ls);
            else DrawBySettlement(ls);

            contentHeight = ls.CurHeight;
            ls.End();
            Widgets.EndScrollView();
        }

        // --- By outpost ---

        private void DrawByOutpost(Listing_Standard ls)
        {
            List<Outpost> outposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>().ToList();
            if (outposts.Count == 0)
            {
                ls.Label("  " + "VOE_MainTabNoOutposts".Translate());
                return;
            }

            foreach (Outpost outpost in outposts)
            {
                Rect row = ls.GetRect(OutpostLinkView.RowHeight);
                Rect labelRect = new Rect(row.x, row.y, row.width * 0.4f, row.height);
                Rect detailRect = new Rect(labelRect.xMax + 4f, row.y, row.width - labelRect.width - 4f, row.height);

                OutpostLinkView.DrawOutpostLabel(labelRect, outpost);
                OutpostLinkView.DrawDetail(detailRect, OutpostRelationships(outpost));
            }
        }

        /// <summary>Summarizes which settlements an outpost is linked to / affecting.</summary>
        private string OutpostRelationships(Outpost outpost)
        {
            List<string> parts = new List<string>();
            foreach (WorldSettlementFC s in uiFaction.settlements)
            {
                WorldObjectComp_ResourceLink rl = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (rl?.linkedOutposts is object && rl.linkedOutposts.Contains(outpost))
                    parts.Add("VOE_TabFeeds".Translate(s.Name));

                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                if (links is object)
                {
                    if (links.GetDeliveryOutpost() == outpost)
                        parts.Add("VOE_ReceivingTaxes".Translate(s.Name));
                    if (links.GetFinancingOutpost() == outpost)
                        parts.Add("VOE_FinancingFor".Translate(s.Name));
                }
            }
            return parts.Count > 0 ? string.Join("   ", parts.ToArray()) : (string)"VOE_MainTabOutpostUnlinked".Translate();
        }

        // --- By settlement ---

        private void DrawBySettlement(Listing_Standard ls)
        {
            if (!uiFaction.settlements.Any())
            {
                ls.Label("  " + "VOE_MainTabNoSettlements".Translate());
                return;
            }

            foreach (WorldSettlementFC s in uiFaction.settlements)
            {
                ls.GapLine(6f);
                Text.Font = GameFont.Small;
                GUI.color = new Color(0.85f, 0.85f, 0.7f);
                ls.Label(s.Name);
                GUI.color = Color.white;

                bool any = false;

                WorldObjectComp_ResourceLink rl = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (rl?.linkedOutposts is object)
                {
                    foreach (Outpost o in rl.linkedOutposts.Where(o => o is object && !o.Destroyed))
                    {
                        any = true;
                        DrawSettlementOutpostRow(ls, s, o, "VOE_TabFeeds".Translate(ResourcesFed(o)));
                    }
                }

                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                Outpost delivery = links?.GetDeliveryOutpost();
                if (delivery is object) { any = true; DrawSettlementOutpostRow(ls, s, delivery, "VOE_TabSectionTaxDelivery".Translate()); }
                Outpost financing = links?.GetFinancingOutpost();
                if (financing is object) { any = true; DrawSettlementOutpostRow(ls, s, financing, "VOE_TabSectionFinancing".Translate()); }

                if (!any)
                    ls.Label("  " + "VOE_TabNoLinks".Translate());
            }
        }

        private void DrawSettlementOutpostRow(Listing_Standard ls, WorldSettlementFC settlement, Outpost outpost, string detail)
        {
            Rect row = ls.GetRect(OutpostLinkView.RowHeight);
            Rect labelRect = new Rect(row.x + 8f, row.y, row.width * 0.4f, row.height);
            Rect detailRect = new Rect(labelRect.xMax + 4f, row.y, row.width - labelRect.width - 12f, row.height);
            OutpostLinkView.DrawOutpostLabel(labelRect, outpost);
            OutpostLinkView.DrawDetail(detailRect, detail + "   " + OutpostLinkView.DistanceLabel(settlement, outpost));
        }

        private static string ResourcesFed(Outpost outpost)
        {
            OutpostResourceLinkExtension ext = outpost.def.GetModExtension<OutpostResourceLinkExtension>();
            if (ext?.resources is null) return "";
            return string.Join(", ", ext.resources.Select(r => r.LabelCap.ToString()).ToArray());
        }
    }
}
