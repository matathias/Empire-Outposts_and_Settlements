using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Provides a "Change Defender" gizmo on outposts when they are under attack.
    /// Cannot use MilitaryUtilFC.ChangeDefendingMilitaryForce() because it calls
    /// WorldObjectAt<WorldSettlementFC>(evt.location) which returns null for outpost tiles.
    /// </summary>
    public static class OutpostDefenderGizmo
    {
        public static Command_Action CreateGizmo(Outpost outpost)
        {
            return new Command_Action
            {
                defaultLabel = "VOE_ChangeDefender".Translate(),
                defaultDesc = "VOE_ChangeDefenderDesc".Translate(),
                icon = TexLoad.iconMilitary,
                action = delegate { ShowDefenderMenu(outpost); }
            };
        }

        private static void ShowDefenderMenu(Outpost outpost)
        {
            FactionFC faction = FactionCache.FactionComp;
            if (faction is null) return;

            FCEvent evt = faction.events.FirstOrDefault(e =>
                e.def == FCEventDefOf.settlementBeingAttacked
                && e.settlementFCDefending == outpost);
            if (evt is null) return;

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (WorldSettlementFC settlement in faction.settlements)
            {
                if (settlement.MilitaryComp is null) continue;
                if (!settlement.MilitaryComp.IsMilitaryValid()) continue;
                if (settlement.MilitaryComp.IsMilitaryBusy()) continue;

                WorldSettlementFC s = settlement;
                int level = s.MilitaryComp.settlementMilitaryLevel;
                options.Add(new FloatMenuOption(
                    s.Name + " " + "ShortMilitary".Translate() + " " + level +
                        " - " + "FCAvailable".Translate() + ": " +
                        (!s.MilitaryComp.IsMilitaryBusySilent()).ToString(),
                    delegate
                    {
                        // Return previous defender's military if it was an Empire settlement
                        if (evt.militaryForceDefending != null
                            && evt.militaryForceDefending.homeSettlement != null)
                        {
                            evt.militaryForceDefending.homeSettlement.MilitaryComp
                                ?.ReturnMilitary(false);
                        }

                        // Create new defending force from the selected settlement
                        evt.militaryForceDefending =
                            militaryForce.CreateMilitaryForceFromSettlement(s);
                        evt.externalDefenderSource = null;

                        // Send the settlement's military to defend the outpost
                        s.MilitaryComp.SendMilitary(
                            outpost.Tile,
                            MilitaryJobDefOf.DefendFriendlySettlement,
                            -1,
                            evt.militaryForceAttackingFaction);

                        string outpostName = outpost.Name ?? outpost.def.label;
                        Messages.Message(
                            "VOE_DefenderChanged".Translate(s.Name, outpostName),
                            MessageTypeDefOf.NeutralEvent);
                    }));
            }

            if (options.Count == 0)
                options.Add(new FloatMenuOption("NoValidMilitaries".Translate(), null));

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
