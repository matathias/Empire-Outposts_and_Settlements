using Outposts;
using VOE;

namespace EmpireVOE;

/* DESTRUCTIVE: exercises how outpost military wrappers stay registered while a live operation still needs
   them, and how stuck under-attack/defending flags are repaired. Uses transient outposts and bare
   MilitaryOperation stand-ins registered on the live operation manager (never ticked: each is removed
   again in the same test). ReconcileMilitaryState runs over every outpost in the world, as it does after
   a load. The military settings are forced on and restored; the tick counter is restored. */
public static class VOEMilitaryReconcileDestructiveTests
{
    private struct MilitarySettings
    {
        public bool enableMilitary;
        public bool disableIntegration;
    }

    private static MilitarySettings ForceMilitaryOn()
    {
        MilitarySettings before = new()
        {
            enableMilitary = EmpireVOESettings.enableMilitary,
            disableIntegration = EmpireVOESettings.disableIntegration,
        };
        EmpireVOESettings.enableMilitary = true;
        EmpireVOESettings.disableIntegration = false;
        return before;
    }

    private static void Restore(MilitarySettings before)
    {
        EmpireVOESettings.enableMilitary = before.enableMilitary;
        EmpireVOESettings.disableIntegration = before.disableIntegration;
    }

    private static MilitaryOperationManager RequireManager() =>
        TestAssert.NotNull(FindFC.MilitaryManager, "The military operation manager is unavailable");

