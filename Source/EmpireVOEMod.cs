using System.Collections.Generic;
using FactionColonies;
using UnityEngine;
using Verse;

namespace EmpireVOE
{
    public class EmpireVOESettings : ModSettings
    {
        // Master toggle
        public static bool disableIntegration = false;
        public static bool debugLogging = false;

        // Per-feature toggles
        public static bool enableMilitary = true;
        public static bool enableDelivery = true;
        public static bool enableFinancing = true;
        public static bool enableResourceLink = true;
        public static bool enableEncampment = true;
        public static bool enableOutpostConversion = true;
        public static bool enableRoads = true;
        public static bool enableOutpostMainTab = true;

        // Skill-based bonuses (used by resource linking and outpost conversion)
        public static float additivePerLevel = 0.025f;
        public static int skillFloor = 0;
        public static int resourceLinkRange = 10;

        // Encampment Recovery
        public static int encampmentRange = 10;
        public static float encampmentHealRatePerLevel = 0.02f;

        // Passive defensive aura (nearby defensive outposts stiffen a settlement's defense)
        public static bool enableDefensiveAura = true;
        public static bool enableDefensiveAuraDivert = false;
        public static int defensiveAuraRange = 10;
        public static float defensiveAuraLevelFactor = 0.25f;
        public static float defensiveAuraDivertScale = 0.1f;
        public static float defensiveAuraWeightFloor = 0.25f;

        // Outpost -> Settlement conversion feature
        public static bool requireOutpostForSettlement = false;
        public static bool convertOutpostPawns = true;
        public static float reducedFoundingCostFactor = 0.5f;
        public static bool enableConversionDelay = true;
        public static int conversionDelayDays = 30;
        public static float townFlatAdditive = 0.25f;

        // VOE Town spawn requirement (absorbed from "VOE Towns Count Outposts")
        public static int townMinSettlements = 0;
        public static int townMinOutposts = 0;
        public static int townMinTotal = 3;
        public static int townRange = 10;
        public static bool townExcludeTowns = false;

        // VOE Town xenotype-pure recruiting (per-town toggle lives on WorldObjectComp_TownRecruiting;
        // this is the global recruit-chance multiplier applied while a town has the mode on)
        public static float townXenotypePureChanceMult = 0.5f;

