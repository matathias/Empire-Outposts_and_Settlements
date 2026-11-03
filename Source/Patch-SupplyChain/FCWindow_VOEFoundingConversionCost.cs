using FactionColonies;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace EmpireVOE.SupplyChain
{
    /// <summary>
    /// Companion to the Found-screen, shown only when founding is restricted to outposts and Routes &amp;
    /// Resources is active. Replaces R&amp;R's own founding-cost window (which is suppressed in that case)
    /// to preview the <em>reduced conversion</em> cost for the selected settlement type and make clear
    /// the cost is paid when the outpost is converted, not when it is founded.
    /// </summary>
    public class FCWindow_VOEFoundingConversionCost : Window, IFoundingCompanionWindow
    {
        // Order 20 takes the slot R&R's suppressed FCWindow_FoundingSource used; EmpireVOE's
        // FCWindow_OutpostRequirements (order 10) still sits closest to the main window.
        public int CompanionOrder => 20;

        private const float WindowWidth = 260f;

        public override Vector2 InitialSize => new Vector2(WindowWidth, 350f);

        public FCWindow_VOEFoundingConversionCost()
        {
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            forcePause = false;
            closeOnAccept = false;
            closeOnCancel = false;
        }

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            FoundingScreenHooks.ReflowCompanions();
        }

        public override void DoWindowContents(Rect inRect)
        {
            CreateColonyWindowFc createWindow = Find.WindowStack.WindowOfType<CreateColonyWindowFc>();
            if (createWindow is null)
            {
                Close();
                return;
            }

            WorldSettlementDef type = createWindow.currentSettlementType;
            if (!VOEConversionCostView.ShouldShow(type))
            {
                Close();
                return;
            }

            PlanetTile tile = createWindow.currentTileSelected;
            float height = VOEConversionCostView.Draw(inRect, tile, type);
            windowRect.height = height + Window.StandardMargin * 2;
        }

        public static void Refresh()
        {
            FCWindow_VOEFoundingConversionCost existing =
                Find.WindowStack.WindowOfType<FCWindow_VOEFoundingConversionCost>();
            if (existing is null)
                Find.WindowStack.Add(new FCWindow_VOEFoundingConversionCost());
        }

        public static void TryClose()
        {
            Find.WindowStack.WindowOfType<FCWindow_VOEFoundingConversionCost>()?.Close();
        }
    }
}
