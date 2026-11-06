using Outposts;
using UnityEngine;

namespace EmpireVOE;

/// <summary>
/// Faction-wide "Outposts" tab in the Empire+ window. Surfaces every outpost-settlement relationship at
/// once through two internal sub-tabs: by outpost (outposts grouped by type in a 2-column grid, each with
/// link/unlink) and by settlement (each settlement and the outposts feeding / shielding it, one
/// consolidated row per outpost). Names jump to the world map / open the settlement window. Registered as
/// a singleton through the EmpireRegistry facade.
/// </summary>
public class OutpostMainTab : IMainTabWindowOverview
{
    public static readonly OutpostMainTab Instance = new();

    private const float HeaderHeight = 35f;
    private const float Margin = 5f;

    private FactionFC uiFaction;
    private int subTab;
    private Vector2 scroll = Vector2.zero;
    private float contentHeight = 400f;

    private static readonly List<string> subTabs = [];

    /// <summary>Register/unregister the tab when its setting is toggled at runtime.</summary>
    internal static void SetRegistered(bool on)
    {
        if (on) EmpireRegistry.Register(Instance);
        else EmpireRegistry.Unregister(Instance);
    }

    public string TabName() => "FCVOE_OutpostsMainTab".Translate();

    public void PreOpenWindow(FactionFC faction)
    {
        uiFaction = faction;
        scroll = Vector2.zero;
    }

    public void PostCloseWindow() { uiFaction = null; }

