using FactionColonies;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Coverage for <see cref="WorldComponent_VOETracker"/>, the transient redirect-flag store that stops the
    /// tax-delivery interceptor re-redirecting an event it already redirected. The set/clear round-trip is the
    /// production handshake (set on redirect, cleared when the re-queued event is consumed the same tick) and
    /// is non-destructive. The self-clearing expiry — the guard against a flag stranded by a foreign interceptor
    /// — needs the game tick to advance, so it lives in the destructive tier and restores the tick counter.
    /// A fabricated far-off tile id is used as the key so a real in-flight redirect can't collide.
    /// </summary>
    public static class VOERedirectTrackerTests
    {
        // A tile id no real world object will own, used purely as a dictionary key.
        private static PlanetTile FakeTile()
        {
            return new PlanetTile(int.MaxValue - 7, Find.WorldGrid.Surface);
        }

        [EmpireTest("VOE.Redirect")]
        public static void Redirect_SetClearRoundTrip()
        {
            if (Find.World is null) TestAssert.Skip("No world loaded");
            PlanetTile tile = FakeTile();

            try
            {
                TestAssert.IsFalse(WorldComponent_VOETracker.IsRedirected(tile),
                    "An untouched tile should not read as redirected");

                WorldComponent_VOETracker.SetRedirected(tile);
                TestAssert.IsTrue(WorldComponent_VOETracker.IsRedirected(tile),
                    "A tile should read redirected right after SetRedirected");

                WorldComponent_VOETracker.ClearRedirected(tile);
                TestAssert.IsFalse(WorldComponent_VOETracker.IsRedirected(tile),
                    "ClearRedirected should drop the flag (the same-tick consume handshake)");
            }
            finally
            {
                WorldComponent_VOETracker.ClearRedirected(tile);
            }
        }

        [EmpireDestructiveTest("VOE.Redirect")]
        public static void Redirect_Flag_SelfClearsAfterExpiry()
        {
            if (Find.World is null) TestAssert.Skip("No world loaded");
            PlanetTile tile = FakeTile();

            int t0 = Find.TickManager.TicksGame;
            try
            {
                WorldComponent_VOETracker.SetRedirected(tile);
                TestAssert.IsTrue(WorldComponent_VOETracker.IsRedirected(tile), "Flag should be live after SetRedirected");

                // The flag lifetime is ~2500 ticks; advance well past it. IsRedirected must self-clear the
                // stale entry so a stranded flag can never suppress a settlement's next genuine redirect.
                Find.TickManager.DebugSetTicksGame(t0 + 5000);
                TestAssert.IsFalse(WorldComponent_VOETracker.IsRedirected(tile),
                    "An expired redirect flag should self-clear on read");
            }
            finally
            {
                Find.TickManager.DebugSetTicksGame(t0);
                WorldComponent_VOETracker.ClearRedirected(tile);
            }
        }
    }
}
