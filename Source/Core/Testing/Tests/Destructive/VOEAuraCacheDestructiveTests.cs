using System.Linq;
using FactionColonies;
using RimWorld;
using Verse;
using VOE;

namespace EmpireVOE
{
    /* DESTRUCTIVE: builds the defensive-aura and encampment cache entries against live faction/world state.
       Entry construction is read-only, but it needs a real settlement + faction, so it rides the destructive
       tier (RequireFaction + invariants). The OutpostBonus formula check creates and removes a transient
       defensive outpost. The filtering checks assert the layer/range/eligibility contract over whatever
       outposts the live world holds — trivially satisfied (no assertions) when there are none nearby. */
    public static class VOEAuraCacheDestructiveTests
    {
        /// <summary>
        /// The single-source aura formula: one outpost contributes militaryLevel * defensiveAuraLevelFactor
        /// to each nearby settlement (always weaker than deploying the garrison).
        /// </summary>
        [EmpireDestructiveTest("VOE.Destructive.Aura")]
        public static void DefensiveAura_OutpostBonus_MatchesFormula()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldObjectDef def = VOEDestructiveTestUtil.FindDefensiveOutpostDef();
            if (def is null) TestAssert.Skip("No defensive outpost def available");
            var outpost = VOEDestructiveTestUtil.CreateTransientOutpost(def) as Outpost_Defensive;
            if (outpost is null) TestAssert.Skip("Could not create a transient defensive outpost");

            try
            {
                double expected = OutpostMilitaryUtil.MilitaryLevel(outpost) * EmpireVOESettings.defensiveAuraLevelFactor;
                TestAssert.AreEqual(expected, DefensiveAuraEntry.OutpostBonus(outpost), 0.001,
                    "OutpostBonus should be militaryLevel * defensiveAuraLevelFactor");
            }
            finally
            {
                VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "DefensiveAura_OutpostBonus");
        }

        /// <summary>
        /// Every outpost the aura entry includes must satisfy the filter: same planet layer, within
        /// defensiveAuraRange, garrisoned, and standing a passive watch (ProvidesAura). The summed bonus must
        /// equal the per-outpost OutpostBonus over exactly those outposts.
        /// </summary>
        [EmpireDestructiveTest("VOE.Destructive.Aura")]
        public static void DefensiveAura_Entry_OnlyIncludesEligibleOutposts()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC settlement = f.settlements.FirstOrDefault();
            if (settlement is null) TestAssert.Skip("No Empire settlement available");

            var entry = new DefensiveAuraEntry(settlement);
            double sum = 0;
            foreach (Outpost_Defensive o in entry.outposts)
            {
                TestAssert.IsTrue(o.Tile.Layer == settlement.Tile.Layer, "Aura outpost must be on the settlement's layer");
                TestAssert.LessThanOrEqual(Find.WorldGrid.ApproxDistanceInTiles(o.Tile, settlement.Tile),
                    EmpireVOESettings.defensiveAuraRange, "Aura outpost must be within range");
                TestAssert.GreaterThan(o.PawnCount, 0, "Aura outpost must be garrisoned");

                WorldObjectComp_EmpireDefensive comp = o.GetComponent<WorldObjectComp_EmpireDefensive>();
                if (comp is object)
                    TestAssert.IsTrue(comp.ProvidesAura, "A committed garrison should not project an aura");

                sum += DefensiveAuraEntry.OutpostBonus(o);
            }
            TestAssert.AreEqual(sum, entry.militaryLevelBonus, 0.001,
                "Entry bonus should equal the sum of per-outpost OutpostBonus values");

            DestructiveTestUtil.AssertEmpireInvariants(f, "DefensiveAura_EntryFilter");
        }

        /// <summary>
        /// Every encampment the entry includes must satisfy the filter: same layer, within encampmentRange,
        /// garrisoned. The summed heal bonus must equal the per-encampment contribution over exactly those.
        /// </summary>
        [EmpireDestructiveTest("VOE.Destructive.Aura")]
        public static void Encampment_Entry_OnlyIncludesEligibleEncampments()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC settlement = f.settlements.FirstOrDefault();
            if (settlement is null) TestAssert.Skip("No Empire settlement available");

            var entry = new EncampmentCacheEntry(settlement);
            double sum = 0;
            foreach (EncampmentData d in entry.encampments)
            {
                TestAssert.GreaterThan(d.encampment.PawnCount, 0, "Encampment must be garrisoned");
                TestAssert.IsTrue(d.encampment.Tile.Layer == settlement.Tile.Layer,
                    "Encampment must be on the settlement's layer");
                TestAssert.LessThanOrEqual(Find.WorldGrid.ApproxDistanceInTiles(d.encampment.Tile, settlement.Tile),
                    EmpireVOESettings.encampmentRange, "Encampment must be within range");
                sum += d.healRateContribution;
            }
            TestAssert.AreEqual(sum, entry.totalHealRateBonus, 0.001,
                "Entry heal bonus should equal the sum of per-encampment contributions");

            DestructiveTestUtil.AssertEmpireInvariants(f, "Encampment_EntryFilter");
        }
    }
}
