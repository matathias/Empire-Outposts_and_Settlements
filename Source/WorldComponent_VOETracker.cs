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
        // Short-lived redirect flags — not serialized. The redirect handshake normally clears a flag
        // the same tick it is consumed (OutpostDeliveryInterceptor.OnTaxEventCreated), but a foreign
        // ITaxDeliveryInterceptor could consume the re-queued event first and strand the flag, which
        // would make the settlement's next genuine tax event skip its outpost redirect. Storing an
        // expiry tick makes the flag self-clear so it can never leak past its intended one-cycle life.
        private const int RedirectFlagLifetimeTicks = 2500; // ~1 in-game hour; a redirect resolves well within this.
        private static readonly Dictionary<PlanetTile, int> redirectExpiryTick = new Dictionary<PlanetTile, int>();

        public WorldComponent_VOETracker(World world) : base(world)
        {
        }

        public override void FinalizeInit(bool fromload)
        {
            base.FinalizeInit(fromload);
            redirectExpiryTick.Clear();
        }

        public static bool IsRedirected(PlanetTile settlementTile)
        {
            if (!redirectExpiryTick.TryGetValue(settlementTile, out int expiry))
                return false;
            if (Find.TickManager.TicksGame > expiry)
            {
                redirectExpiryTick.Remove(settlementTile);
                return false;
            }
            return true;
        }

        public static void SetRedirected(PlanetTile settlementTile)
        {
            redirectExpiryTick[settlementTile] = Find.TickManager.TicksGame + RedirectFlagLifetimeTicks;
        }

        public static void ClearRedirected(PlanetTile settlementTile)
        {
            redirectExpiryTick.Remove(settlementTile);
        }
    }
}
