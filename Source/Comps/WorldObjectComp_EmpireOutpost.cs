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
            if (EmpireVOESettings.disableIntegration) return;
            Register();
        }

        public override void PostPostRemove()
        {
            base.PostPostRemove();
            Unregister();
            if (parent is Outpost_Encampment)
                EncampmentCache.Invalidate();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (EmpireVOESettings.disableIntegration) yield break;

            FCEvent evt = FindAttackEvent();
            if (evt is null) yield break;

            yield return OutpostDefenderGizmo.CreateGizmo(Outpost);
        }

        public override string CompInspectStringExtra()
        {
            if (EmpireVOESettings.disableIntegration) return null;
            if (raidTarget is null) return null;

            List<string> lines = new List<string>();

            // Military level
            lines.Add("VOE_MilitaryLevel".Translate(raidTarget.MilitaryLevel * FCSettings.defenderAdvantage));

            // Science link status
            if (parent is Outpost_Science science)
            {
                List<string> linkedNames = GetLinkedSettlementNames(science);
                if (linkedNames.Count > 0)
                    lines.Add("VOE_LinkedTo".Translate(string.Join(", ", linkedNames)));
                else
                    lines.Add("VOE_NotLinked".Translate());
            }

            // Defensive outpost status
            WorldObjectComp_EmpireDefensive defComp = parent.GetComponent<WorldObjectComp_EmpireDefensive>();
            if (defComp is object)
            {
                lines.Add(defComp.GetStatusInspectString(raidTarget.IsUnderAttack));
            }

            // Financing info (reverse lookup)
            List<string> financedNames = GetFinancedSettlementNames();
            if (financedNames.Count > 0)
                lines.Add("VOE_FinancingFor".Translate(string.Join(", ", financedNames)));

            // Delivery source info (reverse lookup)
            List<string> deliveryNames = GetDeliverySourceNames();
            if (deliveryNames.Count > 0)
                lines.Add("VOE_ReceivingTaxes".Translate(string.Join(", ", deliveryNames)));

            return string.Join("\n", lines);
        }

        // --- Registration ---

        internal void Register()
        {
            if (raidTarget is object) return;
            raidTarget = new OutpostRaidTarget(Outpost);
            RaidTargetRegistry.Register(raidTarget);
            if (parent is Outpost_Encampment)
                EncampmentCache.Invalidate();
        }

        internal void Unregister()
        {
            if (raidTarget is null) return;
            RaidTargetRegistry.Unregister(raidTarget);
            raidTarget = null;
        }

        // --- Static helpers for runtime toggle ---

        public static void UnregisterAll()
        {
            foreach (Outpost outpost in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
            {
                WorldObjectComp_EmpireOutpost comp = outpost.GetComponent<WorldObjectComp_EmpireOutpost>();
                comp?.Unregister();

                WorldObjectComp_EmpireDefensive defComp = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
                defComp?.Unregister();
            }
            SilverPaymentRegistry.Unregister(OutpostFinancer.Instance);
            ThreatScalingRegistry.Unregister(VOECompatInit.ThreatContributor);
        }

        public static void ReregisterAll()
        {
            if (Find.World is null) return;
            SilverPaymentRegistry.Register(OutpostFinancer.Instance);
            ThreatScalingRegistry.Register(VOECompatInit.ThreatContributor);
            foreach (Outpost outpost in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
            {
                WorldObjectComp_EmpireOutpost comp = outpost.GetComponent<WorldObjectComp_EmpireOutpost>();
                comp?.Register();

                WorldObjectComp_EmpireDefensive defComp = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
                defComp?.Register();
            }
        }

        // --- Private helpers ---

        private FCEvent FindAttackEvent()
        {
            IReadOnlyList<FCEvent> attackEvents = FactionCache.FactionComp?.GetEventsByDef(FCEventDefOf.settlementBeingAttacked);
            if (attackEvents is null) return null;
            for (int i = 0; i < attackEvents.Count; i++)
            {
                if (attackEvents[i].settlementFCDefending == parent)
                    return attackEvents[i];
            }
            return null;
        }

        private List<string> GetLinkedSettlementNames(Outpost_Science science)
        {
            List<string> names = new List<string>();
            FactionFC faction = FactionCache.FactionComp;
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
            FactionFC faction = FactionCache.FactionComp;
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
            FactionFC faction = FactionCache.FactionComp;
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
