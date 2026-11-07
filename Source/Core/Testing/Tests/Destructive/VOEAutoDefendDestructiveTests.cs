using VOE;

namespace EmpireVOE;

/* DESTRUCTIVE: exercises the defensive auto-defender against a live (or transient) Outpost_Defensive.
   OnDefenseComplete sets a real cooldown on the comp; the loss branch injures pawns (only ever on a
   transient outpost). The cooldown-expiry test moves the game tick counter and restores it. When the
   world holds no (manned) player defensive outpost, a transient one is created. Created outposts are
   NOT cleaned up unless a test made a pure-probe transient, which it removes itself. */
public static class VOEAutoDefendDestructiveTests
{
    [EmpireDestructiveTest("VOE.Destructive.Defense")]
    public static void DefensiveAutoDefender_Lifecycle_TogglesBusyAndCooldown()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        Outpost_Defensive outpost = VOEDestructiveTestUtil.EnsureDefensiveOutpost();
        WorldObjectComp_EmpireDefensive comp = RequireDefensiveComp(outpost);

        comp.ClearCooldown();
        var defender = new DefensiveAutoDefender(outpost);
        TestAssert.IsFalse(defender.Busy, "A fresh defender should not be busy");

        TestAssert.DoesNotThrow(() => defender.OnDefenseStarted(outpost), "OnDefenseStarted threw");
        TestAssert.IsTrue(defender.Busy, "Defender should be busy after OnDefenseStarted");

        TestAssert.DoesNotThrow(() => defender.OnDefenseComplete(true, new BattleResult()), "OnDefenseComplete threw");
        TestAssert.IsFalse(defender.Busy, "Defender should not be busy after OnDefenseComplete");
        TestAssert.IsTrue(comp.IsOnCooldown, "Completing a defense should put the outpost on cooldown");

