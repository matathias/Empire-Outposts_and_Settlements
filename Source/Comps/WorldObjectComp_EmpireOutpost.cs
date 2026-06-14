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

        private Outpost Outpost => (Outpost)parent;

        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
            // Road integration is independent of the Military toggle: flag the road
            // network to recompute so this outpost is picked up as a road node.
            if (EmpireVOESettings.RoadsActive) FindFC.RoadBuilder?.FlagUpdateRoadQueues();
            if (!EmpireVOESettings.MilitaryActive) return;
            Register();
        }

        public override void PostPostRemove()
        {
            base.PostPostRemove();
            Unregister();
            // Drop this outpost's road node; cached edges are invalidated automatically.
            if (EmpireVOESettings.RoadsActive) FindFC.RoadBuilder?.FlagUpdateRoadQueues();
            if (parent is Outpost_Encampment)
                EncampmentCache.Invalidate();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!EmpireVOESettings.MilitaryActive) yield break;

            FCEvent evt = FindAttackEvent();
            if (evt is null) yield break;

            yield return new Command_Action
            {
                defaultLabel = "VOE_ChangeDefender".Translate(),
                defaultDesc = "VOE_ChangeDefenderDesc".Translate(),
                icon = TexLoad.iconMilitary,
                action = () => Find.WindowStack.Add(new Dialog_DefendSettlement(evt))
            };
        }

        public override string CompInspectStringExtra()
        {
            if (EmpireVOESettings.disableIntegration) return null;

            List<string> lines = new List<string>();

            // Military level + defensive status
            if (EmpireVOESettings.enableMilitary && raidTarget is object)
            {
                lines.Add("VOE_MilitaryLevel".Translate(raidTarget.MilitaryLevel * FCSettings.defenderAdvantage));

                WorldObjectComp_EmpireDefensive defComp = parent.GetComponent<WorldObjectComp_EmpireDefensive>();
                if (defComp is object)
                    lines.Add(defComp.GetStatusInspectString(raidTarget.IsUnderAttack));
            }

            // Science link status
            if (EmpireVOESettings.enableScienceLink && parent is Outpost_Science science)
            {
                List<string> linkedNames = GetLinkedSettlementNames(science);
                if (linkedNames.Count > 0)
                    lines.Add("VOE_LinkedTo".Translate(string.Join(", ", linkedNames)));
                else
                    lines.Add("VOE_NotLinked".Translate());
            }

            // Financing info (reverse lookup)
            if (EmpireVOESettings.enableFinancing)
            {
                List<string> financedNames = GetFinancedSettlementNames();
                if (financedNames.Count > 0)
                    lines.Add("VOE_FinancingFor".Translate(string.Join(", ", financedNames)));
            }

            // Delivery source info (reverse lookup)
            if (EmpireVOESettings.enableDelivery)
            {
                List<string> deliveryNames = GetDeliverySourceNames();
                if (deliveryNames.Count > 0)
                    lines.Add("VOE_ReceivingTaxes".Translate(string.Join(", ", deliveryNames)));
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
            EmpireRegistry.Unregister(VOECompatInit.ThreatContributor);
        }

        public static void ReregisterAll()
        {
            if (Find.World is null) return;
            EmpireRegistry.Register(OutpostFinancer.Instance);
            EmpireRegistry.Register(VOECompatInit.ThreatContributor);
            if (EmpireVOESettings.enableMilitary)
                ToggleMilitary(true);
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

        private List<string> GetLinkedSettlementNames(Outpost_Science science)
        {
            List<string> names = new List<string>();
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return names;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_ScienceLink link = s.GetComponent<WorldObjectComp_ScienceLink>();
                if (link?.linkedOutpost == science)
                    names.Add(s.Name);
            }
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
