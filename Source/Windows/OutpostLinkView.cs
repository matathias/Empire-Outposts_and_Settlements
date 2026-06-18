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
            string label = outpost.Name ?? outpost.LabelCap;
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

        /// <summary>Distance from a settlement to an outpost, formatted as "{n} tiles".</summary>
        public static string DistanceLabel(WorldSettlementFC settlement, Outpost outpost)
        {
            float distance = Find.WorldGrid.ApproxDistanceInTiles(outpost.Tile, settlement.Tile);
            return distance.ToString("F1") + " " + "VOE_Tiles".Translate();
        }
    }
}
