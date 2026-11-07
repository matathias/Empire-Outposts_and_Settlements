using Outposts;

namespace EmpireVOE;

/* DESTRUCTIVE: exercises WorldObjectComp_ResourceLink.ToggleLink against live Empire settlements and a
   transient outpost. Toggling mutates the settlement-side linked list and the outpost's runtime reverse
   pointer and invalidates resource caches; every test unlinks what it linked and removes its transient
   outpost. Missing settlements are created; those are NOT cleaned up. */
public static class VOEResourceLinkDestructiveTests
{
    /// <summary>
    /// ToggleLink is its own inverse: linking adds the outpost to the settlement's list and points the
    /// outpost's reverse index at that settlement; toggling again removes both. This is the core of the
    /// management-tab wiring.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.ResourceLink")]
    public static void ResourceLink_ToggleLink_AddRemoveRoundTrips()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_ResourceLink comp = RequireResourceLink(settlement);

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();
        bool hasEmpireComp = outpost.GetComponent<WorldObjectComp_EmpireOutpost>() is not null;

        try
        {
            TestAssert.IsFalse(comp.IsLinkedHere(outpost), "A fresh outpost should not start linked");

            comp.ToggleLink(outpost);
            TestAssert.IsTrue(comp.IsLinkedHere(outpost), "ToggleLink should link the outpost here");
            if (hasEmpireComp)
                TestAssert.AreEqual(settlement, ResourceLinkUtil.LinkedSettlementOf(outpost),
                    "The outpost's reverse index should point at this settlement while linked");

            comp.ToggleLink(outpost);
            TestAssert.IsFalse(comp.IsLinkedHere(outpost), "ToggleLink again should unlink the outpost");
            if (hasEmpireComp)
                TestAssert.IsNull(ResourceLinkUtil.LinkedSettlementOf(outpost),
                    "The reverse index should be cleared after unlinking");
        }
        finally
        {
            if (comp.IsLinkedHere(outpost)) comp.ToggleLink(outpost);
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "ResourceLink_ToggleRoundTrip");
    }

    /// <summary>
    /// An outpost may be linked by at most one settlement. Linking it at a second settlement must first
    /// cleanly unlink it from the first (dropping that entry), not silently double-book it.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.ResourceLink")]
    public static void ResourceLink_Exclusivity_OneSettlementPerOutpost()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        List<WorldSettlementFC> settlements = DestructiveTestUtil.EnsureSettlements(2);
        WorldSettlementFC s1 = settlements[0];
        WorldSettlementFC s2 = settlements[1];
        WorldObjectComp_ResourceLink comp1 = RequireResourceLink(s1);
        WorldObjectComp_ResourceLink comp2 = RequireResourceLink(s2);

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost();
        // Exclusivity is enforced through the outpost's reverse pointer, which lives on its Empire comp
        // (patched onto every VEF outpost def).
        if (outpost.GetComponent<WorldObjectComp_EmpireOutpost>() is null)
        {
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
            TestAssert.Fail($"Outpost {outpost.def.defName} has no WorldObjectComp_EmpireOutpost (no reverse index)");
        }

        try
        {
            comp1.ToggleLink(outpost);
            TestAssert.IsTrue(comp1.IsLinkedHere(outpost), "Outpost should be linked at the first settlement");

            comp2.ToggleLink(outpost); // must unlink from s1 before linking here
            TestAssert.IsTrue(comp2.IsLinkedHere(outpost), "Outpost should be linked at the second settlement");
            TestAssert.IsFalse(comp1.IsLinkedHere(outpost),
                "Linking at the second settlement must have unlinked it from the first (exclusivity)");
            TestAssert.AreEqual(s2, ResourceLinkUtil.LinkedSettlementOf(outpost),
                "The reverse index should now point at the second settlement");
        }
        finally
        {
            if (comp1.IsLinkedHere(outpost)) comp1.ToggleLink(outpost);
            if (comp2.IsLinkedHere(outpost)) comp2.ToggleLink(outpost);
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "ResourceLink_Exclusivity");
    }

    /// <summary>
    /// Linking an outpost raises the settlement's additive modifier for a resource it feeds by exactly that
    /// outpost's own contribution. Uses a transient outpost of a resource-feeding def (patched onto VOE's
    /// production outposts) and compares against the pre-link modifier, so outposts the settlement already
    /// links don't skew the result.
    /// </summary>
    [EmpireDestructiveTest("VOE.Destructive.ResourceLink")]
    public static void ResourceLink_AdditiveModifier_EqualsLinkedContribution()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_ResourceLink comp = RequireResourceLink(settlement);

        WorldObjectDef linkableDef = TestAssert.NotNull(
            DefDatabase<WorldObjectDef>.AllDefsListForReading.FirstOrDefault(d =>
                d.worldObjectClass is not null && typeof(Outpost).IsAssignableFrom(d.worldObjectClass)
                && d.GetModExtension<OutpostResourceLinkExtension>()?.resources?.Count > 0),
            "No outpost def carries a resource-feeding OutpostResourceLinkExtension");
        OutpostResourceLinkExtension ext = linkableDef.GetModExtension<OutpostResourceLinkExtension>();
        ResourceTypeDef fed = TestAssert.NotNull(ext.resources.FirstOrDefault(),
            $"{linkableDef.defName} resource-link extension lists a null resource");

        Outpost outpost = VOEDestructiveTestUtil.RequireTransientOutpost(linkableDef);
        TestAssert.IsTrue(ResourceLinkUtil.IsLinkable(outpost), "A resource-feeding outpost should be linkable");

        bool resourceLinkBefore = EmpireVOESettings.enableResourceLink;
        bool integrationBefore = EmpireVOESettings.disableIntegration;
        try
        {
            EmpireVOESettings.disableIntegration = false;
            EmpireVOESettings.enableResourceLink = true;

            var res = new ResourceFC { def = fed };
            double before = comp.GetResourceAdditiveModifier(res);

            comp.ToggleLink(outpost);
            TestAssert.IsTrue(comp.IsLinkedHere(outpost), "A fresh, unclaimed outpost should link on the first toggle");

            double expected = ext.Contribution(outpost, settlement);
            TestAssert.AreEqual(before + expected, comp.GetResourceAdditiveModifier(res), 0.001,
                "Linking an outpost should raise the additive modifier by exactly its own contribution");
        }
        finally
        {
            if (comp.IsLinkedHere(outpost)) comp.ToggleLink(outpost);
            EmpireVOESettings.enableResourceLink = resourceLinkBefore;
            EmpireVOESettings.disableIntegration = integrationBefore;
            VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "ResourceLink_AdditiveEqualsContribution");
    }

    private static WorldObjectComp_ResourceLink RequireResourceLink(WorldSettlementFC settlement) =>
        TestAssert.NotNull(settlement.GetComponent<WorldObjectComp_ResourceLink>(),
            $"Every settlement def carries WorldObjectComp_ResourceLink; '{settlement.Name}' has none");
}