    private static WorldObjectComp_EmpireOutpost RequireOutpostComp(Outpost outpost) =>
        TestAssert.NotNull(outpost.GetComponent<WorldObjectComp_EmpireOutpost>(),
            $"Every VEF outpost def carries WorldObjectComp_EmpireOutpost; {outpost.def.defName} has none");

    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void UnregisterIdleWrappers_KeepsRaidTargetWhileOperationTargetsIt()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryOperationManager manager = RequireManager();
        MilitarySettings settings = ForceMilitaryOn();
        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost(pawnCount: 0);
        MilitaryOperation op = new() { targetObject = outpost };
        try
        {
            RequireOutpostComp(outpost).Register();
            manager.Register(op);

            TestAssert.IsFalse(WorldObjectComp_EmpireOutpost.UnregisterIdleWrappers(outpost),
                "an outpost targeted by a live operation is not idle");
            TestAssert.IsNotNull(RaidTargetRegistry.FindByWorldObject(outpost),
                "the raid target wrapper should stay registered while the operation needs it");

            manager.Unregister(op);
            TestAssert.IsTrue(WorldObjectComp_EmpireOutpost.UnregisterIdleWrappers(outpost),
                "with no operation left, the outpost's wrappers are idle");
            TestAssert.IsNull(RaidTargetRegistry.FindByWorldObject(outpost),
                "an idle raid target wrapper should be unregistered");
        }
        finally
        {
            manager.Unregister(op);
            Restore(settings);
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "UnregisterIdleWrappers_Target");
    }

    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void UnregisterIdleWrappers_KeepsDefenderWhileOperationUsesIt()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryOperationManager manager = RequireManager();
        MilitarySettings settings = ForceMilitaryOn();
        Outpost_Defensive outpost = VOEDestructiveTestUtil.RequireTransientDefensiveOutpost(pawnCount: 0);
        MilitaryOperation op = new() { externalDefenderSource = outpost };
        try
        {
            WorldObjectComp_EmpireDefensive defComp = TestAssert.NotNull(outpost.GetComponent<WorldObjectComp_EmpireDefensive>(),
                $"The defensive outpost def carries WorldObjectComp_EmpireDefensive; {outpost.def.defName} has none");
            defComp.Register();
            RequireOutpostComp(outpost).Register();
            manager.Register(op);

            TestAssert.IsFalse(WorldObjectComp_EmpireOutpost.UnregisterIdleWrappers(outpost),
                "an outpost defending in a live operation is not idle");
            TestAssert.IsNotNull(AutoDefenderRegistry.FindByWorldObject(outpost),
                "the auto-defender wrapper should stay registered while the operation needs it");
            TestAssert.IsNull(RaidTargetRegistry.FindByWorldObject(outpost),
                "the raid target wrapper, which no operation needs, should be unregistered");

            manager.Unregister(op);
            TestAssert.IsTrue(WorldObjectComp_EmpireOutpost.UnregisterIdleWrappers(outpost),
                "with no operation left, the outpost's wrappers are idle");
            TestAssert.IsNull(AutoDefenderRegistry.FindByWorldObject(outpost),
                "an idle auto-defender wrapper should be unregistered");
        }
        finally
        {
            manager.Unregister(op);
            Restore(settings);
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "UnregisterIdleWrappers_Defender");
    }

    /// <summary>
    /// A wrapper left registered after a military toggle-off is unregistered by the tracker's periodic
    /// pass once its operation is gone, and not before.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void PendingUnregister_TrackerPassWaitsForOperationToEnd()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryOperationManager manager = RequireManager();
        WorldComponent_VOETracker tracker = TestAssert.NotNull(Find.World.GetComponent<WorldComponent_VOETracker>(),
            "The VOE world component is missing");
        MilitarySettings settings = ForceMilitaryOn();
        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost(pawnCount: 0);
        MilitaryOperation op = new() { targetObject = outpost };
        int t0 = Find.TickManager.TicksGame;
        try
        {
            RequireOutpostComp(outpost).Register();
            manager.Register(op);
            WorldComponent_VOETracker.AddPendingUnregister(outpost);

            // The pass runs on ticks that are a multiple of 250.
            int passTick = t0 + 250 - t0 % 250;
            Find.TickManager.DebugSetTicksGame(passTick);
            tracker.WorldComponentTick();
            TestAssert.IsNotNull(RaidTargetRegistry.FindByWorldObject(outpost),
                "the pass must keep a wrapper a live operation still references");

            manager.Unregister(op);
            Find.TickManager.DebugSetTicksGame(passTick + 250);
            tracker.WorldComponentTick();
            TestAssert.IsNull(RaidTargetRegistry.FindByWorldObject(outpost),
                "once the operation is gone, the pass should unregister the wrapper");
        }
        finally
        {
            Find.TickManager.DebugSetTicksGame(t0);
            WorldComponent_VOETracker.CancelPendingUnregister(outpost);
            manager.Unregister(op);
            Restore(settings);
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "PendingUnregister_TrackerPass");
    }

    /// <summary>
    /// The post-load repair clears under-attack and defending flags that no live operation backs, and keeps
    /// the ones that are still backed.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.Military")]
    public static void ReconcileMilitaryState_ClearsOnlyUnbackedFlags()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryOperationManager manager = RequireManager();
        MilitarySettings settings = ForceMilitaryOn();
        Outpost_Defensive stuck = VOEDestructiveTestUtil.RequireTransientDefensiveOutpost(pawnCount: 0);
        Outpost_Defensive backed = null;
        MilitaryOperation op = null;
        try
        {
            backed = VOEDestructiveTestUtil.RequireTransientDefensiveOutpost(pawnCount: 0);
            WorldObjectComp_EmpireOutpost stuckComp = RequireOutpostComp(stuck);
            WorldObjectComp_EmpireDefensive stuckDef = TestAssert.NotNull(stuck.GetComponent<WorldObjectComp_EmpireDefensive>(),
                "The defensive outpost def carries WorldObjectComp_EmpireDefensive");
            WorldObjectComp_EmpireOutpost backedComp = RequireOutpostComp(backed);

            stuckComp.outpostUnderAttack = true;
            stuckDef.defending = true;
            stuckDef.defendingTarget = backed;
            backedComp.outpostUnderAttack = true;
            op = new MilitaryOperation { targetObject = backed };
            manager.Register(op);

            WorldObjectComp_EmpireOutpost.ReconcileMilitaryState();

            TestAssert.IsFalse(stuckComp.outpostUnderAttack, "an under-attack flag no operation backs should be cleared");
            TestAssert.IsFalse(stuckDef.defending, "a defending flag no operation backs should be cleared");
            TestAssert.IsNull(stuckDef.defendingTarget, "the defending target should be cleared with the flag");
            TestAssert.IsTrue(backedComp.outpostUnderAttack, "an under-attack flag a live operation backs should stay set");
        }
        finally
        {
            manager.Unregister(op);
            if (backed?.GetComponent<WorldObjectComp_EmpireOutpost>() is { } bc) bc.outpostUnderAttack = false;
            Restore(settings);
            VOEDestructiveTestUtil.SafeRemoveOutpost(stuck);
            VOEDestructiveTestUtil.SafeRemoveOutpost(backed);
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "ReconcileMilitaryState");
    }
}
