using System.Collections.Generic;
using FactionColonies;
using Outposts;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    /// <summary>A single outpost-to-settlement relationship (resource feed / tax delivery / financing).</summary>
    public struct OutpostRelation
    {
        public WorldSettlementFC settlement;
        public string role;

        public OutpostRelation(WorldSettlementFC settlement, string role)
        {
            this.settlement = settlement;
            this.role = role;
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
        public static void DrawSectionHeader(Listing_Standard ls, string title, Texture2D icon = null, Color? iconColor = null)
        {
            ls.Gap(4f);
            Rect rect = ls.GetRect(HeaderHeight);
            Widgets.DrawHighlight(rect);

            float textX = rect.x + 6f;
            if (icon != null)
            {
                Rect iconRect = new Rect(rect.x + 4f, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
                Color prevIcon = GUI.color;
                GUI.color = iconColor ?? Color.white;
                GUI.DrawTexture(iconRect, icon);
                GUI.color = prevIcon;
                textX = iconRect.xMax + 6f;
            }

            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.85f, 0.85f, 0.7f);
            Widgets.Label(new Rect(textX, rect.y, rect.xMax - textX - 6f, rect.height), title);
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
            Widgets.DrawHighlight(rect);

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
        /// Draws an outpost's relationships on one line as `[clickable settlement] [role]` segments, left to
        /// right. Each settlement name opens its window on click. Empty list renders "not linked".
        /// </summary>
        public static void DrawOutpostRelations(Rect rect, List<OutpostRelation> relations)
        {
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (relations is null || relations.Count == 0)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(rect, "VOE_MainTabOutpostUnlinked".Translate());
                GUI.color = prevColor;
                Text.Anchor = prevAnchor;
                Text.Font = prevFont;
                return;
            }

            float x = rect.x;
            for (int i = 0; i < relations.Count && x < rect.xMax; i++)
            {
                OutpostRelation rel = relations[i];

                string sName = rel.settlement.Name;
                float nameW = Mathf.Min(Text.CalcSize(sName).x, rect.xMax - x);
                Rect nameRect = new Rect(x, rect.y, nameW, rect.height);
                GUI.color = AccentUtil.GetSettlementAccent(rel.settlement);
                Widgets.Label(nameRect, sName);
                GUI.color = prevColor;
                if (Mouse.IsOver(nameRect)) Widgets.DrawHighlight(nameRect);
                if (Widgets.ButtonInvisible(nameRect))
                    Find.WindowStack.Add(new SettlementWindowFc(rel.settlement));
                x = nameRect.xMax + 4f;

                if (!rel.role.NullOrEmpty() && x < rect.xMax)
                {
                    float roleW = Mathf.Min(Text.CalcSize(rel.role).x, rect.xMax - x);
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(new Rect(x, rect.y, roleW, rect.height), rel.role);
                    GUI.color = prevColor;
                    x += roleW + 8f;
                }
            }

            Text.Anchor = prevAnchor;
            Text.Font = prevFont;
        }

        /// <summary>Distance from a settlement to an outpost, formatted as "{n} tiles".</summary>
        public static string DistanceLabel(WorldSettlementFC settlement, Outpost outpost)
        {
            float distance = Find.WorldGrid.ApproxDistanceInTiles(outpost.Tile, settlement.Tile);
            return distance.ToString("F1") + " " + "VOE_Tiles".Translate();
        }
    }
}
