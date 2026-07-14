using FactionColonies;
using Outposts;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    /* DESTRUCTIVE: exercises the layer-filtered nearby-object counting behind VOE's Town spawn requirement.
       Creates a transient outpost to count against and toggles townExcludeTowns; both are restored/removed.
       (The xenotype-pure Produce patch is left out — its recruit generation is random and heavy to stage.) */
    public static class VOETownRequirementDestructiveTests
    {
        /// <summary>
        /// NearbyOutposts counts same-layer outposts within townRange, so an outpost sitting on the query tile
        /// (distance 0) is always counted. This is the counter the CanSpawnOnWith requirement gates on.
        /// </summary>
        [EmpireDestructiveTest("VOE.Destructive.Town")]
        public static void TownRequirements_NearbyOutposts_CountsOutpostOnTile()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (EmpireVOESettings.townRange <= 0) TestAssert.Skip("townRange is non-positive; nothing can be in range");

            Outpost outpost = VOEDestructiveTestUtil.CreateTransientOutpost();
            if (outpost is null) TestAssert.Skip("Could not create a transient outpost");
            PlanetTile tile = outpost.Tile;

            bool excludeBefore = EmpireVOESettings.townExcludeTowns;
            try
            {
                // Count towns too, so the assertion holds even if the transient def happens to be a Town.
                EmpireVOESettings.townExcludeTowns = false;
                TestAssert.GreaterThan(OutpostTownRequirements.NearbyOutposts(tile), 0,
                    "An outpost on the query tile should be counted as nearby");
            }
            finally
            {
                EmpireVOESettings.townExcludeTowns = excludeBefore;
                VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "TownRequirements_NearbyOutposts");
        }

        /// <summary>
        /// Excluding Town outposts from the nearby count can only lower or equal it, never raise it.
        /// </summary>
        [EmpireDestructiveTest("VOE.Destructive.Town")]
        public static void TownRequirements_ExcludeTowns_NeverIncreasesCount()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            Outpost outpost = VOEDestructiveTestUtil.CreateTransientOutpost();
            if (outpost is null) TestAssert.Skip("Could not create a transient outpost");
            PlanetTile tile = outpost.Tile;

            bool excludeBefore = EmpireVOESettings.townExcludeTowns;
            try
            {
                EmpireVOESettings.townExcludeTowns = false;
                int all = OutpostTownRequirements.NearbyOutposts(tile);
                EmpireVOESettings.townExcludeTowns = true;
                int excludingTowns = OutpostTownRequirements.NearbyOutposts(tile);

                TestAssert.LessThanOrEqual(excludingTowns, all,
                    "Excluding towns must not increase the nearby-outpost count");
            }
            finally
            {
                EmpireVOESettings.townExcludeTowns = excludeBefore;
                VOEDestructiveTestUtil.SafeRemoveOutpost(outpost); // pure probe
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "TownRequirements_ExcludeTowns");
        }
    }
}
