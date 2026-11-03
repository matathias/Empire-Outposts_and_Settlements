using System.Linq;
using FactionColonies;
using Outposts;
using Verse;

namespace EmpireVOE
{
    /* DESTRUCTIVE: exercises WorldObjectComp_ResourceLink.ToggleLink against live Empire settlements and a
       transient outpost. Toggling mutates the settlement-side linked list and the outpost's runtime reverse
       pointer and invalidates resource caches; every test unlinks what it linked and removes its transient
       outpost. The aggregation test only runs when a genuinely linkable (resource-feeding) outpost exists. */
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
            WorldSettlementFC settlement = f.settlements.FirstOrDefault();
            if (settlement is null) TestAssert.Skip("No Empire settlement available");
            WorldObjectComp_ResourceLink comp = settlement.GetComponent<WorldObjectComp_ResourceLink>();
            if (comp is null) TestAssert.Skip("Settlement has no WorldObjectComp_ResourceLink");

            Outpost outpost = VOEDestructiveTestUtil.CreateTransientOutpost();
            if (outpost is null) TestAssert.Skip("Could not create a transient outpost");
            bool hasEmpireComp = outpost.GetComponent<WorldObjectComp_EmpireOutpost>() is object;

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
            if (f.settlements.Count < 2) TestAssert.Skip("Need at least two settlements to test link exclusivity");
            WorldSettlementFC s1 = f.settlements[0];
            WorldSettlementFC s2 = f.settlements[1];
            WorldObjectComp_ResourceLink comp1 = s1.GetComponent<WorldObjectComp_ResourceLink>();
            WorldObjectComp_ResourceLink comp2 = s2.GetComponent<WorldObjectComp_ResourceLink>();
            if (comp1 is null || comp2 is null) TestAssert.Skip("A settlement has no WorldObjectComp_ResourceLink");

            Outpost outpost = VOEDestructiveTestUtil.CreateTransientOutpost();
            if (outpost is null) TestAssert.Skip("Could not create a transient outpost");
            // Exclusivity is enforced through the outpost's reverse pointer, which lives on its Empire comp.
            if (outpost.GetComponent<WorldObjectComp_EmpireOutpost>() is null)
            {
                VOEDestructiveTestUtil.SafeRemoveOutpost(outpost);
                TestAssert.Skip("Transient outpost has no WorldObjectComp_EmpireOutpost (no reverse index)");
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
        /// The additive modifier a settlement gets from a linked outpost equals that outpost's own
        /// contribution for a resource it feeds. Only runs when a genuinely linkable outpost exists (needs
        /// the VOE resource-link defs loaded); otherwise skips.
        /// </summary>
        [EmpireDestructiveTest("VOE.Destructive.ResourceLink")]
        public static void ResourceLink_AdditiveModifier_EqualsLinkedContribution()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC settlement = f.settlements.FirstOrDefault();
            if (settlement is null) TestAssert.Skip("No Empire settlement available");
            WorldObjectComp_ResourceLink comp = settlement.GetComponent<WorldObjectComp_ResourceLink>();
            if (comp is null) TestAssert.Skip("Settlement has no WorldObjectComp_ResourceLink");

            Outpost outpost = Find.WorldObjects.AllWorldObjects
                .OfType<Outpost>()
                .FirstOrDefault(o => ResourceLinkUtil.IsLinkable(o) && !comp.IsLinkedHere(o));
            if (outpost is null) TestAssert.Skip("No linkable resource outpost available");

            OutpostResourceLinkExtension ext = outpost.def.GetModExtension<OutpostResourceLinkExtension>();
            ResourceTypeDef fed = ext.resources.FirstOrDefault();
            if (fed is null) TestAssert.Skip("Linkable outpost declares no resources");

            bool resourceLinkBefore = EmpireVOESettings.enableResourceLink;
            bool integrationBefore = EmpireVOESettings.disableIntegration;
            bool linkedByUs = false;
            try
            {
                EmpireVOESettings.disableIntegration = false;
                EmpireVOESettings.enableResourceLink = true;

                comp.ToggleLink(outpost);
                linkedByUs = comp.IsLinkedHere(outpost);
                if (!linkedByUs) TestAssert.Skip("Could not link the outpost (already claimed elsewhere)");

                var res = new ResourceFC { def = fed };
                double expected = ext.Contribution(outpost, settlement);
                TestAssert.AreEqual(expected, comp.GetResourceAdditiveModifier(res), 0.001,
                    "A single linked outpost's additive modifier should equal its own contribution");
            }
            finally
            {
                if (linkedByUs && comp.IsLinkedHere(outpost)) comp.ToggleLink(outpost);
                EmpireVOESettings.enableResourceLink = resourceLinkBefore;
                EmpireVOESettings.disableIntegration = integrationBefore;
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "ResourceLink_AdditiveEqualsContribution");
        }
    }
}
