using FactionColonies;
using Outposts;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
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

        /// <summary>Draws the outpost's type icon (tinted) followed by its name, clamped to the rect.</summary>
        public static void DrawOutpostLabel(Rect rect, Outpost outpost, string suffix = null)
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
            Widgets.DrawHighlight(rect);

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

        /// <summary>Distance from a settlement to an outpost, formatted as "{n} tiles".</summary>
        public static string DistanceLabel(WorldSettlementFC settlement, Outpost outpost)
        {
            float distance = Find.WorldGrid.ApproxDistanceInTiles(outpost.Tile, settlement.Tile);
            return distance.ToString("F1") + " " + "VOE_Tiles".Translate();
        }
    }
}
