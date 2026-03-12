using FactionColonies;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    public class EmpireVOESettings : ModSettings
    {
        public static bool disableIntegration = false;
        public static bool debugLogging = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref disableIntegration, "disableIntegration", false);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
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

            ls.End();
        }
    }
}
