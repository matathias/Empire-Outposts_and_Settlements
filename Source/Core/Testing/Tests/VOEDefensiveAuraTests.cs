using FactionColonies;

namespace EmpireVOE
{
    /// <summary>
    /// Coverage for the passive defensive aura (<see cref="WorldObjectComp_DefensiveAura"/> and
    /// <see cref="DefensiveAuraRaidWeightProvider"/>). These exercise the early-return guards with bare
    /// instances, so they mutate nothing and are non-destructive. A live in-range power test lives in the
    /// destructive suite.
    /// </summary>
    public static class VOEDefensiveAuraTests
    {
        [EmpireTest("VOE.Aura")]
        public static void RaidWeight_NullSettlement_IsOne()
        {
            TestAssert.AreEqual(1.0, DefensiveAuraRaidWeightProvider.Instance.GetSettlementRaidWeight(null, null));
        }

        [EmpireTest("VOE.Aura")]
        public static void StatModifier_WrongStat_ReturnsIdentity()
        {
            WorldObjectComp_DefensiveAura comp = new WorldObjectComp_DefensiveAura();
            // Any stat other than militaryBaseLevel returns the stat's identity without touching the (null) parent.
            FCStatDef other = FCStatDefOf.mercHealRateMultiplier;
            TestAssert.AreEqual(other.IdentityValue, comp.GetStatModifier(other));
        }
    }
}
