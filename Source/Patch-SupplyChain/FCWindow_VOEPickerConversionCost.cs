using FactionColonies;
using Outposts;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EmpireVOE.SupplyChain
{
    /// <summary>
    /// Companion to the conversion type-picker (<see cref="FCWindow_SettlementTypePicker"/>) opened by
    /// the outpost "Convert to settlement" gizmo. Previews the reduced R&amp;R resource cost for the type
    /// the cursor is over, computed from the outpost's own tile. Self-positions beside the picker (the
    /// Found-screen companion reflow does not apply to this anchor) and closes when the picker closes.
    /// </summary>
    public class FCWindow_VOEPickerConversionCost : Window
    {
        private const float WindowWidth = 260f;
        private const float Gap = 10f;

        private readonly Outpost outpost;

        public override Vector2 InitialSize => new Vector2(WindowWidth, 350f);

        public FCWindow_VOEPickerConversionCost(Outpost outpost)
        {
            this.outpost = outpost;
            draggable = false;
            doCloseX = false;
            preventCameraMotion = false;
            forcePause = false;
            closeOnAccept = false;
            closeOnCancel = false;
            drawShadow = false;
        }

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            DockToPicker();
        }

        private void DockToPicker()
        {
            FCWindow_SettlementTypePicker picker = Find.WindowStack.WindowOfType<FCWindow_SettlementTypePicker>();
            if (picker is null) return;
            windowRect.x = picker.windowRect.x - windowRect.width - Gap;
            windowRect.y = picker.windowRect.y;
        }

        public override void DoWindowContents(Rect inRect)
        {
            FCWindow_SettlementTypePicker picker = Find.WindowStack.WindowOfType<FCWindow_SettlementTypePicker>();
            if (picker is null || outpost is null || !outpost.Spawned)
            {
                Close();
                return;
            }

            // Track the picker as it is dragged.
            DockToPicker();

            WorldSettlementDef type = picker.HoveredType;
            if (type is null || !VOEConversionCostView.ShouldShow(type))
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(0, 0, inRect.width, 40f), "FCVOE_ConversionCostHoverPrompt".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                windowRect.height = 40f + Window.StandardMargin * 2;
                return;
            }

            float height = VOEConversionCostView.Draw(inRect, outpost.Tile, type);
            windowRect.height = height + Window.StandardMargin * 2;
        }
    }
}
