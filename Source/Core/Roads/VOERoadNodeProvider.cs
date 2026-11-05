using Outposts;

namespace EmpireVOE;

/// <summary>
/// Feeds player-owned VOE outpost tiles into Empire's road-network MST so roads are
/// built to (and among) outposts the same way they are for settlements. Stateless
/// singleton — registered once via <see cref="VOECompatInit"/>; the surface-only
/// filter is applied centrally by <see cref="RoadNodeProviderRegistry.CollectInto"/>.
/// </summary>
public class VOERoadNodeProvider : IRoadNodeProvider
{
    public static readonly VOERoadNodeProvider Instance = new();

    public IEnumerable<PlanetTile> GetRoadNodeTiles()
    {
        if (!EmpireVOESettings.RoadsActive || Find.World is null) yield break;
        foreach (Outpost outpost in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
        {
            if (outpost.Faction != Faction.OfPlayer) continue;
            // Respect the per-outpost road opt-in (default true if the comp is somehow absent).
            WorldObjectComp_EmpireOutpost comp = outpost.GetComponent<WorldObjectComp_EmpireOutpost>();
            if (comp is not null && !comp.buildRoads) continue;
            yield return outpost.Tile;
        }
    }
}
