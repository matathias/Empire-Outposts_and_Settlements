using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Adds a small threat level contribution for each player-owned outpost.
    /// Defensive outposts contribute at a higher rate since they represent visible military assets.
    /// </summary>
    public class OutpostThreatContributor : IThreatScalingContributor
    {
        private const float DefensiveMultiplier = 1.5f;

        public double GetAdditiveContribution(FactionFC faction)
        {
            if (EmpireVOESettings.disableIntegration) return 0;

            double total = 0;
            foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
            {
                Outpost outpost = wo as Outpost;
                if (outpost is null) continue;
                if (outpost.Faction != Faction.OfPlayer) continue;

                float typeMultiplier = (outpost is Outpost_Defensive) ? DefensiveMultiplier : 1.0f;
                total += outpost.PawnCount * EmpireVOESettings.outpostThreatPerPawn * typeMultiplier;
            }

            return total;
        }

        public double GetMultiplicativeContribution(FactionFC faction)
        {
            return 1.0;
        }
    }
}
