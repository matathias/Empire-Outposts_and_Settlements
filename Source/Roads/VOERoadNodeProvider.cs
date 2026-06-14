using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Feeds player-owned VOE outpost tiles into Empire's road-network MST so roads are
    /// built to (and among) outposts the same way they are for settlements. Stateless
    /// singleton — registered once via <see cref="VOECompatInit"/>; the surface-only
    /// filter is applied centrally by <see cref="RoadNodeProviderRegistry.CollectInto"/>.
    /// </summary>
    public class VOERoadNodeProvider : IRoadNodeProvider
    {
        public static readonly VOERoadNodeProvider Instance = new VOERoadNodeProvider();

        public IEnumerable<PlanetTile> GetRoadNodeTiles()
        {
            if (!EmpireVOESettings.RoadsActive || Find.World is null) yield break;
            foreach (Outpost outpost in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
            {
                if (outpost.Faction == Faction.OfPlayer)
                    yield return outpost.Tile;
            }
        }
    }
}
