using Outposts;
using VOE;

namespace EmpireVOE;

/* DESTRUCTIVE: garrison-level behavior on transient outposts: the two-capable-pawn rule for auto-defense,
   random outpost injuries, stale link clearing, and the resource-link candidate cache. Pawns are
   generated into transient outposts, which each test removes. The candidate-cache test moves the tick
   counter and restores it. */
public static class VOEGarrisonDestructiveTests
{
    private static WorldObjectComp_EmpireDefensive RequireDefensiveComp(Outpost_Defensive outpost) =>
        TestAssert.NotNull(outpost.GetComponent<WorldObjectComp_EmpireDefensive>(),
            $"The defensive outpost def carries WorldObjectComp_EmpireDefensive; {outpost.def.defName} has none");

    private static void ReadyForAutoDefend(WorldObjectComp_EmpireDefensive comp)
    {
        comp.autoDefend = true;
        comp.defending = false;
        comp.defendingTarget = null;
        comp.ClearCooldown();
    }

    /// <summary>
    /// One capable pawn plus an animal: the outpost holds two pawns, but only one can defend and it has to
    /// stay behind, so the outpost cannot auto-defend and sends nobody.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Defense")]
    public static void AutoDefend_OneCapablePawnPlusAnimal_CannotDefend()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Outpost_Defensive outpost = VOEDestructiveTestUtil.RequireTransientDefensiveOutpost(pawnCount: 1);
        try
        {
            PawnKindDef animalKind = TestAssert.NotNull(FactionCache.AllAnimalKindDefs?.FirstOrDefault(),
                "No animal pawn kind is loaded");
            Pawn animal = PawnGenerator.GeneratePawn(animalKind, Faction.OfPlayer);
            if (!outpost.AddPawn(animal))
            {
                animal.Destroy(DestroyMode.Vanish);
                TestAssert.Fail($"The defensive outpost refused a {animalKind.defName}");
            }
            TestAssert.AreEqual(2, outpost.PawnCount, "the outpost should hold the colonist and the animal");
            TestAssert.AreEqual(1, outpost.CapablePawns.Count(), "only the colonist should be capable");

            WorldObjectComp_EmpireDefensive comp = RequireDefensiveComp(outpost);
            ReadyForAutoDefend(comp);
            DefensiveAutoDefender defender = new(outpost);

            TestAssert.IsFalse(defender.CanAutoDefend, "a single capable pawn must stay behind, so auto-defend is unavailable");
            TestAssert.IsNull(defender.GetDefendingPawns(), "with one capable pawn, no defenders should be sent");
            TestAssert.AreEqual(2, outpost.PawnCount, "no pawn should leave the outpost");

            DestructiveTestUtil.AssertEmpireInvariants(f, "AutoDefend_OneCapablePlusAnimal");
        }
        finally
        {
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
        }
    }

    /// <summary>Two capable pawns: one goes out, one stays, and returning the defender restores the garrison.</summary>
    [EmpireDestructiveTest("VOE.Destructive.Defense")]
    public static void AutoDefend_TwoCapablePawns_SendsOneKeepsOne()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Outpost_Defensive outpost = VOEDestructiveTestUtil.RequireTransientDefensiveOutpost(pawnCount: 2);
        List<Pawn> sent = null;
        try
        {
            int capableBefore = outpost.CapablePawns.Count();
            WorldObjectComp_EmpireDefensive comp = RequireDefensiveComp(outpost);
            ReadyForAutoDefend(comp);
            DefensiveAutoDefender defender = new(outpost);

            TestAssert.IsTrue(defender.CanAutoDefend, "two capable pawns should allow auto-defense");

            sent = defender.GetDefendingPawns();
            TestAssert.NotNull(sent, "defenders should be sent");
            TestAssert.AreEqual(capableBefore - 1, sent.Count, "all but one capable pawn should be sent");
            TestAssert.AreEqual(1, outpost.CapablePawns.Count(), "exactly one capable pawn should stay behind");

            defender.ReturnDefendingPawns(sent);
            sent = null;
            TestAssert.AreEqual(capableBefore, outpost.CapablePawns.Count(), "returning the defenders should restore the garrison");

            DestructiveTestUtil.AssertEmpireInvariants(f, "AutoDefend_TwoCapable");
        }
        finally
        {
            if (sent is not null && !outpost.Destroyed)
            {
                try { new DefensiveAutoDefender(outpost).ReturnDefendingPawns(sent); }
                catch (Exception ex) { VOELog.Warning($"returning test defenders threw (ignored): {ex}"); }
            }
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
        }
    }

    /// <summary>
    /// InjureRandomPawns injures exactly the requested number of occupants, and at most every occupant.
    /// Apparel is stripped first so armor cannot deflect the cut.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Defense")]
    public static void InjureRandomPawns_InjuresRequestedCountCappedByOccupants()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost(pawnCount: 3);
        try
        {
            foreach (Pawn pawn in outpost.AllPawns)
                pawn.apparel?.DestroyAll();
            List<Pawn> pawns = outpost.AllPawns.ToList();
            FloatRange damage = new(6f, 6f);

            Dictionary<Pawn, int> before = pawns.ToDictionary(p => p, p => p.health.hediffSet.hediffs.Count);
            VOEPawnUtil.InjureRandomPawns(outpost, 2, damage);
            TestAssert.AreEqual(2, pawns.Count(p => p.health.hediffSet.hediffs.Count > before[p]),
                "exactly two occupants should be injured");

            before = pawns.ToDictionary(p => p, p => p.health.hediffSet.hediffs.Count);
            VOEPawnUtil.InjureRandomPawns(outpost, 10, damage);
            TestAssert.AreEqual(pawns.Count, pawns.Count(p => p.health.hediffSet.hediffs.Count > before[p]),
                "asking for more injuries than occupants should injure every occupant once");

            DestructiveTestUtil.AssertEmpireInvariants(f, "InjureRandomPawns");
        }
        finally
        {
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
        }
    }

    /// <summary>A link to an outpost that has since been destroyed reads as unset and is cleared.</summary>
    [EmpireDestructiveTest("VOE.Links")]
    public static void LinkSlots_DestroyedOutpost_ReadsUnsetAndIsCleared()
    {
        Outpost delivery = VOEDestructiveTestUtil.RequireTransientOutpost(pawnCount: 0);
        Outpost financing = null;
        try
        {
            financing = VOEDestructiveTestUtil.RequireTransientOutpost(pawnCount: 0);
            WorldObjectComp_OutpostLinks links = new() { deliveryOutpost = delivery, financingOutpost = financing };

            VOEDestructiveTestUtil.SafeRemoveOutpost(delivery);
            TestAssert.IsTrue(delivery.Destroyed, "the delivery fixture should be destroyed");
            TestAssert.IsNull(links.GetDeliveryOutpost(), "a destroyed delivery outpost should read as unset");
            TestAssert.IsNull(links.StoredLink(OutpostLinkSlot.Delivery), "reading it should clear the stale delivery link");
            TestAssert.AreEqual(financing, links.GetFinancingOutpost(), "the live financing link should be untouched");
        }
        finally
        {
            VOEDestructiveTestUtil.SafeRemoveOutpost(delivery);
            VOEDestructiveTestUtil.SafeRemoveOutpost(financing);
        }
    }

    /// <summary>
    /// The management tab's link candidates are cached: repeated reads return the same list until a link
    /// changes anywhere, the refresh interval passes, or a cached candidate is destroyed.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.ResourceLink")]
    public static void LinkCandidates_CachedUntilLinkChangeIntervalOrDestroyedCandidate()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_ResourceLink comp = TestAssert.NotNull(settlement.GetComponent<WorldObjectComp_ResourceLink>(),
            $"Every settlement def carries WorldObjectComp_ResourceLink; '{settlement.Name}' has none");
        WorldObjectDef linkableDef = TestAssert.NotNull(DefDatabase<WorldObjectDef>.AllDefsListForReading.FirstOrDefault(d =>
                d.worldObjectClass is not null && typeof(Outpost).IsAssignableFrom(d.worldObjectClass)
                && d.GetModExtension<OutpostResourceLinkExtension>() is { } ext
                && ext.GetType() == typeof(OutpostResourceLinkExtension) && ext.resources?.Count > 0),
            "No outpost def with a plain resource-link extension is loaded");

        int t0 = Find.TickManager.TicksGame;
        Outpost candidate = null;
        try
        {
            List<Outpost> first = comp.GetLinkableOutpostsInRange();
            TestAssert.IsTrue(ReferenceEquals(first, comp.GetLinkableOutpostsInRange()),
                "a second read in the same tick should return the cached list");

            ResourceLinkUtil.NotifyLinkChanged(null);
            List<Outpost> afterLink = comp.GetLinkableOutpostsInRange();
            TestAssert.IsFalse(ReferenceEquals(first, afterLink), "a link change should rebuild the candidate list");

            Find.TickManager.DebugSetTicksGame(t0 + 250);
            List<Outpost> afterInterval = comp.GetLinkableOutpostsInRange();
            TestAssert.IsFalse(ReferenceEquals(afterLink, afterInterval), "the list should be rebuilt once the refresh interval passes");

            candidate = VOEDestructiveTestUtil.RequireTransientOutpost(linkableDef, pawnCount: 0, near: settlement.Tile,
                maxDistance: Math.Max(1, EmpireVOESettings.resourceLinkRange / 2));
            Find.TickManager.DebugSetTicksGame(t0 + 500);
            List<Outpost> withCandidate = comp.GetLinkableOutpostsInRange();
            TestAssert.Contains(withCandidate, candidate, "a linkable outpost in range should be listed once the list refreshes");

            VOEDestructiveTestUtil.SafeRemoveOutpost(candidate);
            List<Outpost> afterDestroy = comp.GetLinkableOutpostsInRange();
            TestAssert.IsFalse(afterDestroy.Contains(candidate),
                "a destroyed candidate should force a rebuild within the refresh interval");

            DestructiveTestUtil.AssertEmpireInvariants(f, "LinkCandidates_Cache");
        }
        finally
        {
            Find.TickManager.DebugSetTicksGame(t0);
            VOEDestructiveTestUtil.SafeRemoveOutpost(candidate);
            // The cache was stamped at a later tick than the restored one; force a rebuild on the next read.
            ResourceLinkUtil.NotifyLinkChanged(null);
        }
    }
}
