using System.Linq;
using FactionColonies;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Coverage for resource outpost linking (<see cref="WorldObjectComp_ResourceLink"/> and
    /// <see cref="OutpostResourceLinkExtension"/>). These build throwaway comps and read the DefDatabase
    /// only, so they mutate nothing and are non-destructive.
    /// </summary>
    public static class VOEResourceLinkTests
    {
        [EmpireTest("VOE.ResourceLink")]
        public static void MultiplierModifier_IsOne()
        {
            WorldObjectComp_ResourceLink comp = new WorldObjectComp_ResourceLink();
            TestAssert.AreEqual(1.0, comp.GetResourceMultiplierModifier(null));
        }

        [EmpireTest("VOE.ResourceLink")]
        public static void IsLinkable_Null_False()
        {
            TestAssert.IsTrue(!ResourceLinkUtil.IsLinkable(null), "null outpost is not linkable");
        }

        [EmpireTest("VOE.ResourceLink")]
        public static void ContributionOf_Null_Zero()
        {
            TestAssert.AreEqual(0.0, ResourceLinkUtil.ContributionOf(null));
        }

        [EmpireTest("VOE.ResourceLink")]
        public static void Extension_AllMapped_HaveResourcesAndSkill()
        {
            var mapped = DefDatabase<WorldObjectDef>.AllDefsListForReading
                .Where(d => d.GetModExtension<OutpostResourceLinkExtension>() is object)
                .ToList();
            if (mapped.Count == 0)
                TestAssert.Skip("No outpost defs carry OutpostResourceLinkExtension (VOE not loaded)");

            foreach (WorldObjectDef d in mapped)
            {
                OutpostResourceLinkExtension ext = d.GetModExtension<OutpostResourceLinkExtension>();
                TestAssert.IsTrue(ext.resources is object && ext.resources.Count > 0,
                    $"{d.defName} resource-link extension declares no resources");
                TestAssert.IsNotNull(ext.skill, $"{d.defName} resource-link extension declares no skill");
            }
        }
    }
}
