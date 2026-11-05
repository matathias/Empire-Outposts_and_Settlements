using FactionColonies.Specialists;

namespace EmpireVOE.Specialists;

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
        OutpostConversionUtil.SpecialistsCallback = AssignTownPawns;
        VOELog.MessageForce("EmpireVOE - Specialists compat loaded.");
    }

    /// <summary>
    /// Assigns the outpost pawns as specialists/residents. Returns the pawns it did NOT place so
    /// the caller can fold them into the world instead of losing them.
    /// </summary>
    private static List<Pawn> AssignTownPawns(WorldSettlementFC settlement, List<Pawn> pawns)
    {
        WorldObjectComp_SettlementSpecialists comp = settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
        if (comp is null)
        {
            VOELog.Warning($"VOESpecialistsCompat: Settlement {settlement.Name} has no SettlementSpecialists comp. Pawns will be folded into the world instead.");
            return new List<Pawn>(pawns); // hand every pawn back so none is orphaned
        }

        foreach (Pawn pawn in pawns)
        {
            SkillRecord best = BestNonDisabledSkill(pawn);
            if (best is not null && best.Level >= SpecialistSkillThreshold
                && comp.SpecialistCount < comp.MaxSpecialists)
            {
                SpecialistRoleDef bestRole = FindBestRole(pawn);
                // AssignSpecialist rejects (without placing) when the role is at its per-settlement
                // cap, which would orphan the pawn. Fall back to Resident (null role, always placed).
                if (comp.RoleAtCap(bestRole))
                    bestRole = null;
                comp.AssignSpecialist(pawn, bestRole);
            }
            else
            {
                // null role = Resident
                comp.AssignSpecialist(pawn, null);
            }
        }
        return []; // every pawn was placed as a specialist or resident
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
