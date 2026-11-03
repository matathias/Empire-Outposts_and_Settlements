using System.Collections.Generic;
using FactionColonies;
using RimWorld;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Companion to the Found-screen, shown only when founding is restricted to outposts. Lists the
    /// outpost types the player must establish (and then convert) to found the selected settlement type.
    /// Modeled on FactionColonies.FCWindow_CreateColonyStatModifiers.
    /// </summary>
    public class FCWindow_OutpostRequirements : Window, IFoundingCompanionWindow
    {
        public int CompanionOrder => 10;

        private const float WindowWidth = 280f;
        private const float Padding = 8f;

        private WorldSettlementDef type;
        private List<WorldObjectDef> outposts;

        public override Vector2 InitialSize => new Vector2(WindowWidth, 220f);

        public FCWindow_OutpostRequirements(WorldSettlementDef type, List<WorldObjectDef> outposts)
        {
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            forcePause = false;
            closeOnAccept = false;
            closeOnCancel = false;
            this.type = type;
            this.outposts = outposts;
        }

        public void SetData(WorldSettlementDef newType, List<WorldObjectDef> newOutposts)
        {
            type = newType;
            outposts = newOutposts;
        }

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            FoundingScreenHooks.ReflowCompanions();
        }

        public override void DoWindowContents(Rect inRect)
        {
            CreateColonyWindowFc createWindow = Find.WindowStack.WindowOfType<CreateColonyWindowFc>();
            if (createWindow is null || type is null)
            {
                Close();
                return;
            }

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float curY = 0f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0, curY, inRect.width, 30f), "FCVOE_OutpostRequirementsTitle".Translate());
            curY += 30f + Padding;

            Text.Anchor = TextAnchor.UpperLeft;
            string intro = "FCVOE_OutpostRequirementsDesc".Translate(type.LabelCap);
            float introH = Text.CalcHeight(intro, inRect.width);
            Widgets.Label(new Rect(0, curY, inRect.width, introH), intro);
            curY += introH + Padding;

            if (outposts is null || outposts.Count == 0)
            {
                string none = "FCVOE_OutpostRequirementsNone".Translate();
                float h = Text.CalcHeight(none, inRect.width);
                Widgets.Label(new Rect(0, curY, inRect.width, h), none);
                curY += h;
            }
            else
            {
                foreach (WorldObjectDef wod in outposts)
                {
                    string line = "  - " + wod.LabelCap;
                    float h = Text.CalcHeight(line, inRect.width);
                    Widgets.Label(new Rect(0, curY, inRect.width, h), line);
                    curY += h;
                }
            }

            windowRect.height = curY + Padding + Window.StandardMargin * 2;

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        public static void RefreshFor(WorldSettlementDef type)
        {
            FCWindow_OutpostRequirements existing = Find.WindowStack.WindowOfType<FCWindow_OutpostRequirements>();
            if (type is null)
            {
                existing?.Close();
                return;
            }
            List<WorldObjectDef> outposts = OutpostConversionUtil.GetOutpostTypesFor(type);
            if (existing is null)
                Find.WindowStack.Add(new FCWindow_OutpostRequirements(type, outposts));
            else
                existing.SetData(type, outposts);
        }

        public static void TryClose()
        {
            Find.WindowStack.WindowOfType<FCWindow_OutpostRequirements>()?.Close();
        }
    }
}
