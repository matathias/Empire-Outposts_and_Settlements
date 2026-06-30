using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_EmpireOutpost : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_EmpireOutpost()
        {
            compClass = typeof(WorldObjectComp_EmpireOutpost);
        }
    }

    /// <summary>
    /// Core Empire integration comp for all VOE outposts. Handles raid target
    /// registration, inspect string, and the "Change Defender" gizmo.
    /// </summary>
    public class WorldObjectComp_EmpireOutpost : WorldObjectComp
    {
        [Unsaved] public OutpostRaidTarget raidTarget;

        // Reverse index of the resource link (the settlement-side authoritative list is
        // WorldObjectComp_ResourceLink.linkedOutposts). Runtime-only: maintained on link/unlink and rebuilt
        // on load — never serialized.
        [Unsaved] public WorldSettlementFC linkedSettlement;

        // Per-outpost opt-in for road building. Seeded from EmpireVOESettings.roadsDefaultOn on fresh
        // creation; toggled via the road gizmo. Honored by VOERoadNodeProvider.
        public bool buildRoads = true;

        private Outpost Outpost => (Outpost)parent;

        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
            // Fresh outpost only: Initialize also runs during LoadingVars, so guard on Scribe.mode to seed
            // the road opt-in from the setting only for newly created outposts. On load, PostExposeData
            // restores the saved value instead.
            if (Scribe.mode == LoadSaveMode.Inactive)
                buildRoads = EmpireVOESettings.roadsDefaultOn;
            // Road integration is independent of the Military toggle: flag the road
            // network to recompute so this outpost is picked up as a road node.
            if (EmpireVOESettings.RoadsActive) FindFC.RoadBuilder?.FlagUpdateRoadQueues();
            if (!EmpireVOESettings.MilitaryActive) return;
            Register();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref buildRoads, "voeBuildRoads", true);
        }

        public override void PostPostRemove()
        {
            base.PostPostRemove();
            Unregister();
            // Drop this outpost's road node; cached edges are invalidated automatically.
            if (EmpireVOESettings.RoadsActive) FindFC.RoadBuilder?.FlagUpdateRoadQueues();
            if (parent is Outpost_Encampment)
                EncampmentCache.Invalidate();
            if (parent is Outpost_Defensive)
                DefensiveAuraCache.Invalidate();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            // Per-outpost road-building opt-in toggle
            if (EmpireVOESettings.RoadsActive && parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "FCVOE_BuildRoads".Translate(),
                    defaultDesc = "FCVOE_BuildRoadsDesc".Translate(),
                    icon = VOETex.Trail,
                    isActive = () => buildRoads,
                    toggleAction = () =>
                    {
                        buildRoads = !buildRoads;
                        // Recompute the MST so this outpost's road node is added/dropped.
                        FindFC.RoadBuilder?.FlagUpdateRoadQueues();
                    }
                };
            }

            // Military "Change Defender" gizmo
            if (EmpireVOESettings.MilitaryActive)
            {
                FCEvent evt = FindAttackEvent();
                if (evt is object)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "FCVOE_ChangeDefender".Translate(),
                        defaultDesc = "FCVOE_ChangeDefenderDesc".Translate(),
                        icon = TexLoad.iconMilitary,
                        action = () => Find.WindowStack.Add(new Dialog_DefendSettlement(evt))
                    };
                }
            }

            // Outpost -> Settlement conversion gizmo
            if (EmpireVOESettings.OutpostConversionActive && parent.Faction == Faction.OfPlayer)
            {
                List<WorldSettlementDef> types = OutpostConversionUtil.GetConvertibleTypes(Outpost);
                if (types.Count > 0)
                {
                    Command_Action convert = new Command_Action
                    {
                        defaultLabel = "FCVOE_ConvertToSettlement".Translate(),
                        defaultDesc = "FCVOE_ConvertToSettlementDesc".Translate(),
                        icon = TexCommand.Install,
                        action = () =>
                        {
                            Find.WindowStack.Add(new FCWindow_SettlementTypePicker(
                                types,
                                selected => OutpostConversionUtil.ConvertOutpost(Outpost, selected),
                                "FCVOE_ConvertPickType"));
                            // R&R compat (if loaded) docks a cost-preview window beside the picker.
                            OutpostConversionUtil.ConversionCostCompanionOpener?.Invoke(Outpost);
                        }
                    };
                    if (!OutpostConversionUtil.CanConvertNow(Outpost, out string convertReason))
                    {
                        convert.Disabled = true;
                        convert.disabledReason = convertReason;
                    }
                    yield return convert;
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (EmpireVOESettings.disableIntegration) return null;

            List<string> lines = new List<string>();

            // Military level + defensive status
            if (EmpireVOESettings.enableMilitary && raidTarget is object)
            {
                // Mirror the military tab's "Mil N • Def M" exactly (see MainTabWindow_Colony): raw
                // cost-curve level, plus the defender-advantage-adjusted defense power.
                int milLevel = raidTarget.MilitaryLevel;
                double defPower = Math.Round(milLevel * FCSettings.defenderAdvantage);
                lines.Add("FCVOE_MilitaryLevel".Translate(milLevel, defPower));

                WorldObjectComp_EmpireDefensive defComp = parent.GetComponent<WorldObjectComp_EmpireDefensive>();
                // Passive defensive aura (sits directly under the military level it stiffens for neighbors).
                if (defComp is object && defComp.IsProjectingAura)
                    lines.Add("FCVOE_InspectAuraActive".Translate(defComp.AuraMilitaryBonus.ToString("0.#")));
                // Combat efficiency is defensive-outpost-only (non-defensive outposts have no defender).
                if (defComp?.defender is object)
                    lines.Add("FCVOE_CombatEfficiency".Translate(defComp.defender.Efficiency.ToString("0.0#")));
                if (defComp is object)
                    lines.Add(defComp.GetStatusInspectString(raidTarget.IsUnderAttack));
            }

            // Resource link status (any production outpost that can feed a settlement's resource output)
            if (EmpireVOESettings.enableResourceLink && ResourceLinkUtil.IsLinkable(Outpost))
            {
                List<string> linkedNames = GetLinkedSettlementNames(Outpost);
                if (linkedNames.Count > 0)
                    lines.Add("FCVOE_LinkedTo".Translate(string.Join(", ", linkedNames)));
                else
                    lines.Add("FCVOE_NotLinked".Translate());
            }

            // Financing info (reverse lookup)
            if (EmpireVOESettings.enableFinancing)
            {
                List<string> financedNames = GetFinancedSettlementNames();
                if (financedNames.Count > 0)
                    lines.Add("FCVOE_FinancingFor".Translate(string.Join(", ", financedNames)));
            }

            // Delivery source info (reverse lookup)
            if (EmpireVOESettings.enableDelivery)
            {
                List<string> deliveryNames = GetDeliverySourceNames();
                if (deliveryNames.Count > 0)
                    lines.Add("FCVOE_ReceivingTaxes".Translate(string.Join(", ", deliveryNames)));
            }

            // Outpost -> Settlement conversion availability
            if (EmpireVOESettings.OutpostConversionActive && OutpostConversionUtil.GetConvertibleTypes(Outpost).Count > 0)
            {
                int remaining = OutpostConversionUtil.DelayDaysRemaining(Outpost);
                if (remaining > 0)
                    lines.Add("FCVOE_ConvertAvailableInDays".Translate(remaining.ToString()));
                else
                    lines.Add("FCVOE_ConvertReady".Translate());
            }

            if (lines.Count == 0) return null;
            return string.Join("\n", lines);
        }

        // --- Registration ---

        internal void Register()
        {
            if (raidTarget is object) return;
            raidTarget = new OutpostRaidTarget(Outpost);
            EmpireRegistry.Register(raidTarget);
            if (parent is Outpost_Encampment)
                EncampmentCache.Invalidate();
            if (parent is Outpost_Defensive)
                DefensiveAuraCache.Invalidate();
        }

        internal void Unregister()
        {
            if (raidTarget is null) return;
            EmpireRegistry.Unregister(raidTarget);
            raidTarget = null;
        }

        // --- Static helpers for runtime toggle ---

        public static void ToggleMilitary(bool enable)
        {
            if (Find.World is null) return;
            foreach (Outpost outpost in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
            {
                WorldObjectComp_EmpireOutpost comp = outpost.GetComponent<WorldObjectComp_EmpireOutpost>();
                if (enable) comp?.Register();
                else comp?.Unregister();

                WorldObjectComp_EmpireDefensive defComp = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
                if (enable) defComp?.Register();
                else defComp?.Unregister();
            }
        }

        public static void UnregisterAll()
        {
            ToggleMilitary(false);
            EmpireRegistry.Unregister(OutpostFinancer.Instance);
            OutpostMainTab.SetRegistered(false);
        }

        public static void ReregisterAll()
        {
            if (Find.World is null) return;
            EmpireRegistry.Register(OutpostFinancer.Instance);
            if (EmpireVOESettings.enableMilitary)
                ToggleMilitary(true);
            OutpostMainTab.SetRegistered(EmpireVOESettings.OutpostMainTabActive);
        }

        // --- Private helpers ---

        private FCEvent FindAttackEvent()
        {
            IReadOnlyList<FCEvent> attackEvents = FindFC.FactionComp?.GetEventsByDef(FCEventDefOf.settlementBeingAttacked);
            if (attackEvents is null) return null;
            for (int i = 0; i < attackEvents.Count; i++)
            {
                if (attackEvents[i].linkedOperation?.targetObject == parent)
                    return attackEvents[i];
            }
            return null;
        }

        private List<string> GetLinkedSettlementNames(Outpost outpost)
        {
            // An outpost links to at most one settlement; use the O(1) reverse index.
            List<string> names = new List<string>();
            WorldSettlementFC linked = ResourceLinkUtil.LinkedSettlementOf(outpost);
            if (linked is object) names.Add(linked.Name);
            return names;
        }

        private List<string> GetFinancedSettlementNames()
        {
            List<string> names = new List<string>();
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return names;
            Outpost outpost = Outpost;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                if (links?.financingOutpost == outpost)
                    names.Add(s.Name);
            }
            return names;
        }

        private List<string> GetDeliverySourceNames()
        {
            List<string> names = new List<string>();
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return names;
            Outpost outpost = Outpost;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_OutpostLinks links = s.GetComponent<WorldObjectComp_OutpostLinks>();
                if (links?.deliveryOutpost == outpost)
                    names.Add(s.Name);
            }
            return names;
        }
    }
}