        comp.ClearCooldown();
        DestructiveTestUtil.AssertEmpireInvariants(f, "DefensiveAutoDefender_Lifecycle");
    }

    [EmpireDestructiveTest("VOE.Destructive.Defense")]
    public static void DefensiveAutoDefender_MilitaryLevelAndForce_Sane()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        Outpost_Defensive outpost = VOEDestructiveTestUtil.EnsureMannedDefensiveOutpost();

        var defender = new DefensiveAutoDefender(outpost);
        TestAssert.GreaterThan(defender.MilitaryLevel, 0, "A manned outpost should have a positive military level");
        TestAssert.DoesNotThrow(() => defender.CreateDefendingForce(), "CreateDefendingForce threw");

        DestructiveTestUtil.AssertEmpireInvariants(f, "DefensiveAutoDefender_MilitaryLevel");
    }

    /// <summary>
    /// T2: the efficiency seam. VOEFormulas.Efficiency is unit-tested directly; this covers the
    /// aggregation wrapping it — per-pawn max(Shooting, Melee), the empty-garrison DefaultIfEmpty(5),
    /// and the average. An empty garrison must read the skill-5 floor (Efficiency(5) = 0.75); a manned
    /// one must equal the formula fed the re-derived average, proving the wiring.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Defense")]
    public static void DefensiveAutoDefender_Efficiency_MatchesAggregation()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        // Empty-garrison floor: no capable pawns -> DefaultIfEmpty(5) -> Efficiency(5) = 0.5 + 0.05*5 = 0.75.
        Outpost_Defensive empty = VOEDestructiveTestUtil.RequireTransientDefensiveOutpost(pawnCount: 0);
        try
        {
            TestAssert.IsFalse(empty.CapablePawns.Any(), "A transient outpost created with no pawns should have no capable pawns");
            var emptyDefender = new DefensiveAutoDefender(empty);
            TestAssert.AreEqual(0.75, emptyDefender.Efficiency, 0.001,
                "An empty garrison should fall back to the skill-5 efficiency floor (0.75)");
        }
        finally
        {
            VOEDestructiveTestUtil.SafeRemoveOutpost(empty); // pure probe
        }

        // Manned garrison: Efficiency must equal VOEFormulas.Efficiency of the re-derived average.
        Outpost_Defensive manned = VOEDestructiveTestUtil.EnsureMannedDefensiveOutpost();
        double sum = 0; int n = 0;
        foreach (Pawn p in manned.CapablePawns)
        {
            if (p.skills is null) continue;
            sum += Math.Max(p.skills.GetSkill(SkillDefOf.Shooting).Level, p.skills.GetSkill(SkillDefOf.Melee).Level);
            n++;
        }
        TestAssert.GreaterThan(n, 0, "A manned outpost's capable pawns should all have skills");
        double expected = VOEFormulas.Efficiency(sum / n);
        var defender = new DefensiveAutoDefender(manned);
        TestAssert.AreEqual(expected, defender.Efficiency, 0.001,
            "Efficiency should equal VOEFormulas.Efficiency of the per-pawn max(Shooting,Melee) average");

        DestructiveTestUtil.AssertEmpireInvariants(f, "DefensiveAutoDefender_Efficiency");
    }

    /// <summary>
    /// T3: the cooldown state machine. SetCooldown arms it; once the game tick passes the end tick,
    /// IsOnCooldown must read false AND self-zero the stored tick (so CooldownTicksLeft clamps to 0).
    /// Moves the tick counter with DebugSetTicksGame and restores it in finally.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Defense")]
    public static void DefensiveCooldown_ExpiresAndSelfZeroes()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        Outpost_Defensive outpost = VOEDestructiveTestUtil.EnsureDefensiveOutpost();
        WorldObjectComp_EmpireDefensive comp = RequireDefensiveComp(outpost);

        int t0 = Find.TickManager.TicksGame;
        try
        {
            comp.SetCooldown(GenDate.TicksPerDay);
            TestAssert.IsTrue(comp.IsOnCooldown, "SetCooldown should put the outpost on cooldown");
            TestAssert.GreaterThan(comp.CooldownTicksLeft, 0, "CooldownTicksLeft should be positive while on cooldown");

            Find.TickManager.DebugSetTicksGame(t0 + GenDate.TicksPerDay + 1);
            TestAssert.IsFalse(comp.IsOnCooldown, "Cooldown should have elapsed once the tick passes the end tick");
            TestAssert.AreEqual(0, comp.CooldownTicksLeft, "CooldownTicksLeft should clamp to 0 after expiry");
        }
        finally
        {
            Find.TickManager.DebugSetTicksGame(t0);
            comp.ClearCooldown();
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "DefensiveCooldown_Expires");
    }

    /// <summary>
    /// T4: OnDefenseComplete's win/loss branch. A won defense sets a 2-day cooldown and no injuries;
    /// a lost one sets a 3-day cooldown (base + 1) and injures pawns. Uses two transient outposts so
    /// the loss branch never touches the player's real garrison.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Defense")]
    public static void OnDefenseComplete_WonVsLost_CooldownMagnitude()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        Outpost_Defensive won = VOEDestructiveTestUtil.RequireTransientDefensiveOutpost();
        Outpost_Defensive lost = null;
        try
        {
            lost = VOEDestructiveTestUtil.RequireTransientDefensiveOutpost();
            WorldObjectComp_EmpireDefensive wonComp = RequireDefensiveComp(won);
            WorldObjectComp_EmpireDefensive lostComp = RequireDefensiveComp(lost);

            new DefensiveAutoDefender(won).OnDefenseComplete(true, new BattleResult());
            TestAssert.AreEqual(GenDate.TicksPerDay * 2, wonComp.CooldownTicksLeft,
                "A won defense should set a 2-day cooldown");

            new DefensiveAutoDefender(lost).OnDefenseComplete(false, new BattleResult());
            TestAssert.AreEqual(GenDate.TicksPerDay * 3, lostComp.CooldownTicksLeft,
                "A lost defense should set a 3-day cooldown (2-day base + 1)");
        }
        finally
        {
            VOEDestructiveTestUtil.SafeRemoveOutpost(won);
            VOEDestructiveTestUtil.SafeRemoveOutpost(lost);
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "OnDefenseComplete_WonVsLost");
    }

    private static WorldObjectComp_EmpireDefensive RequireDefensiveComp(Outpost_Defensive outpost) =>
        TestAssert.NotNull(outpost.GetComponent<WorldObjectComp_EmpireDefensive>(),
            $"The defensive outpost def carries WorldObjectComp_EmpireDefensive; {outpost.def.defName} has none");
}
