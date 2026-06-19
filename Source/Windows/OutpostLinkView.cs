using System.Collections.Generic;
using FactionColonies;
using Outposts;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    /// <summary>A compact role badge (icon + colored label) describing one thing an outpost does.</summary>
    public struct LinkBadge
    {
        public string label;
        public Texture2D icon;
        public Color color;

        public LinkBadge(string label, Texture2D icon, Color color)
        {
            this.label = label;
            this.icon = icon;
            this.color = color;
        }
    }

    /// <summary>
    /// Shared row renderers for the outpost-link surfaces (the settlement-window Outposts subtab and the
    /// faction-wide Outposts main tab), so both draw identical rows. Every outpost label is drawn icon-first,
    /// using the outpost type's expanding icon (the same icon shown in the world-map gizmos).
    /// </summary>
    public static class OutpostLinkView
    {
        public const float RowHeight = 28f;
        private const float IconSize = 22f;
        private const float HeaderHeight = 24f;

        /// <summary>
        /// Draws the outpost's type icon (tinted) followed by its name, clamped to the rect. When
        /// <paramref name="jumpOnClick"/> is set the whole rect becomes a clickable button that jumps the
        /// world camera to (and selects) the outpost.
        /// </summary>
        public static void DrawOutpostLabel(Rect rect, Outpost outpost, string suffix = null, bool jumpOnClick = false)
        {
            if (outpost is null) return;

            Rect iconRect = new Rect(rect.x, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
            Texture2D icon = outpost.ExpandingIcon;
            if (icon != null)
            {
                Color prev = GUI.color;
                GUI.color = outpost.ExpandingIconColor;
                GUI.DrawTexture(iconRect, icon);
                GUI.color = prev;
            }

            Rect labelRect = new Rect(iconRect.xMax + 6f, rect.y, rect.width - IconSize - 6f, rect.height);
            // VOE overrides Outpost.Label => Name, so Name/LabelCap are both null for an un-renamed
            // outpost; RenamableLabel falls back to def.label and is never null (Text.ClampTextWithEllipsis
            // dereferences the string with no null guard).
            string label = outpost.RenamableLabel;
            if (!suffix.NullOrEmpty()) label += "  " + suffix;

            TextAnchor prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, Text.ClampTextWithEllipsis(labelRect, label));
            Text.Anchor = prevAnchor;

            if (jumpOnClick)
            {
                if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
                if (Widgets.ButtonInvisible(rect))
                    CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(outpost));
            }
        }

        /// <summary>
        /// Draws a clickable settlement name (opens that settlement's window) in the given color, optionally
        /// with a leading icon. The whole rect (icon + name) is the click target.
        /// </summary>
        public static void DrawClickableSettlementName(Rect rect, WorldSettlementFC settlement, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, Texture2D icon = null)
        {
            if (settlement is null) return;

            Rect labelRect = rect;
            if (icon != null)
            {
                Rect iconRect = new Rect(rect.x, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
                GUI.DrawTexture(iconRect, icon);
                labelRect = new Rect(iconRect.xMax + 6f, rect.y, rect.xMax - iconRect.xMax - 6f, rect.height);
            }

            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            Text.Anchor = anchor;
            GUI.color = color;
            Widgets.Label(labelRect, Text.ClampTextWithEllipsis(labelRect, settlement.Name));
            GUI.color = prevColor;
            Text.Anchor = prevAnchor;

            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
            if (Widgets.ButtonInvisible(rect))
                Find.WindowStack.Add(new SettlementWindowFc(settlement));
        }

        /// <summary>Right-aligned grey detail text (distance, contribution, status) within the rect.</summary>
        public static void DrawDetail(Rect rect, string text)
        {
            if (text.NullOrEmpty()) return;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(rect, Text.ClampTextWithEllipsis(rect, text));
            GUI.color = prevColor;
            Text.Anchor = prevAnchor;
        }

        /// <summary>
        /// Draws a highlighted section header (subtle highlight bar behind the tan label), shared by both
        /// outpost-link surfaces so sections read as clearly separated. Matches the base mod's
        /// <c>UIUtil.HighlightedLabel</c> idiom.
        /// </summary>
        public static void DrawSectionHeader(Listing_Standard ls, string title)
        {
            ls.Gap(4f);
            Rect rect = ls.GetRect(HeaderHeight);
            Widgets.DrawBoxSolid(rect, ColorUtil.Gray3);

            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.85f, 0.85f, 0.7f);
            Widgets.Label(new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height), title);
            GUI.color = prevColor;
            Text.Anchor = prevAnchor;
            Text.Font = prevFont;

            ls.Gap(2f);
        }

        /// <summary>
        /// Section header for a settlement (by-settlement subtab): subtle highlight bar with the Empire
        /// emblem and the settlement name in its accent color (brighter than the muted section tan). The
        /// whole bar is clickable and opens that settlement's window.
        /// </summary>
        public static void DrawSettlementHeader(Listing_Standard ls, WorldSettlementFC settlement)
        {
            ls.Gap(4f);
            Rect rect = ls.GetRect(HeaderHeight);
            Widgets.DrawBoxSolid(rect, ColorUtil.Gray3);

            float textX = rect.x + 6f;
            Texture2D icon = FindFC.FactionComp?.factionIcon;
            if (icon != null)
            {
                Rect iconRect = new Rect(rect.x + 4f, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
                GUI.DrawTexture(iconRect, icon);
                textX = iconRect.xMax + 6f;
            }

            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = AccentUtil.GetSettlementAccent(settlement);
            Rect nameRect = new Rect(textX, rect.y, rect.xMax - textX - 6f, rect.height);
            Widgets.Label(nameRect, Text.ClampTextWithEllipsis(nameRect, settlement.Name));
            GUI.color = prevColor;
            Text.Anchor = prevAnchor;
            Text.Font = prevFont;

            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
            if (Widgets.ButtonInvisible(rect))
                Find.WindowStack.Add(new SettlementWindowFc(settlement));

            ls.Gap(2f);
        }

        /// <summary>
        /// Section header for an outpost (by-outpost subtab): lighter bar with the outpost's type icon + name
        /// (clicking jumps to it on the map). Reserves <paramref name="reserveRight"/> px on the right so the
        /// caller can place a Link/Unlink button; returns the header rect.
        /// </summary>
        public static Rect DrawOutpostHeader(Listing_Standard ls, Outpost outpost, float reserveRight)
        {
            ls.Gap(4f);
            Rect rect = ls.GetRect(HeaderHeight);
            Widgets.DrawBoxSolid(rect, ColorUtil.Gray3);

            Rect labelArea = new Rect(rect.x + 2f, rect.y, rect.width - 4f - reserveRight, rect.height);
            DrawOutpostLabel(labelArea, outpost, jumpOnClick: true);

            ls.Gap(2f);
            return rect;
        }

        /// <summary>Zebra-stripes a row: shades odd-indexed rows for readability.</summary>
        public static void DrawRowAlt(Rect row, int index)
        {
            if ((index & 1) == 1) Widgets.DrawAltRect(row);
        }

        // --- Role badges ---

        private const float BadgeIconSize = 16f;
        private const float BadgePad = 6f;
        private const float BadgeGap = 4f;

        /// <summary>
        /// One badge per resource the outpost boosts (icon + name in the resource's color), with the per-worker
        /// production bonus for <paramref name="settlement"/> baked into the label as "(+x.x)".
        /// </summary>
        public static List<LinkBadge> ResourceBadges(Outpost outpost, WorldSettlementFC settlement)
        {
            List<LinkBadge> badges = new List<LinkBadge>();
            OutpostResourceLinkExtension ext = outpost?.def.GetModExtension<OutpostResourceLinkExtension>();
            if (ext?.resources is object)
            {
                double contrib = ResourceLinkUtil.ContributionOf(outpost, settlement);
                string bonus = " (+" + contrib.ToString("0.##") + ")";
                foreach (ResourceTypeDef r in ext.resources)
                    badges.Add(new LinkBadge(r.LabelCap.ToString() + bonus, r.Icon, r.color));
            }
            return badges;
        }

        public static LinkBadge TaxDeliveryBadge() =>
            new LinkBadge("VOE_TabSectionTaxDelivery".Translate(), null, new Color(0.78f, 0.82f, 0.9f));

        public static LinkBadge FinancingBadge() =>
            new LinkBadge("VOE_TabSectionFinancing".Translate(), null, ColorUtil.Gold);

        /// <summary>Lays out badges left-to-right within the area, clamping when out of room.</summary>
        public static void DrawBadgeRow(Rect area, List<LinkBadge> badges)
        {
            if (badges is null || badges.Count == 0) return;

            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            float h = Mathf.Min(area.height, 22f);
            float y = area.y + (area.height - h) / 2f;
            float x = area.x;

            foreach (LinkBadge b in badges)
            {
                if (x >= area.xMax) break;
                float iconW = b.icon != null ? BadgeIconSize + 4f : 0f;
                float w = BadgePad + iconW + Text.CalcSize(b.label).x + BadgePad;
                w = Mathf.Min(w, area.xMax - x);
                DrawBadge(new Rect(x, y, w, h), b);
                x += w + BadgeGap;
            }

            Text.Font = prevFont;
        }

        private static void DrawBadge(Rect rect, LinkBadge badge)
        {
            Color prevColor = GUI.color;
            TextAnchor prevAnchor = Text.Anchor;

            Widgets.DrawBoxSolid(rect, ColorUtil.SetA(badge.color, 0.15f));

            float textX = rect.x + BadgePad;
            if (badge.icon != null)
            {
                GUI.DrawTexture(new Rect(rect.x + BadgePad, rect.y + (rect.height - BadgeIconSize) / 2f, BadgeIconSize, BadgeIconSize), badge.icon);
                textX += BadgeIconSize + 4f;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = badge.color;
            Widgets.Label(new Rect(textX, rect.y, rect.xMax - textX - 4f, rect.height), badge.label);

            GUI.color = prevColor;
            Text.Anchor = prevAnchor;
        }
    }
}
