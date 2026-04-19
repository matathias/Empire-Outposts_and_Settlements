using System.Collections.Generic;
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
            WorldObjectComp_SettlementSpecialists comp = settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null)
            {
                VOELog.Warning($"VOESpecialistsCompat: Settlement {settlement.Name} has no SettlementSpecialists comp. Pawns will not be assigned.");
                return;
            }

            foreach (Pawn pawn in pawns)
            {
                SkillRecord best = BestNonDisabledSkill(pawn);
                if (best != null && best.Level >= SpecialistSkillThreshold
                    && comp.SpecialistCount < comp.MaxSpecialists)
                {
                    SpecialistRoleDef bestRole = FindBestRole(pawn);
                    // AssignSpecialist handles per-role maxPerSettlement caps internally
                    comp.AssignSpecialist(pawn, bestRole);
                }
                else
                {
                    // null role = Resident
                    comp.AssignSpecialist(pawn, null);
                }
            }
        }

        /// <summary>
        /// Finds the SpecialistRoleDef with the highest weighted skill score for this pawn.
        /// Returns null if no roles are defined (pawn will be assigned as Resident).
        /// </summary>
        private static SpecialistRoleDef FindBestRole(Pawn pawn)
        {
            if (pawn.skills is null) return null;

            SpecialistRoleDef bestRole = null;
            float bestScore = -1f;

            foreach (SpecialistRoleDef role in DefDatabase<SpecialistRoleDef>.AllDefsListForReading)
            {
                if (role.skillWeights is null || role.skillWeights.Count == 0) continue;

                float score = 0f;
                foreach (SkillWeight sw in role.skillWeights)
                {
                    SkillRecord record = pawn.skills.GetSkill(sw.skill);
                    if (record is null || record.TotallyDisabled) continue;
                    score += record.Level * sw.weight;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRole = role;
                }
            }

            return bestRole;
        }

        private static SkillRecord BestNonDisabledSkill(Pawn pawn)
        {
            if (pawn.skills?.skills is null) return null;

            SkillRecord best = null;
            foreach (SkillRecord record in pawn.skills.skills)
            {
                if (record.TotallyDisabled) continue;
                if (best is null || record.Level > best.Level)
                {
                    best = record;
                }
            }
            return best;
        }
    }
}
