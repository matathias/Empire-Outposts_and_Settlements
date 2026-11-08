using System.Text;
using LudeonTK;
using Outposts;
using UnityEngine;

namespace EmpireVOE;

public static class VOEDebugActions
{
    [EmpireDebugAction("Empire - VOE", "Attack VOE Outpost", allowedGameStates = AllowedGameStates.Playing)]
    private static void AttackVOEOutpost()
    {
        IReadOnlyList<IRaidTarget> targets = RaidTargetRegistry.Targets;
        if (targets.Count == 0)
        {
            Messages.Message("No registered raid targets.", MessageTypeDefOf.RejectInput);
            return;
        }

        List<DebugMenuOption> list = [];
        foreach (IRaidTarget target in targets)
        {
            IRaidTarget t = target;
            list.Add(new DebugMenuOption(t.Name, DebugMenuOptionMode.Action, delegate
            {
                Faction enemyFaction = Find.FactionManager.RandomEnemyFaction();
                if (enemyFaction is null)
                {
                    Messages.Message("No enemy faction found.", MessageTypeDefOf.RejectInput);
                    return;
                }

                List<DebugMenuOption> levelList = [];
                for (int level = 1; level <= 10; level++)
                {
                    int chosenLevel = level;
                    levelList.Add(new DebugMenuOption("Level " + chosenLevel, DebugMenuOptionMode.Action, delegate
                    {
                        double efficiency;
                        MilitaryDeploymentUtil.GetTechLevelBaseline(
                            enemyFaction.def.techLevel, out double _, out efficiency);
                        MilitaryForce attackingForce = new(chosenLevel, efficiency, null, enemyFaction);
                        VOELog.MessageForce("Debug - Attack VOE Outpost - " + t.Name
                            + " (level " + chosenLevel + ", efficiency " + efficiency + ")");
                        MilitaryOperationsUtil.AttackRaidTarget(attackingForce, t, enemyFaction);
                    }));
                }
                Find.WindowStack.Add(new Dialog_DebugOptionListLister(levelList));
            }));
        }

        Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
    }

    /* Spawns one fully-manned outpost of every registered outpost type, clustered near an anchor tile. */
    [DebugAction("Empire - VOE", "Spawn one of each Outpost", allowedGameStates = AllowedGameStates.Playing)]
    private static void SpawnOneOfEachOutpost()
    {
        List<WorldObjectDef> defs = AllOutpostDefs();
        if (defs.Count == 0)
        {
            VOELog.MessageForce("No outpost WorldObjectDefs found.");
            return;
        }

        PlanetTile anchor = ResolveAnchorTile();
        List<PlanetTile> tiles = [];
        bool foundAll = TryFindEmptyTilesNear(anchor, defs.Count, null, tiles);
        if (tiles.Count == 0)
        {
            VOELog.MessageForce("Could not find any empty tiles near the anchor to place outposts.");
            return;
        }
        if (!foundAll)
            VOELog.Warning($"Only found {tiles.Count} empty tiles for {defs.Count} outpost types; spawning a subset.");

        int spawned = 0;
        StringBuilder sb = new();
        for (int i = 0; i < defs.Count && i < tiles.Count; i++)
        {
            Outpost o = SpawnMannedOutpost(defs[i], tiles[i]);
            if (o is null)
            {
                sb.AppendLine($"  FAILED: {defs[i].defName}");
                continue;
            }
            spawned++;
            sb.AppendLine($"  {defs[i].defName} - {o.AllPawns.Count()} pawns");
        }
        VOELog.MessageForce($"Spawned {spawned}/{defs.Count} outposts near {anchor}:\n{sb}");
    }

