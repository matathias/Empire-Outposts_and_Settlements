using FactionColonies;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    public class EmpireVOESettings : ModSettings
    {
        public static bool disableIntegration = false;
        public static bool debugLogging = false;

        // Town Settlement feature
        public static bool requireTownForSettlement = false;
        public static bool convertTownPawns = true;
        public static bool pawnSkillBonuses = true;
        public static float additiveBonus = 0.2f;
        public static int skillThreshold = 10;
        public static bool scalingBonus = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref disableIntegration, "disableIntegration", false);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            Scribe_Values.Look(ref requireTownForSettlement, "requireTownForSettlement", false);
            Scribe_Values.Look(ref convertTownPawns, "convertTownPawns", true);
            Scribe_Values.Look(ref pawnSkillBonuses, "pawnSkillBonuses", true);
            Scribe_Values.Look(ref additiveBonus, "additiveBonus", 0.2f);
            Scribe_Values.Look(ref skillThreshold, "skillThreshold", 10);
            Scribe_Values.Look(ref scalingBonus, "scalingBonus", true);
        }
    }

    public class EmpireVOEMod : Mod
    {
        public static EmpireVOESettings settings;

        public EmpireVOEMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<EmpireVOESettings>();
        }

        public override string SettingsCategory()
        {
            return "Empire - VOE Integration";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);

            bool prev = EmpireVOESettings.disableIntegration;
            ls.CheckboxLabeled(
                "VOE_DisableIntegration".Translate(),
                ref EmpireVOESettings.disableIntegration,
                "VOE_DisableIntegrationDesc".Translate());

            if (prev != EmpireVOESettings.disableIntegration)
            {
                if (EmpireVOESettings.disableIntegration)
                    VOETracker.UnregisterAll();
                else
                    VOETracker.ReregisterAll();
            }

            ls.CheckboxLabeled(
                "VOE_DebugLogging".Translate(),
                ref EmpireVOESettings.debugLogging,
                "VOE_DebugLoggingDesc".Translate());

            ls.GapLine();

            // Town Settlement section
            ls.CheckboxLabeled(
                "VOE_RequireTown".Translate(),
                ref EmpireVOESettings.requireTownForSettlement,
                "VOE_RequireTownDesc".Translate());

            if (EmpireVOESettings.requireTownForSettlement)
            {
                ls.CheckboxLabeled(
                    "  " + "VOE_ConvertTownPawns".Translate(),
                    ref EmpireVOESettings.convertTownPawns,
                    "VOE_ConvertTownPawnsDesc".Translate());

                if (EmpireVOESettings.convertTownPawns)
                {
                    ls.CheckboxLabeled(
                        "    " + "VOE_PawnSkillBonuses".Translate(),
                        ref EmpireVOESettings.pawnSkillBonuses,
                        "VOE_PawnSkillBonusesDesc".Translate());

                    if (EmpireVOESettings.pawnSkillBonuses)
                    {
                        ls.Label("    " + "VOE_AdditiveBonus".Translate() + ": " + EmpireVOESettings.additiveBonus.ToString("F2"));
                        EmpireVOESettings.additiveBonus = ls.Slider(EmpireVOESettings.additiveBonus, 0.05f, 1f);

                        ls.Label("    " + "VOE_SkillThreshold".Translate() + ": " + EmpireVOESettings.skillThreshold);
                        EmpireVOESettings.skillThreshold = (int)ls.Slider(EmpireVOESettings.skillThreshold, 1, 30);

                        ls.CheckboxLabeled(
                            "    " + "VOE_ScalingBonus".Translate(),
                            ref EmpireVOESettings.scalingBonus,
                            "VOE_ScalingBonusDesc".Translate());
                    }
                }
            }

            ls.End();
        }
    }
}
