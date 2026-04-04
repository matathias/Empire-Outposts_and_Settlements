using FactionColonies;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    public class EmpireVOESettings : ModSettings
    {
        public static bool disableIntegration = false;
        public static bool debugLogging = false;

        // Science Linking
        public static float scienceBonusPerPawn = 0.5f;
        public static bool scienceSkillScaling = true;
        public static int scienceLinkRange = 20;

        // Encampment Recovery
        public static float encampmentBaseReduction = 0.15f;
        public static float encampmentMedicineScaling = 0.01f;
        public static float encampmentMaxReduction = 0.50f;
        public static int encampmentRange = 20;

        // Threat Scaling
        public static float outpostThreatPerPawn = 0.5f;

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
            // Science Linking
            Scribe_Values.Look(ref scienceBonusPerPawn, "scienceBonusPerPawn", 0.5f);
            Scribe_Values.Look(ref scienceSkillScaling, "scienceSkillScaling", true);
            Scribe_Values.Look(ref scienceLinkRange, "scienceLinkRange", 20);

            // Encampment Recovery
            Scribe_Values.Look(ref encampmentBaseReduction, "encampmentBaseReduction", 0.15f);
            Scribe_Values.Look(ref encampmentMedicineScaling, "encampmentMedicineScaling", 0.01f);
            Scribe_Values.Look(ref encampmentMaxReduction, "encampmentMaxReduction", 0.50f);
            Scribe_Values.Look(ref encampmentRange, "encampmentRange", 20);

            // Threat Scaling
            Scribe_Values.Look(ref outpostThreatPerPawn, "outpostThreatPerPawn", 0.5f);

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
                    WorldComponent_VOETracker.UnregisterAll();
                else
                    WorldComponent_VOETracker.ReregisterAll();
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

            ls.GapLine();

            // Science Linking section
            ls.Label("VOE_ScienceLinkHeader".Translate());
            ls.Label("  " + "VOE_ScienceBonusPerPawn".Translate() + ": " + EmpireVOESettings.scienceBonusPerPawn.ToString("F2"));
            EmpireVOESettings.scienceBonusPerPawn = ls.Slider(EmpireVOESettings.scienceBonusPerPawn, 0.1f, 2f);

            ls.CheckboxLabeled(
                "  " + "VOE_ScienceSkillScaling".Translate(),
                ref EmpireVOESettings.scienceSkillScaling,
                "VOE_ScienceSkillScalingDesc".Translate());

            ls.Label("  " + "VOE_ScienceLinkRange".Translate() + ": " + EmpireVOESettings.scienceLinkRange);
            EmpireVOESettings.scienceLinkRange = (int)ls.Slider(EmpireVOESettings.scienceLinkRange, 5, 50);

            ls.GapLine();

            // Encampment Recovery section
            ls.Label("VOE_EncampmentRecoveryHeader".Translate());
            ls.Label("  " + "VOE_EncampmentBaseReduction".Translate() + ": " + EmpireVOESettings.encampmentBaseReduction.ToString("P0"));
            EmpireVOESettings.encampmentBaseReduction = ls.Slider(EmpireVOESettings.encampmentBaseReduction, 0.05f, 0.5f);

            ls.Label("  " + "VOE_EncampmentMedicineScaling".Translate() + ": " + EmpireVOESettings.encampmentMedicineScaling.ToString("F3"));
            EmpireVOESettings.encampmentMedicineScaling = ls.Slider(EmpireVOESettings.encampmentMedicineScaling, 0f, 0.05f);

            ls.Label("  " + "VOE_EncampmentMaxReduction".Translate() + ": " + EmpireVOESettings.encampmentMaxReduction.ToString("P0"));
            EmpireVOESettings.encampmentMaxReduction = ls.Slider(EmpireVOESettings.encampmentMaxReduction, 0.1f, 0.75f);

            ls.Label("  " + "VOE_EncampmentRange".Translate() + ": " + EmpireVOESettings.encampmentRange);
            EmpireVOESettings.encampmentRange = (int)ls.Slider(EmpireVOESettings.encampmentRange, 5, 50);

            ls.GapLine();

            // Threat Scaling section
            ls.Label("VOE_ThreatScalingHeader".Translate());
            ls.Label("  " + "VOE_OutpostThreatPerPawn".Translate() + ": " + EmpireVOESettings.outpostThreatPerPawn.ToString("F2"));
            EmpireVOESettings.outpostThreatPerPawn = ls.Slider(EmpireVOESettings.outpostThreatPerPawn, 0f, 2f);

            ls.End();
        }
    }
}
