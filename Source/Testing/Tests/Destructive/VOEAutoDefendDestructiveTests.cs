using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /* DESTRUCTIVE: exercises the defensive auto-defender against a live (or transient) Outpost_Defensive.
       OnDefenseComplete sets a real cooldown on the comp. Skips cleanly when no defensive outpost (and no
       def for one) exists. Created outposts are NOT cleaned up. */
    public static class VOEAutoDefendDestructiveTests
    {
        /// <summary>A live player Outpost_Defensive, or a freshly created transient one with the
        /// Empire defensive comp. Null if neither is possible (caller skips).</summary>
        private static Outpost_Defensive GetDefensiveOutpost()
        {
            Outpost_Defensive existing = Find.WorldObjects.AllWorldObjects
                .OfType<Outpost_Defensive>()
                .FirstOrDefault(o => o.Faction == Faction.OfPlayer);
            if (existing is object) return existing;

            WorldObjectDef def = DefDatabase<WorldObjectDef>.AllDefsListForReading.FirstOrDefault(d =>
                d.worldObjectClass is object && typeof(Outpost_Defensive).IsAssignableFrom(d.worldObjectClass));
            if (def is null) return null;

            return VOEDestructiveTestUtil.CreateTransientOutpost(def) as Outpost_Defensive;
        }

        [EmpireDestructiveTest("VOE.Destructive.Defense")]
        public static void DefensiveAutoDefender_Lifecycle_TogglesBusyAndCooldown()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost_Defensive outpost = GetDefensiveOutpost();
            if (outpost is null) TestAssert.Skip("No defensive outpost available");
            WorldObjectComp_EmpireDefensive comp = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
            if (comp is null) TestAssert.Skip("Defensive outpost has no WorldObjectComp_EmpireDefensive");

            comp.ClearCooldown();
            var defender = new DefensiveAutoDefender(outpost);
            TestAssert.IsFalse(defender.Busy, "A fresh defender should not be busy");

            TestAssert.DoesNotThrow(() => defender.OnDefenseStarted(outpost), "OnDefenseStarted threw");
            TestAssert.IsTrue(defender.Busy, "Defender should be busy after OnDefenseStarted");

            TestAssert.DoesNotThrow(() => defender.OnDefenseComplete(true, new BattleResult()), "OnDefenseComplete threw");
            TestAssert.IsFalse(defender.Busy, "Defender should not be busy after OnDefenseComplete");
            TestAssert.IsTrue(comp.IsOnCooldown, "Completing a defense should put the outpost on cooldown");

            DestructiveTestUtil.AssertEmpireInvariants(f, "DefensiveAutoDefender_Lifecycle");
        }

        [EmpireDestructiveTest("VOE.Destructive.Defense")]
        public static void DefensiveAutoDefender_MilitaryLevelAndForce_Sane()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost_Defensive outpost = GetDefensiveOutpost();
            if (outpost is null) TestAssert.Skip("No defensive outpost available");
            if (outpost.PawnCount == 0) TestAssert.Skip("Defensive outpost has no pawns");

            var defender = new DefensiveAutoDefender(outpost);
            TestAssert.GreaterThan(defender.MilitaryLevel, 0, "A manned outpost should have a positive military level");
            TestAssert.DoesNotThrow(() => defender.CreateDefendingForce(), "CreateDefendingForce threw");

            DestructiveTestUtil.AssertEmpireInvariants(f, "DefensiveAutoDefender_MilitaryLevel");
        }
    }
}
