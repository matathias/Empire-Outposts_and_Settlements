using Outposts;

namespace EmpireVOE;

/* DESTRUCTIVE: exercises OutpostFinancer against a live Empire settlement + a transient outpost.
   Deposits silver into the outpost and drains it through the payment path. Restores
   enableFinancing and the settlement's financing link. Created outposts are NOT cleaned up.

   Covers the H1/H5 fix: outpost silver credits affordability only for the paying settlement,
   CanAfford never drains, TryPaySilver drains only on commit, and a settlement-less payment is
   never credited (the original free-payment scenario). */
public static class VOEFinancingDestructiveTests
{
    [EmpireDestructiveTest("VOE.Destructive.Financing")]
    public static void OutpostFinancer_CanAfford_CreditsOutpostWithoutDraining()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_OutpostLinks links = TestAssert.NotNull(settlement.GetComponent<WorldObjectComp_OutpostLinks>(),
            "Every settlement def carries WorldObjectComp_OutpostLinks; this one has none");

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();

        bool financingBefore = EmpireVOESettings.enableFinancing;
        Outpost linkBefore = links.financingOutpost;
        EmpireVOESettings.enableFinancing = true;
        links.financingOutpost = outpost;
        DepositSilver(outpost, 5000);
        try
        {
            int home = PaymentUtil.GetSilver();
            // Beyond home storage, but covered by the outpost's 5000.
            TestAssert.IsTrue(
                PaymentUtil.CanAfford(home + 500, PaymentUtil.Reason_BuildingConstruction, settlement),
                "CanAfford should credit the settlement's financing outpost");
            TestAssert.AreEqual(5000, OutpostSilver(outpost), message: "CanAfford must not drain the outpost (H5)");
        }
        finally
        {
            EmpireVOESettings.enableFinancing = financingBefore;
            links.financingOutpost = linkBefore;
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostFinancer_CanAfford");
    }

    [EmpireDestructiveTest("VOE.Destructive.Financing")]
    public static void OutpostFinancer_TryPaySilver_DrainsOutpostNotHome()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_OutpostLinks links = TestAssert.NotNull(settlement.GetComponent<WorldObjectComp_OutpostLinks>(),
            "Every settlement def carries WorldObjectComp_OutpostLinks; this one has none");

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();

        bool financingBefore = EmpireVOESettings.enableFinancing;
        Outpost linkBefore = links.financingOutpost;
        EmpireVOESettings.enableFinancing = true;
        links.financingOutpost = outpost;
        DepositSilver(outpost, 5000);
        try
        {
            int home = PaymentUtil.GetSilver();
            // Small charge fully covered by the outpost: succeeds, drains the outpost, leaves home alone.
            TestAssert.IsTrue(
                PaymentUtil.TryPaySilver(300, PaymentUtil.Reason_BuildingConstruction, settlement),
                "Outpost-financed payment should succeed");
            TestAssert.AreEqual(4700, OutpostSilver(outpost), message: "Commit must drain the outpost by the charge");
            TestAssert.AreEqual(home, PaymentUtil.GetSilver(), message: "Outpost-financed payment must not touch home silver");
        }
        finally
        {
            EmpireVOESettings.enableFinancing = financingBefore;
            links.financingOutpost = linkBefore;
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostFinancer_TryPaySilver");
    }

    [EmpireDestructiveTest("VOE.Destructive.Financing")]
    public static void OutpostFinancer_NullSettlement_NotCreditedOrDrained()
    {
        // The original H1 free-payment scenario: a settlement-less payment must NOT see any outpost
        // silver (no global inflation) and must NOT drain any outpost.
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_OutpostLinks links = TestAssert.NotNull(settlement.GetComponent<WorldObjectComp_OutpostLinks>(),
            "Every settlement def carries WorldObjectComp_OutpostLinks; this one has none");

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();

        bool financingBefore = EmpireVOESettings.enableFinancing;
        Outpost linkBefore = links.financingOutpost;
        EmpireVOESettings.enableFinancing = true;
        links.financingOutpost = outpost;
        DepositSilver(outpost, 5000);
        try
        {
            int home = PaymentUtil.GetSilver();
            // No settlement context => financing does not apply; only home storage counts.
            TestAssert.IsFalse(PaymentUtil.CanAfford(home + 500, PaymentUtil.Reason_SquadHire, null),
                "A settlement-less payment must not be credited with outpost silver (H1)");
            TestAssert.AreEqual(5000, OutpostSilver(outpost), message: "A settlement-less payment must not drain any outpost (H1)");
        }
        finally
        {
            EmpireVOESettings.enableFinancing = financingBefore;
            links.financingOutpost = linkBefore;
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "OutpostFinancer_NullSettlement");
    }

    private static void DepositSilver(Outpost outpost, int count)
    {
        Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
        silver.stackCount = count;
        outpost.AddItem(silver);
    }

    private static int OutpostSilver(Outpost outpost)
    {
        int total = 0;
        foreach (Thing t in outpost.Things)
        {
            if (t.def == ThingDefOf.Silver) total += t.stackCount;
        }
        return total;
    }
}