    /* Spawns one of each link-capable outpost within range of a settlement (creating one if needed) and links each. */
    [EmpireDebugAction("Empire - VOE", "Spawn & link resource outposts", allowedGameStates = AllowedGameStates.Playing)]
    private static void SpawnAndLinkOutposts()
    {
        List<WorldObjectDef> defs = LinkableOutpostDefs();
        if (defs.Count == 0)
        {
            VOELog.MessageForce("No link-capable outpost defs (none carry an OutpostResourceLinkExtension with resources).");
            return;
        }

        // Target the first existing settlement, or create one if the empire has none yet.
        WorldSettlementFC settlement = FindFC.Settlements?.FirstOrDefault();
        if (settlement is null)
        {
            PlanetTile tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
            if (!tile.Valid)
            {
                VOELog.MessageForce("No existing settlement and could not find a tile to create one.");
                return;
            }
            settlement = ColonyUtil.CreatePlayerColonySettlement(tile, null, "VOE Debug Settlement");
        }
        if (settlement is null)
        {
            VOELog.MessageForce("Could not resolve or create a target settlement (no Empire faction?).");
            return;
        }

        WorldObjectComp_ResourceLink link = settlement.GetComponent<WorldObjectComp_ResourceLink>();
        if (link is null)
        {
            VOELog.MessageForce($"Settlement '{settlement.Name}' has no WorldObjectComp_ResourceLink; cannot link.");
            return;
        }

        // Only accept tiles within the outpost link range (same layer + distance), so every link takes.
        float range = EmpireVOESettings.resourceLinkRange;
        PlanetTile settlementTile = settlement.Tile;
        Predicate<PlanetTile> inRange = t => t.ApproxDistanceSameLayer(settlementTile) <= range;

        List<PlanetTile> tiles = [];
        TryFindEmptyTilesNear(settlementTile, defs.Count, inRange, tiles);
        if (tiles.Count == 0)
        {
            VOELog.MessageForce($"No empty tiles within link range ({range}) of '{settlement.Name}'.");
            return;
        }

        int linked = 0;
        StringBuilder sb = new();
        for (int i = 0; i < defs.Count && i < tiles.Count; i++)
        {
            Outpost o = SpawnMannedOutpost(defs[i], tiles[i]);
            if (o is null)
            {
                sb.AppendLine($"  FAILED to spawn: {defs[i].defName}");
                continue;
            }
            link.ToggleLink(o);
            bool ok = ResourceLinkUtil.IsLinked(o);
            if (ok) linked++;
            sb.AppendLine($"  {defs[i].defName} - {(ok ? "LINKED" : "spawned but NOT linked")}");
        }
        VOELog.MessageForce($"Linked {linked}/{tiles.Count} outposts to '{settlement.Name}':\n{sb}");
    }

    /*-*-*- Helpers -*-*-*/

    /// <summary>Every WorldObjectDef whose worldObjectClass is an <see cref="Outpost"/>.</summary>
    private static List<WorldObjectDef> AllOutpostDefs()
    {
        return DefDatabase<WorldObjectDef>.AllDefsListForReading
            .Where(d => d.worldObjectClass is not null && typeof(Outpost).IsAssignableFrom(d.worldObjectClass))
            .ToList();
    }

