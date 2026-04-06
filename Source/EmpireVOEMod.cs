using FactionColonies;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    public class EmpireVOESettings : ModSettings
    {
        public static bool disableIntegration = false;
        public static bool debugLogging = false;

        // Skill-based bonuses (shared by science link and town conversion)
        public static float additivePerLevel = 0.025f;
        public static int skillFloor = 0;
        public static int scienceLinkRange = 20;

        // Encampment Recovery
        public static float encampmentBaseReduction = 0.15f;
        public static float encampmentMedicineScaling = 0.01f;
        public static float encampmentMaxReduction = 0.50f;
        public static int encampmentRange = 20;
        public static float encampmentHealRatePerLevel = 0.02f;

        // Threat Scaling
        public static float outpostThreatPerPawn = 0.5f;

        // Town Settlement feature
        public static bool requireTownForSettlement = false;
        public static bool convertTownPawns = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref disableIntegration, "disableIntegration", false);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            // Skill-based bonuses
            Scribe_Values.Look(ref additivePerLevel, "additivePerLevel", 0.025f);
            Scribe_Values.Look(ref skillFloor, "skillFloor", 0);
            Scribe_Values.Look(ref scienceLinkRange, "scienceLinkRange", 20);

            // Encampment Recovery
            Scribe_Values.Look(ref encampmentBaseReduction, "encampmentBaseReduction", 0.15f);
            Scribe_Values.Look(ref encampmentMedicineScaling, "encampmentMedicineScaling", 0.01f);
            Scribe_Values.Look(ref encampmentMaxReduction, "encampmentMaxReduction", 0.50f);
            Scribe_Values.Look(ref encampmentRange, "encampmentRange", 20);
            Scribe_Values.Look(ref encampmentHealRatePerLevel, "encampmentHealRatePerLevel", 0.02f);

            // Threat Scaling
            Scribe_Values.Look(ref outpostThreatPerPawn, "outpostThreatPerPawn", 0.5f);

            // Town Settlement
            Scribe_Values.Look(ref requireTownForSettlement, "requireTownForSettlement", false);
            Scribe_Values.Look(ref convertTownPawns, "convertTownPawns", true);
        }
    }

    public class EmpireVOEMod : Mod
    {
        public static EmpireVOESettings settings;

        public EmpireVOEMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<EmpireVOESettings>();
            
            string modVersion = content?.ModMetaData?.ModVersion;
            if (modVersion.NullOrEmpty())
            {
                VOELog.MessageForce("Did not load a mod version");
            }
            else
            {
                VOELog.MessageForce($"v{modVersion}");
            }
        }

        public override string SettingsCategory()
        {
            return "VOE_Title".Translate();
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
            }

            ls.GapLine();

            // Skill-Based Bonuses section (shared by science link + town conversion)
            ls.Label("VOE_SkillBonusHeader".Translate());
            ls.Label("  " + "VOE_AdditivePerLevel".Translate() + ": " + EmpireVOESettings.additivePerLevel.ToString("F3"));
            EmpireVOESettings.additivePerLevel = (float)System.Math.Round(ls.Slider(EmpireVOESettings.additivePerLevel, 0.005f, 0.1f), 3);

            ls.Label("  " + "VOE_SkillFloor".Translate() + ": " + EmpireVOESettings.skillFloor);
            EmpireVOESettings.skillFloor = (int)ls.Slider(EmpireVOESettings.skillFloor, 0, 10);

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

            ls.Label("  " + "VOE_EncampmentHealRatePerLevel".Translate() + ": " + EmpireVOESettings.encampmentHealRatePerLevel.ToString("F3"));
            EmpireVOESettings.encampmentHealRatePerLevel = (float)System.Math.Round(ls.Slider(EmpireVOESettings.encampmentHealRatePerLevel, 0f, 0.1f), 3);

            ls.GapLine();

            // Threat Scaling section
            ls.Label("VOE_ThreatScalingHeader".Translate());
            ls.Label("  " + "VOE_OutpostThreatPerPawn".Translate() + ": " + EmpireVOESettings.outpostThreatPerPawn.ToString("F2"));
            EmpireVOESettings.outpostThreatPerPawn = ls.Slider(EmpireVOESettings.outpostThreatPerPawn, 0f, 2f);

            ls.Gap(12f);
            if (ls.ButtonText("VOE_OpenPatchNotes".Translate()))
                Find.WindowStack.Add(new PatchNotesDisplayWindow("matathias.empirevoe", "VOE_PatchTitle".Translate()));

            ls.End();
        }
    }
}
