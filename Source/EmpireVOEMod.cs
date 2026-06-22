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
        // Power-outpost resource linking (VOEPowerGrid compat): scales watts->RTD_Power conversion
        public static float powerConversionMultiplier = 1f;

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
            Scribe_Values.Look(ref powerConversionMultiplier, "powerConversionMultiplier", 1f);

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
            return "FCVOE_Title".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            settingsTabs.Clear();
            settingsTabs.Add(new TabRecord("FCVOE_TabIntegration".Translate(), delegate { settingsTab = 0; }, settingsTab == 0));
            settingsTabs.Add(new TabRecord("FCVOE_TabTowns".Translate(), delegate { settingsTab = 1; }, settingsTab == 1));

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
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref integrationScroll, integrationContentHeight);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);
            Listing_StandardExtensions.ResetRowStripe();
            ls.maxOneColumn = true;

            // Master toggle
            bool prev = EmpireVOESettings.disableIntegration;
            ls.CheckboxLabeled(
                "FCVOE_DisableIntegration".Translate(),
                ref EmpireVOESettings.disableIntegration,
                "FCVOE_DisableIntegrationDesc".Translate());

            if (prev != EmpireVOESettings.disableIntegration)
            {
                if (EmpireVOESettings.disableIntegration)
                    WorldObjectComp_EmpireOutpost.UnregisterAll();
                else
                    WorldObjectComp_EmpireOutpost.ReregisterAll();
            }

            ls.CheckboxLabeled(
                "FCVOE_DebugLogging".Translate(),
                ref EmpireVOESettings.debugLogging,
                "FCVOE_DebugLoggingDesc".Translate());

            ls.GapLine();

            // --- Military ---
            bool prevMil = EmpireVOESettings.enableMilitary;
            ls.CheckboxLabeled(
                "FCVOE_EnableMilitary".Translate(),
                ref EmpireVOESettings.enableMilitary,
                "FCVOE_EnableMilitaryDesc".Translate());
            if (prevMil != EmpireVOESettings.enableMilitary && !EmpireVOESettings.disableIntegration)
            {
                WorldObjectComp_EmpireOutpost.ToggleMilitary(EmpireVOESettings.enableMilitary);
            }

            if (EmpireVOESettings.enableMilitary)
            {
                ls.CheckboxLabeled(
                    "  " + "FCVOE_EnableDefensiveAura".Translate(),
                    ref EmpireVOESettings.enableDefensiveAura,
                    "FCVOE_EnableDefensiveAuraDesc".Translate());

                if (EmpireVOESettings.enableDefensiveAura)
                {
                    int prevRange = EmpireVOESettings.defensiveAuraRange;
                    float prevFactor = EmpireVOESettings.defensiveAuraLevelFactor;

                    EmpireVOESettings.defensiveAuraRange = ls.SliderTextField("FCVOE_DefensiveAuraRange", "    " + "FCVOE_DefensiveAuraRange".Translate(), EmpireVOESettings.defensiveAuraRange, 5, 50);

                    EmpireVOESettings.defensiveAuraLevelFactor = ls.SliderTextField("FCVOE_DefensiveAuraLevelFactor", "    " + "FCVOE_DefensiveAuraLevelFactor".Translate(), EmpireVOESettings.defensiveAuraLevelFactor, 0.05f, 1f, 2);

                    ls.CheckboxLabeled(
                        "    " + "FCVOE_EnableDefensiveAuraDivert".Translate(),
                        ref EmpireVOESettings.enableDefensiveAuraDivert,
                        "FCVOE_EnableDefensiveAuraDivertDesc".Translate());

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
                "FCVOE_EnableDelivery".Translate(),
                ref EmpireVOESettings.enableDelivery,
                "FCVOE_EnableDeliveryDesc".Translate());

            ls.GapLine();

            // --- Financing ---
            ls.CheckboxLabeled(
                "FCVOE_EnableFinancing".Translate(),
                ref EmpireVOESettings.enableFinancing,
                "FCVOE_EnableFinancingDesc".Translate());

            ls.GapLine();

            // --- Resource Linking ---
            ls.CheckboxLabeled(
                "FCVOE_EnableResourceLink".Translate(),
                ref EmpireVOESettings.enableResourceLink,
                "FCVOE_EnableResourceLinkDesc".Translate());

            if (EmpireVOESettings.enableResourceLink)
            {
                EmpireVOESettings.resourceLinkRange = ls.SliderTextField("FCVOE_ResourceLinkRange", "  " + "FCVOE_ResourceLinkRange".Translate(), EmpireVOESettings.resourceLinkRange, 5, 50);

                EmpireVOESettings.additivePerLevel = ls.SliderTextField("FCVOE_AdditivePerLevel", "  " + "FCVOE_AdditivePerLevel".Translate(), EmpireVOESettings.additivePerLevel, 0.005f, 0.1f, 3);

                EmpireVOESettings.skillFloor = ls.SliderTextField("FCVOE_SkillFloor", "  " + "FCVOE_SkillFloor".Translate(), EmpireVOESettings.skillFloor, 0, 10);

                // Power-outpost conversion (only shown when VOE Power Outposts is installed)
                if (EmpireVOECompat.PowerOutpostsActive)
                {
                    EmpireVOESettings.powerConversionMultiplier = ls.SliderTextField("FCVOE_PowerConversionMultiplier", "  " + "FCVOE_PowerConversionMultiplier".Translate(), EmpireVOESettings.powerConversionMultiplier, 0.1f, 5f, 2);
                }
            }

            ls.GapLine();

            // --- Encampment Recovery ---
            ls.CheckboxLabeled(
                "FCVOE_EnableEncampment".Translate(),
                ref EmpireVOESettings.enableEncampment,
                "FCVOE_EnableEncampmentDesc".Translate());

            if (EmpireVOESettings.enableEncampment)
            {
                EmpireVOESettings.encampmentRange = ls.SliderTextField("FCVOE_EncampmentRange", "  " + "FCVOE_EncampmentRange".Translate(), EmpireVOESettings.encampmentRange, 5, 50);

                EmpireVOESettings.encampmentHealRatePerLevel = ls.SliderTextField("FCVOE_EncampmentHealRatePerLevel", "  " + "FCVOE_EncampmentHealRatePerLevel".Translate(), EmpireVOESettings.encampmentHealRatePerLevel, 0f, 0.1f, 3);
            }

            ls.GapLine();

            // --- Outpost -> Settlement Conversion ---
            ls.CheckboxLabeled(
                "FCVOE_EnableOutpostConversion".Translate(),
                ref EmpireVOESettings.enableOutpostConversion,
                "FCVOE_EnableOutpostConversionDesc".Translate());

            if (EmpireVOESettings.enableOutpostConversion)
            {
                ls.CheckboxLabeled(
                    "  " + "FCVOE_ConvertOutpostPawns".Translate(),
                    ref EmpireVOESettings.convertOutpostPawns,
                    "FCVOE_ConvertOutpostPawnsDesc".Translate());

                EmpireVOESettings.reducedFoundingCostFactor = ls.SliderTextField("FCVOE_ReducedFoundingCostFactor", "  " + "FCVOE_ReducedFoundingCostFactor".Translate(), EmpireVOESettings.reducedFoundingCostFactor, 0.1f, 1f, 2);

                EmpireVOESettings.townFlatAdditive = ls.SliderTextField("FCVOE_TownFlatAdditive", "  " + "FCVOE_TownFlatAdditive".Translate(), EmpireVOESettings.townFlatAdditive, 0f, 2f, 2);

                ls.CheckboxLabeled(
                    "  " + "FCVOE_EnableConversionDelay".Translate(),
                    ref EmpireVOESettings.enableConversionDelay,
                    "FCVOE_EnableConversionDelayDesc".Translate());

                if (EmpireVOESettings.enableConversionDelay)
                {
                    EmpireVOESettings.conversionDelayDays = ls.SliderTextField("FCVOE_ConversionDelayDays", "    " + "FCVOE_ConversionDelayDays".Translate(), EmpireVOESettings.conversionDelayDays, 0, 60);
                }

                ls.CheckboxLabeled(
                    "  " + "FCVOE_RequireOutpost".Translate(),
                    ref EmpireVOESettings.requireOutpostForSettlement,
                    "FCVOE_RequireOutpostDesc".Translate());
            }

            ls.GapLine();

            // --- Road Integration ---
            bool prevRoads = EmpireVOESettings.enableRoads;
            ls.CheckboxLabeled(
                "FCVOE_EnableRoads".Translate(),
                ref EmpireVOESettings.enableRoads,
                "FCVOE_EnableRoadsDesc".Translate());
            if (prevRoads != EmpireVOESettings.enableRoads)
            {
                // Force the road network to recompute so outpost nodes are added/removed.
                FindFC.RoadBuilder?.FlagUpdateRoadQueues();
            }

            ls.GapLine();

            // --- Faction-wide Outposts main tab (Empire+ window) ---
            bool prevMainTab = EmpireVOESettings.enableOutpostMainTab;
            ls.CheckboxLabeled(
                "FCVOE_EnableOutpostMainTab".Translate(),
                ref EmpireVOESettings.enableOutpostMainTab,
                "FCVOE_EnableOutpostMainTabDesc".Translate());
            if (prevMainTab != EmpireVOESettings.enableOutpostMainTab)
                OutpostMainTab.SetRegistered(EmpireVOESettings.enableOutpostMainTab);

            ls.Gap(12f);
            if (ls.ButtonText("FCVOE_OpenPatchNotes".Translate()))
                Find.WindowStack.Add(new PatchNotesDisplayWindow("matathias.empirerefvoe", "FCVOE_PatchTitle".Translate()));

            integrationContentHeight = ls.CurHeight;
            ls.End();
            ScrollUtil.EndScrollView();
        }

        private void DoTownsTab(Rect rect)
        {
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref townsScroll, townsContentHeight);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(viewRect);
            Listing_StandardExtensions.ResetRowStripe();
            ls.maxOneColumn = true;

            ls.Label("FCVOE_TownRequirementsHeader".Translate());
            ls.GapLine();

            EmpireVOESettings.townMinTotal = ls.SliderTextField("FCVOE_TownMinTotal", "  " + "FCVOE_TownMinTotal".Translate(), EmpireVOESettings.townMinTotal, 0, 20);

            EmpireVOESettings.townMinSettlements = ls.SliderTextField("FCVOE_TownMinSettlements", "  " + "FCVOE_TownMinSettlements".Translate(), EmpireVOESettings.townMinSettlements, 0, 20);

            EmpireVOESettings.townMinOutposts = ls.SliderTextField("FCVOE_TownMinOutposts", "  " + "FCVOE_TownMinOutposts".Translate(), EmpireVOESettings.townMinOutposts, 0, 20);

            EmpireVOESettings.townRange = ls.SliderTextField("FCVOE_TownRange", "  " + "FCVOE_TownRange".Translate(), EmpireVOESettings.townRange, 1, 50);

            ls.Gap();

            ls.CheckboxLabeled(
                "FCVOE_TownExcludeTowns".Translate(),
                ref EmpireVOESettings.townExcludeTowns,
                "FCVOE_TownExcludeTownsDesc".Translate());

            ls.GapLine();

            ls.Label("FCVOE_TownXenoPureHeader".Translate());
            ls.Label("  " + "FCVOE_TownXenoPureChanceDesc".Translate());
            EmpireVOESettings.townXenotypePureChanceMult = ls.SliderTextField("FCVOE_TownXenoPureChance", "  " + "FCVOE_TownXenoPureChance".Translate(), EmpireVOESettings.townXenotypePureChanceMult, 0.05f, 1f, 2);

            townsContentHeight = ls.CurHeight;
            ls.End();
            ScrollUtil.EndScrollView();
        }
    }
}