    /// <summary>The outpost defs that can be resource-linked (carry an extension declaring at least one resource).</summary>
    private static List<WorldObjectDef> LinkableOutpostDefs()
    {
        return AllOutpostDefs()
            .Where(d => d.GetModExtension<OutpostResourceLinkExtension>()?.resources?.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Creates a player outpost of <paramref name="def"/> at <paramref name="tile"/> and staffs it with enough
    /// skilled pawns to meet its <see cref="OutpostExtension.MinPawns"/> and <c>RequiredSkills</c>. Returns null
    /// (and logs) on failure so a single bad def can't abort the whole batch.
    /// </summary>
    private static Outpost SpawnMannedOutpost(WorldObjectDef def, PlanetTile tile)
    {
        try
        {
            Outpost outpost = (Outpost)WorldObjectMaker.MakeWorldObject(def);
            outpost.Name = def.label + " (debug)";
            outpost.Tile = tile;
            outpost.SetFaction(Faction.OfPlayer);
            Find.WorldObjects.Add(outpost);

            OutpostExtension ext = def.GetModExtension<OutpostExtension>();
            int target = Mathf.Max(ext?.MinPawns ?? 0, 3);
            for (int i = 0; i < target; i++)
                AddBoostedPawn(outpost);

            // RequiredSkills founding gate: for each entry the SUM of that skill's level across all pawns must
            // reach Count. Add extra (skill-boosted) pawns until met, bounded so a pathological def can't spin.
            if (ext?.RequiredSkills is not null)
            {
                int extra = 0;
                while (extra < 20 && RequiredSkillsUnmet(outpost, ext))
                {
                    if (!AddBoostedPawn(outpost)) break;
                    extra++;
                }
                if (RequiredSkillsUnmet(outpost, ext))
                    VOELog.Warning($"Spawned {def.defName} could not meet all RequiredSkills after {extra} extra pawns.");
            }
            return outpost;
        }
        catch (Exception ex)
        {
            VOELog.Warning($"SpawnMannedOutpost({def?.defName}) threw (ignored): {ex}");
            return null;
        }
    }

    private static bool RequiredSkillsUnmet(Outpost outpost, OutpostExtension ext)
    {
        return ext.RequiredSkills.Any(rq => rq.Skill is not null
            && outpost.AllPawns.Sum(p => p.skills?.GetSkill(rq.Skill)?.Level ?? 0) < rq.Count);
    }

    /// <summary>Generates a colonist with every non-disabled skill maxed, then adds it; discards a rejected pawn.</summary>
    private static bool AddBoostedPawn(Outpost outpost)
    {
        Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        if (pawn.skills is not null)
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                if (skill.TotallyDisabled) continue;   // Level stays 0 for disabled skills, so only touch enabled ones
                skill.Level = 20;
            }
        if (!outpost.AddPawn(pawn))
        {
            pawn.Destroy(DestroyMode.Vanish);
            return false;
        }
        return true;
    }

    /// <summary>Anchor for clustered placement: first settlement, else the selected surface tile, else a random one.</summary>
    private static PlanetTile ResolveAnchorTile()
    {
        List<WorldSettlementFC> settlements = FindFC.Settlements;
        if (settlements is not null && settlements.Count > 0 && settlements[0].Tile.Valid)
            return settlements[0].Tile;

        PlanetTile selected = Find.WorldSelector.SelectedTile;
        if (selected.Valid && selected.Layer == Find.WorldGrid.Surface)
            return selected;

        return TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
    }

    /// <summary>
    /// Breadth-first search outward from <paramref name="anchor"/> collecting up to <paramref name="count"/> empty
    /// tiles that also pass <paramref name="extraFilter"/> (null accepts any). A tile is only taken when it is
    /// empty AND none of its neighbors holds a world object or an already-chosen tile, mirroring vanilla's rule
    /// that world objects never sit adjacent to one another. Returns whether the full count was found; partial
    /// results stay in <paramref name="result"/>.
    /// </summary>
    private static bool TryFindEmptyTilesNear(PlanetTile anchor, int count, Predicate<PlanetTile> extraFilter, List<PlanetTile> result)
    {
        result.Clear();
        if (!anchor.Valid || count <= 0) return false;

        HashSet<PlanetTile> visited = [];
        HashSet<PlanetTile> chosen = [];
        Queue<PlanetTile> frontier = new();
        List<PlanetTile> neighbors = [];
        visited.Add(anchor);
        frontier.Enqueue(anchor);
        int guard = 0;

        while (frontier.Count > 0 && result.Count < count && guard < 5000)
        {
            guard++;
            PlanetTile current = frontier.Dequeue();

            neighbors.Clear();
            Find.WorldGrid.GetTileNeighbors(current, neighbors);

            if (current != anchor
                && !Find.WorldObjects.AnyWorldObjectAt(current)
                && (extraFilter is null || extraFilter(current))
                && !NeighborOccupied(neighbors, chosen))
            {
                result.Add(current);
                chosen.Add(current);
            }

            foreach (PlanetTile n in neighbors)
            {
                if (visited.Add(n)) frontier.Enqueue(n);
            }
        }
        return result.Count >= count;
    }

    /// <summary>Whether any neighbor already holds a world object or a tile chosen earlier this search, so that
    /// placing here would make two world objects adjacent (vanilla forbids this).</summary>
    private static bool NeighborOccupied(List<PlanetTile> neighbors, HashSet<PlanetTile> chosen)
    {
        foreach (PlanetTile n in neighbors)
        {
            if (chosen.Contains(n)) return true;
            if (Find.WorldObjects.AnyWorldObjectAt(n)) return true;
        }
        return false;
    }
}
