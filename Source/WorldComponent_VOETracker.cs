using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Retained as a minimal utility for transient redirect flags used by the
    /// delivery interceptor to prevent re-redirect loops within a single tick.
    /// Auto-discovered by RimWorld (WorldComponent subclasses are instantiated automatically).
    /// </summary>
    public class WorldComponent_VOETracker : WorldComponent
    {
        // One-tick flags consumed same tick they are set — not serialized.
        private static readonly HashSet<int> redirectedSettlements = new HashSet<int>();

        public WorldComponent_VOETracker(World world) : base(world)
        {
        }

        public override void FinalizeInit(bool fromload)
        {
            base.FinalizeInit(fromload);
            redirectedSettlements.Clear();
        }

        public static bool IsRedirected(int settlementTile)
        {
            return redirectedSettlements.Contains(settlementTile);
        }

        public static void SetRedirected(int settlementTile)
        {
            redirectedSettlements.Add(settlementTile);
        }

        public static void ClearRedirected(int settlementTile)
        {
            redirectedSettlements.Remove(settlementTile);
        }
    }
}
