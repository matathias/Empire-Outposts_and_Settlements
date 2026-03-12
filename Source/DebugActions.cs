using System.Collections.Generic;
using FactionColonies;
using LudeonTK;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    public static class VOEDebugActions
    {
        [DebugAction("Empire - VOE", "Attack VOE Outpost", allowedGameStates = AllowedGameStates.Playing)]
        private static void AttackVOEOutpost()
        {
            IReadOnlyList<IRaidTarget> targets = RaidTargetRegistry.Targets;
            if (targets.Count == 0)
            {
                Messages.Message("No registered raid targets.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (IRaidTarget target in targets)
            {
                IRaidTarget t = target;
                list.Add(new DebugMenuOption(t.Name, DebugMenuOptionMode.Action, delegate
                {
                    Faction enemyFaction = Find.FactionManager.RandomEnemyFaction();
                    if (enemyFaction == null)
                    {
                        Messages.Message("No enemy faction found.", MessageTypeDefOf.RejectInput);
                        return;
                    }

                    List<DebugMenuOption> levelList = new List<DebugMenuOption>();
                    for (int level = 1; level <= 10; level++)
                    {
                        int chosenLevel = level;
                        levelList.Add(new DebugMenuOption("Level " + chosenLevel, DebugMenuOptionMode.Action, delegate
                        {
                            double efficiency;
                            militaryForce.GetMilitaryLevelAndEfficiencyFromTechLevel(
                                enemyFaction.def.techLevel, out double _, out efficiency);
                            militaryForce attackingForce = new militaryForce(chosenLevel, efficiency, null, enemyFaction);
                            LogUtil.MessageForce("Debug - Attack VOE Outpost - " + t.Name
                                + " (level " + chosenLevel + ", efficiency " + efficiency + ")");
                            MilitaryUtilFC.AttackRaidTarget(attackingForce, t, enemyFaction);
                        }));
                    }
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(levelList));
                }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }
    }
}