        // Compound checks — master toggle + per-feature toggle
        public static bool MilitaryActive => !disableIntegration && enableMilitary;
        public static bool DeliveryActive => !disableIntegration && enableDelivery;
        public static bool FinancingActive => !disableIntegration && enableFinancing;
        public static bool ResourceLinkActive => !disableIntegration && enableResourceLink;
        public static bool EncampmentActive => !disableIntegration && enableEncampment;
        public static bool OutpostConversionActive => !disableIntegration && enableOutpostConversion;
        public static bool RoadsActive => !disableIntegration && enableRoads;
        // Shield half on by default; raid-diversion half is opt-in. Both gated on the Military toggle.
        public static bool DefensiveAuraActive => !disableIntegration && enableMilitary && enableDefensiveAura;
        public static bool DefensiveAuraDivertActive => !disableIntegration && enableMilitary && enableDefensiveAuraDivert;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref disableIntegration, "disableIntegration", false);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);

            // Per-feature toggles
            Scribe_Values.Look(ref enableMilitary, "enableMilitary", true);
            Scribe_Values.Look(ref enableDelivery, "enableDelivery", true);
            Scribe_Values.Look(ref enableFinancing, "enableFinancing", true);
            Scribe_Values.Look(ref enableResourceLink, "enableResourceLink", true);
            Scribe_Values.Look(ref enableEncampment, "enableEncampment", true);
            Scribe_Values.Look(ref enableOutpostConversion, "enableOutpostConversion", true);
            Scribe_Values.Look(ref enableOutpostMainTab, "enableOutpostMainTab", true);

            // Skill-based bonuses
            Scribe_Values.Look(ref additivePerLevel, "additivePerLevel", 0.025f);
            Scribe_Values.Look(ref skillFloor, "skillFloor", 0);
            Scribe_Values.Look(ref resourceLinkRange, "resourceLinkRange", 10);

            // Encampment Recovery
            Scribe_Values.Look(ref encampmentRange, "encampmentRange", 10);
            Scribe_Values.Look(ref encampmentHealRatePerLevel, "encampmentHealRatePerLevel", 0.02f);

            // Passive defensive aura
            Scribe_Values.Look(ref enableDefensiveAura, "enableDefensiveAura", true);
            Scribe_Values.Look(ref enableDefensiveAuraDivert, "enableDefensiveAuraDivert", false);
            Scribe_Values.Look(ref defensiveAuraRange, "defensiveAuraRange", 10);
            Scribe_Values.Look(ref defensiveAuraLevelFactor, "defensiveAuraLevelFactor", 0.25f);
            Scribe_Values.Look(ref defensiveAuraDivertScale, "defensiveAuraDivertScale", 0.1f);
            Scribe_Values.Look(ref defensiveAuraWeightFloor, "defensiveAuraWeightFloor", 0.25f);

            // Road Integration
            Scribe_Values.Look(ref enableRoads, "enableRoads", true);

            // Outpost -> Settlement conversion
            Scribe_Values.Look(ref requireOutpostForSettlement, "requireOutpostForSettlement", false);
            Scribe_Values.Look(ref convertOutpostPawns, "convertOutpostPawns", true);
            Scribe_Values.Look(ref reducedFoundingCostFactor, "reducedFoundingCostFactor", 0.5f);
            Scribe_Values.Look(ref enableConversionDelay, "enableConversionDelay", true);
            Scribe_Values.Look(ref conversionDelayDays, "conversionDelayDays", 30);
            Scribe_Values.Look(ref townFlatAdditive, "townFlatAdditive", 0.25f);

            // VOE Town spawn requirement (absorbed from "VOE Towns Count Outposts")
            Scribe_Values.Look(ref townMinSettlements, "townMinSettlements", 0);
            Scribe_Values.Look(ref townMinOutposts, "townMinOutposts", 0);
            Scribe_Values.Look(ref townMinTotal, "townMinTotal", 3);
            Scribe_Values.Look(ref townRange, "townRange", 10);
            Scribe_Values.Look(ref townExcludeTowns, "townExcludeTowns", false);

            // VOE Town xenotype-pure recruiting
            Scribe_Values.Look(ref townXenotypePureChanceMult, "townXenotypePureChanceMult", 0.5f);
        }
    }

    public class EmpireVOEMod : Mod
    {
        public static EmpireVOESettings settings;

        // Settings window tab state
        private static int settingsTab = 0;
        private static readonly List<TabRecord> settingsTabs = new List<TabRecord>();
        private static Vector2 integrationScroll = Vector2.zero;
        private static Vector2 townsScroll = Vector2.zero;
        private static float integrationContentHeight = 1000f;
        private static float townsContentHeight = 1000f;

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
            settingsTabs.Clear();
            settingsTabs.Add(new TabRecord("VOE_TabIntegration".Translate(), delegate { settingsTab = 0; }, settingsTab == 0));
            settingsTabs.Add(new TabRecord("VOE_TabTowns".Translate(), delegate { settingsTab = 1; }, settingsTab == 1));

            Rect contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            Widgets.DrawMenuSection(contentRect);
            TabDrawer.DrawTabs(contentRect, settingsTabs);

            Rect innerRect = contentRect.ContractedBy(10f);

            switch (settingsTab)
            {
                case 0: DoIntegrationTab(innerRect); break;
                case 1: DoTownsTab(innerRect); break;
            }
        }

        private void DoIntegrationTab(Rect rect)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, integrationContentHeight);
            Widgets.BeginScrollView(rect, ref integrationScroll, viewRect);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);

            // Master toggle
            bool prev = EmpireVOESettings.disableIntegration;
            ls.CheckboxLabeled(
                "VOE_DisableIntegration".Translate(),
                ref EmpireVOESettings.disableIntegration,
                "VOE_DisableIntegrationDesc".Translate());

            if (prev != EmpireVOESettings.disableIntegration)
            {
                if (EmpireVOESettings.disableIntegration)
                    WorldObjectComp_EmpireOutpost.UnregisterAll();
                else
                    WorldObjectComp_EmpireOutpost.ReregisterAll();
            }

            ls.CheckboxLabeled(
                "VOE_DebugLogging".Translate(),
                ref EmpireVOESettings.debugLogging,
                "VOE_DebugLoggingDesc".Translate());

            ls.GapLine();

            // --- Military ---
            bool prevMil = EmpireVOESettings.enableMilitary;
            ls.CheckboxLabeled(
                "VOE_EnableMilitary".Translate(),
                ref EmpireVOESettings.enableMilitary,
                "VOE_EnableMilitaryDesc".Translate());
            if (prevMil != EmpireVOESettings.enableMilitary && !EmpireVOESettings.disableIntegration)
            {
                WorldObjectComp_EmpireOutpost.ToggleMilitary(EmpireVOESettings.enableMilitary);
            }

            if (EmpireVOESettings.enableMilitary)
            {
                ls.CheckboxLabeled(
                    "  " + "VOE_EnableDefensiveAura".Translate(),
                    ref EmpireVOESettings.enableDefensiveAura,
                    "VOE_EnableDefensiveAuraDesc".Translate());

                if (EmpireVOESettings.enableDefensiveAura)
                {
                    int prevRange = EmpireVOESettings.defensiveAuraRange;
                    float prevFactor = EmpireVOESettings.defensiveAuraLevelFactor;

                    ls.Label("    " + "VOE_DefensiveAuraRange".Translate() + ": " + EmpireVOESettings.defensiveAuraRange);
                    EmpireVOESettings.defensiveAuraRange = (int)ls.Slider(EmpireVOESettings.defensiveAuraRange, 5, 50);

                    ls.Label("    " + "VOE_DefensiveAuraLevelFactor".Translate() + ": " + EmpireVOESettings.defensiveAuraLevelFactor.ToString("P0"));
                    EmpireVOESettings.defensiveAuraLevelFactor = (float)System.Math.Round(ls.Slider(EmpireVOESettings.defensiveAuraLevelFactor, 0.05f, 1f), 2);

                    ls.CheckboxLabeled(
                        "    " + "VOE_EnableDefensiveAuraDivert".Translate(),
                        ref EmpireVOESettings.enableDefensiveAuraDivert,
                        "VOE_EnableDefensiveAuraDivertDesc".Translate());

                    if (prevRange != EmpireVOESettings.defensiveAuraRange ||
                        System.Math.Abs(prevFactor - EmpireVOESettings.defensiveAuraLevelFactor) > 0.0001f)
                    {
                        DefensiveAuraCache.Invalidate();
                    }
                }
            }

            ls.GapLine();

            // --- Tax Delivery ---
            ls.CheckboxLabeled(
                "VOE_EnableDelivery".Translate(),
                ref EmpireVOESettings.enableDelivery,
                "VOE_EnableDeliveryDesc".Translate());

            ls.GapLine();

            // --- Financing ---
            ls.CheckboxLabeled(
                "VOE_EnableFinancing".Translate(),
                ref EmpireVOESettings.enableFinancing,
                "VOE_EnableFinancingDesc".Translate());

            ls.GapLine();

            // --- Resource Linking ---
            ls.CheckboxLabeled(
                "VOE_EnableResourceLink".Translate(),
                ref EmpireVOESettings.enableResourceLink,
                "VOE_EnableResourceLinkDesc".Translate());

            if (EmpireVOESettings.enableResourceLink)
            {
                ls.Label("  " + "VOE_ResourceLinkRange".Translate() + ": " + EmpireVOESettings.resourceLinkRange);
                EmpireVOESettings.resourceLinkRange = (int)ls.Slider(EmpireVOESettings.resourceLinkRange, 5, 50);

                ls.Label("  " + "VOE_AdditivePerLevel".Translate() + ": " + EmpireVOESettings.additivePerLevel.ToString("F3"));
                EmpireVOESettings.additivePerLevel = (float)System.Math.Round(ls.Slider(EmpireVOESettings.additivePerLevel, 0.005f, 0.1f), 3);

                ls.Label("  " + "VOE_SkillFloor".Translate() + ": " + EmpireVOESettings.skillFloor);
                EmpireVOESettings.skillFloor = (int)ls.Slider(EmpireVOESettings.skillFloor, 0, 10);
            }

            ls.GapLine();

            // --- Encampment Recovery ---
            ls.CheckboxLabeled(
                "VOE_EnableEncampment".Translate(),
                ref EmpireVOESettings.enableEncampment,
                "VOE_EnableEncampmentDesc".Translate());

            if (EmpireVOESettings.enableEncampment)
            {
                ls.Label("  " + "VOE_EncampmentRange".Translate() + ": " + EmpireVOESettings.encampmentRange);
                EmpireVOESettings.encampmentRange = (int)ls.Slider(EmpireVOESettings.encampmentRange, 5, 50);

                ls.Label("  " + "VOE_EncampmentHealRatePerLevel".Translate() + ": " + EmpireVOESettings.encampmentHealRatePerLevel.ToString("F3"));
                EmpireVOESettings.encampmentHealRatePerLevel = (float)System.Math.Round(ls.Slider(EmpireVOESettings.encampmentHealRatePerLevel, 0f, 0.1f), 3);
            }

            ls.GapLine();

            // --- Outpost -> Settlement Conversion ---
            ls.CheckboxLabeled(
                "VOE_EnableOutpostConversion".Translate(),
                ref EmpireVOESettings.enableOutpostConversion,
                "VOE_EnableOutpostConversionDesc".Translate());

            if (EmpireVOESettings.enableOutpostConversion)
            {
                ls.CheckboxLabeled(
                    "  " + "VOE_ConvertOutpostPawns".Translate(),
                    ref EmpireVOESettings.convertOutpostPawns,
                    "VOE_ConvertOutpostPawnsDesc".Translate());

                ls.Label("  " + "VOE_ReducedFoundingCostFactor".Translate() + ": " + EmpireVOESettings.reducedFoundingCostFactor.ToString("P0"));
                EmpireVOESettings.reducedFoundingCostFactor = (float)System.Math.Round(ls.Slider(EmpireVOESettings.reducedFoundingCostFactor, 0.1f, 1f), 2);

                ls.Label("  " + "VOE_TownFlatAdditive".Translate() + ": " + EmpireVOESettings.townFlatAdditive.ToString("F2"));
                EmpireVOESettings.townFlatAdditive = (float)System.Math.Round(ls.Slider(EmpireVOESettings.townFlatAdditive, 0f, 2f), 2);

                ls.CheckboxLabeled(
                    "  " + "VOE_EnableConversionDelay".Translate(),
                    ref EmpireVOESettings.enableConversionDelay,
                    "VOE_EnableConversionDelayDesc".Translate());

                if (EmpireVOESettings.enableConversionDelay)
                {
                    ls.Label("    " + "VOE_ConversionDelayDays".Translate() + ": " + EmpireVOESettings.conversionDelayDays);
                    EmpireVOESettings.conversionDelayDays = (int)ls.Slider(EmpireVOESettings.conversionDelayDays, 0, 60);
                }

                ls.CheckboxLabeled(
                    "  " + "VOE_RequireOutpost".Translate(),
                    ref EmpireVOESettings.requireOutpostForSettlement,
                    "VOE_RequireOutpostDesc".Translate());
            }

            ls.GapLine();

            // --- Road Integration ---
            bool prevRoads = EmpireVOESettings.enableRoads;
            ls.CheckboxLabeled(
                "VOE_EnableRoads".Translate(),
                ref EmpireVOESettings.enableRoads,
                "VOE_EnableRoadsDesc".Translate());
            if (prevRoads != EmpireVOESettings.enableRoads)
            {
                // Force the road network to recompute so outpost nodes are added/removed.
                FindFC.RoadBuilder?.FlagUpdateRoadQueues();
            }

            ls.GapLine();

            // --- Faction-wide Outposts main tab (Empire+ window) ---
            bool prevMainTab = EmpireVOESettings.enableOutpostMainTab;
            ls.CheckboxLabeled(
                "VOE_EnableOutpostMainTab".Translate(),
                ref EmpireVOESettings.enableOutpostMainTab,
                "VOE_EnableOutpostMainTabDesc".Translate());
            if (prevMainTab != EmpireVOESettings.enableOutpostMainTab)
                OutpostMainTab.SetRegistered(EmpireVOESettings.enableOutpostMainTab);

            ls.Gap(12f);
            if (ls.ButtonText("VOE_OpenPatchNotes".Translate()))
                Find.WindowStack.Add(new PatchNotesDisplayWindow("matathias.empirerefvoe", "VOE_PatchTitle".Translate()));

            integrationContentHeight = ls.CurHeight;
            ls.End();
            Widgets.EndScrollView();
        }

        private void DoTownsTab(Rect rect)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, townsContentHeight);
            Widgets.BeginScrollView(rect, ref townsScroll, viewRect);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);

            ls.Label("VOE_TownRequirementsHeader".Translate());
            ls.GapLine();

            ls.Label("  " + "VOE_TownMinTotal".Translate() + ": " + EmpireVOESettings.townMinTotal);
            EmpireVOESettings.townMinTotal = (int)ls.Slider(EmpireVOESettings.townMinTotal, 0, 20);

            ls.Label("  " + "VOE_TownMinSettlements".Translate() + ": " + EmpireVOESettings.townMinSettlements);
            EmpireVOESettings.townMinSettlements = (int)ls.Slider(EmpireVOESettings.townMinSettlements, 0, 20);

            ls.Label("  " + "VOE_TownMinOutposts".Translate() + ": " + EmpireVOESettings.townMinOutposts);
            EmpireVOESettings.townMinOutposts = (int)ls.Slider(EmpireVOESettings.townMinOutposts, 0, 20);

            ls.Label("  " + "VOE_TownRange".Translate() + ": " + EmpireVOESettings.townRange);
            EmpireVOESettings.townRange = (int)ls.Slider(EmpireVOESettings.townRange, 1, 50);

            ls.Gap();

            ls.CheckboxLabeled(
                "VOE_TownExcludeTowns".Translate(),
                ref EmpireVOESettings.townExcludeTowns,
                "VOE_TownExcludeTownsDesc".Translate());

            ls.GapLine();

            ls.Label("VOE_TownXenoPureHeader".Translate());
            ls.Label("  " + "VOE_TownXenoPureChanceDesc".Translate());
            ls.Label("  " + "VOE_TownXenoPureChance".Translate() + ": " + EmpireVOESettings.townXenotypePureChanceMult.ToString("P0"));
            EmpireVOESettings.townXenotypePureChanceMult = (float)System.Math.Round(ls.Slider(EmpireVOESettings.townXenotypePureChanceMult, 0.05f, 1f), 2);

            townsContentHeight = ls.CurHeight;
            ls.End();
            Widgets.EndScrollView();
        }
    }
}
