using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using FactionColonies.Specialists;
using RimWorld;
using Verse;

namespace EmpireVOE.Specialists
{
    /// <summary>
    /// Loaded only when Matathias.Empire.Specialists is active (via LoadFolders.xml).
    /// Registers a callback with the main EmpireVOE assembly so that Town pawns
    /// are assigned as specialists/residents instead of converted into flat skill bonuses.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VOESpecialistsCompat
    {
        private const int SpecialistSkillThreshold = 8;

        static VOESpecialistsCompat()
        {
            TownConversionHandler.SpecialistsCallback = AssignTownPawns;
            VOELog.MessageForce("EmpireVOE - Specialists compat loaded.");
        }

        private static void AssignTownPawns(WorldSettlementFC settlement, List<Pawn> pawns)
        {
            WorldObjectComp_SettlementSpecialists comp =
                settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp == null)
            {
                VOELog.Warning("VOESpecialistsCompat: Settlement " + settlement.Name +
                    " has no SettlementSpecialists comp. Pawns will not be assigned.");
                return;
            }

            foreach (Pawn pawn in pawns)
            {
                SpecialistRole role = SpecialistRole.Resident;

                SkillRecord best = BestNonDisabledSkill(pawn);
                if (best != null && best.Level >= SpecialistSkillThreshold
                    && comp.SpecialistCount < comp.MaxSpecialists)
                {
                    role = SpecialistRole.Specialist;
                }

                comp.AssignPawn(pawn, role);
            }
        }

        private static SkillRecord BestNonDisabledSkill(Pawn pawn)
        {
            if (pawn.skills == null || pawn.skills.skills == null) return null;

            SkillRecord best = null;
            foreach (SkillRecord record in pawn.skills.skills)
            {
                if (record.TotallyDisabled) continue;
                if (best == null || record.Level > best.Level)
                {
                    best = record;
                }
            }
            return best;
        }
    }
}
