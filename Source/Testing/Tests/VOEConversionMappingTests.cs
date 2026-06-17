using System.Linq;
using FactionColonies;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Coverage for the outpost->settlement type mapping (<see cref="OutpostConversionExtension"/> and
    /// <see cref="OutpostConversionUtil.GetOutpostTypesFor"/>). These only read the DefDatabase and
    /// build throwaway extension fixtures, so they mutate nothing and are non-destructive.
    /// </summary>
    public static class VOEConversionMappingTests
    {
        private static WorldSettlementDef AnySettlementDef()
        {
            return DefDatabase<WorldSettlementDef>.AllDefsListForReading.FirstOrDefault();
        }

        [EmpireTest("VOE.Conversion")]
        public static void GetExplicitTypes_ResolvesRealNameDropsBogus()
        {
            WorldSettlementDef real = AnySettlementDef();
            if (real is null) TestAssert.Skip("No WorldSettlementDefs loaded");

            OutpostConversionExtension ext = VOETestHelper.MakeConversionExtension(
                false, real.defName, "VOE_BogusSettlementType_DoesNotExist");

            var resolved = ext.GetExplicitTypes();
            TestAssert.AreEqual(1, resolved.Count, "Only the real defName should resolve; the bogus one is dropped");
            TestAssert.Contains(resolved, real);
        }

        [EmpireTest("VOE.Conversion")]
        public static void GetExplicitTypes_EmptyList_IsEmpty()
        {
            OutpostConversionExtension ext = VOETestHelper.MakeConversionExtension(false);
            TestAssert.IsEmpty(ext.GetExplicitTypes());
        }

        [EmpireTest("VOE.Conversion")]
        public static void GetOutpostTypesFor_Null_IsEmpty()
        {
            TestAssert.IsEmpty(OutpostConversionUtil.GetOutpostTypesFor(null));
        }

        [EmpireTest("VOE.Conversion")]
        public static void GetOutpostTypesFor_RealType_ReturnsOnlyConsistentDefs()
        {
            WorldSettlementDef type = AnySettlementDef();
            if (type is null) TestAssert.Skip("No WorldSettlementDefs loaded");

            // The reverse lookup may legitimately be empty (no outpost maps to this type); the contract
            // is that every def it DOES return actually allows this type.
            foreach (WorldObjectDef wod in OutpostConversionUtil.GetOutpostTypesFor(type))
            {
                OutpostConversionExtension ext = wod.GetModExtension<OutpostConversionExtension>();
                TestAssert.IsNotNull(ext, $"{wod.defName} was returned but has no OutpostConversionExtension");
                TestAssert.IsTrue(ext.allowAnySettlementType || ext.GetExplicitTypes().Contains(type),
                    $"{wod.defName} was returned but does not allow {type.defName}");
            }
        }
    }
}