    public void DrawOverviewTab(Rect boundingBox)
    {
        if (uiFaction is null) return;

        // Header bar (no buttons), then the sub-tab row + content inset below it.
        Rect headerRect = new(boundingBox.x + Margin, boundingBox.y + Margin, boundingBox.width - Margin * 2f, HeaderHeight);
        DrawHeader(headerRect);

        subTabs.Clear();
        subTabs.Add("FCVOE_MainTabByOutpost".Translate());
        subTabs.Add("FCVOE_MainTabBySettlement".Translate());

        float bodyY = headerRect.yMax + Margin;
        Rect subtabBox = new(boundingBox.x + Margin, bodyY, boundingBox.width - Margin * 2f, boundingBox.yMax - bodyY - Margin);
        if (subtabBox.height <= 0f) return;

        subTab = UIUtil.DrawTabRowButtonFlat(subtabBox, subTabs, subTab, out Rect contentRect, tabHeight: 26f);
        Rect inner = contentRect.ContractedBy(2f);

        Rect viewRect = ScrollUtil.BeginScrollView(inner, ref scroll, contentHeight);

        Listing_Standard ls = new();
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

    // --- By outpost (each outpost a header, its settlements as rows; mirrors By settlement) ---

    private void DrawByOutpost(Listing_Standard ls)
    {
        List<Outpost> outposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>()
            .OrderBy(o => o.def.label).ThenBy(o => o.RenamableLabel).ToList();
        if (outposts.Count == 0)
        {
            ls.Label("  " + "FCVOE_MainTabNoOutposts".Translate());
            return;
        }

        foreach (Outpost outpost in outposts)
        {
            bool linkable = ResourceLinkUtil.IsLinkable(outpost);
            Rect hdr = OutpostLinkView.DrawOutpostHeader(ls, outpost, linkable ? 70f : 0f);
            if (linkable)
            {
                Rect btnRect = new(hdr.xMax - 66f, hdr.y + 1f, 64f, hdr.height - 2f);
                bool linked = LinkedSettlement(outpost) is not null;
                if (UIUtil.ButtonFlat(btnRect, linked ? "FCVOE_TabUnlink".Translate() : "FCVOE_TabLink".Translate()))
                    ToggleOutpostLink(outpost);
            }

            // Group this outpost's relationships by settlement (one row per settlement it serves).
            List<WorldSettlementFC> order = [];
            Dictionary<WorldSettlementFC, List<LinkBadge>> badges = [];
            List<LinkBadge> Bucket(WorldSettlementFC s)
            {
                if (s is null) return null;
                if (!badges.TryGetValue(s, out List<LinkBadge> list)) { list = []; badges[s] = list; order.Add(s); }
                return list;
            }

            foreach (WorldSettlementFC s in uiFaction.settlements)
            {
                WorldObjectComp_ResourceLink rl = s.GetComponent<WorldObjectComp_ResourceLink>();
                if (rl is not null && rl.IsLinkedHere(outpost))
                    Bucket(s).AddRange(OutpostLinkView.ResourceBadges(outpost, s));

                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                if (links is not null)
                {
                    if (links.GetDeliveryOutpost() == outpost) Bucket(s).Add(OutpostLinkView.TaxDeliveryBadge());
                    if (links.GetFinancingOutpost() == outpost) Bucket(s).Add(OutpostLinkView.FinancingBadge());
                }
            }

            if (order.Count == 0)
            {
                Rect row = ls.GetRect(OutpostLinkView.RowHeight);
                DrawNotLinkedRow(row);
                continue;
            }

            int idx = 0;
            foreach (WorldSettlementFC s in order)
                DrawOutpostSettlementRow(ls, s, badges[s], idx++);
        }
    }

    /// <summary>A settlement row under an outpost header: clickable settlement name + its role badges.</summary>
    private void DrawOutpostSettlementRow(Listing_Standard ls, WorldSettlementFC settlement, List<LinkBadge> badges, int index)
    {
        Rect row = ls.GetRect(OutpostLinkView.RowHeight);
        OutpostLinkView.DrawRowAlt(row, index);

        Rect nameRect = new(row.x + 8f, row.y, (row.width - 8f) * 0.4f, row.height);
        Rect badgeRect = new(nameRect.xMax + 4f, row.y, row.xMax - nameRect.xMax - 8f, row.height);
        OutpostLinkView.DrawClickableSettlementName(nameRect, settlement, AccentUtil.GetSettlementAccent(settlement),
            icon: FindFC.FactionComp?.factionIcon);
        OutpostLinkView.DrawBadgeRow(badgeRect, badges);
    }

    private static void DrawNotLinkedRow(Rect row)
    {
        TextAnchor prevAnchor = Text.Anchor;
        Color prevColor = GUI.color;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        Widgets.Label(new Rect(row.x + 8f, row.y, row.width - 8f, row.height), "FCVOE_MainTabOutpostUnlinked".Translate());
        GUI.color = prevColor;
        Text.Anchor = prevAnchor;
    }

    /// <summary>The settlement an outpost is currently resource-linked to, or null. O(1) via the reverse index.</summary>
    private WorldSettlementFC LinkedSettlement(Outpost outpost) => ResourceLinkUtil.LinkedSettlementOf(outpost);

    /// <summary>Unlinks if already linked; otherwise opens a float menu of in-range settlements to link to.</summary>
    private void ToggleOutpostLink(Outpost outpost)
    {
        WorldSettlementFC linkedTo = LinkedSettlement(outpost);
        if (linkedTo is not null)
        {
            linkedTo.GetComponent<WorldObjectComp_ResourceLink>()?.ToggleLink(outpost);
            return;
        }

        List<FloatMenuOption> options = [];
        foreach (WorldSettlementFC s in uiFaction.settlements)
        {
            if (!ResourceLinkUtil.InLinkRange(outpost, s.Tile)) continue;
            if (s.GetComponent<WorldObjectComp_ResourceLink>() is null) continue;
            WorldSettlementFC captured = s;
            options.Add(new FloatMenuOption(s.Name,
                delegate { captured.GetComponent<WorldObjectComp_ResourceLink>().ToggleLink(outpost); }));
        }
        if (options.Count == 0)
            options.Add(new FloatMenuOption("FCVOE_NoSettlementsInRange".Translate(), null));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    // --- By settlement (one consolidated row per outpost) ---

    private void DrawBySettlement(Listing_Standard ls)
    {
        if (!uiFaction.settlements.Any())
        {
            ls.Label("  " + "FCVOE_MainTabNoSettlements".Translate());
            return;
        }

        foreach (WorldSettlementFC s in uiFaction.settlements)
        {
            OutpostLinkView.DrawSettlementHeader(ls, s);

            // Collect each related outpost once, gathering all the role badges it earns for this settlement.
            List<Outpost> order = [];
            Dictionary<Outpost, List<LinkBadge>> badges = [];
            List<LinkBadge> Bucket(Outpost o)
            {
                if (o is null || o.Destroyed) return null;
                if (!badges.TryGetValue(o, out List<LinkBadge> list)) { list = []; badges[o] = list; order.Add(o); }
                return list;
            }

            WorldObjectComp_ResourceLink rl = s.GetComponent<WorldObjectComp_ResourceLink>();
            if (rl?.linkedOutposts is not null)
                foreach (Outpost o in rl.linkedOutposts)
                    Bucket(o)?.AddRange(OutpostLinkView.ResourceBadges(o, s));

            WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
            Bucket(links?.GetDeliveryOutpost())?.Add(OutpostLinkView.TaxDeliveryBadge());
            Bucket(links?.GetFinancingOutpost())?.Add(OutpostLinkView.FinancingBadge());

            if (order.Count == 0)
            {
                ls.Label("  " + "FCVOE_TabNoLinks".Translate());
                continue;
            }

            int idx = 0;
            foreach (Outpost o in order)
                DrawSettlementOutpostRow(ls, o, badges[o], rl, idx++);
        }
    }

    private void DrawSettlementOutpostRow(Listing_Standard ls, Outpost outpost,
        List<LinkBadge> badges, WorldObjectComp_ResourceLink rl, int index)
    {
        Rect row = ls.GetRect(OutpostLinkView.RowHeight);
        OutpostLinkView.DrawRowAlt(row, index);
        bool canUnlink = rl is not null && rl.IsLinkedHere(outpost);
        float rightEdge = canUnlink ? row.xMax - 70f : row.xMax;

        Rect labelRect = new(row.x + 8f, row.y, (rightEdge - row.x) * 0.4f, row.height);
        Rect badgeRect = new(labelRect.xMax + 4f, row.y, rightEdge - labelRect.xMax - 8f, row.height);

        OutpostLinkView.DrawOutpostLabel(labelRect, outpost, jumpOnClick: true);
        OutpostLinkView.DrawBadgeRow(badgeRect, badges);

        if (canUnlink)
        {
            Rect btnRect = new(row.xMax - 66f, row.y + 2f, 64f, row.height - 4f);
            if (UIUtil.ButtonFlat(btnRect, "FCVOE_TabUnlink".Translate()))
                rl.ToggleLink(outpost);
        }
    }
}
