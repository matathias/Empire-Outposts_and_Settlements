using Outposts;
using VOE;

namespace EmpireVOE;

/// <summary>
/// EmpireVOE-specific helpers for the destructive test tier. Builds on the base mod's
/// <see cref="DestructiveTestUtil"/> (faction guard + invariant battery) and adds outpost fixtures:
/// finding a live player outpost, or creating a transient one with the same recipe VEF's
/// <c>Dialog_CreateCamp</c> uses. VOE is a hard dependency, so its outpost defs are always present:
/// the <c>Require*</c>/<c>Ensure*</c> helpers fail the test when a fixture cannot be built.
/// Transient outposts are NOT cleaned up unless the test calls <see cref="SafeRemoveOutpost"/>.
/// </summary>
public static class VOEDestructiveTestUtil
{
    /// <summary>First player-owned outpost in the world, or null.</summary>
    public static Outpost FirstPlayerOutpost()
    {
        return Find.WorldObjects.AllWorldObjects
            .OfType<Outpost>()
            .FirstOrDefault(o => o.Faction == Faction.OfPlayer);
    }

    /// <summary>First player outpost with a non-empty conversion mapping, or null.</summary>
    public static Outpost FirstConvertibleOutpost()
    {
        return Find.WorldObjects.AllWorldObjects
            .OfType<Outpost>()
            .FirstOrDefault(o => o.Faction == Faction.OfPlayer
                && o.def.GetModExtension<OutpostConversionExtension>() is not null
                && OutpostConversionUtil.GetConvertibleTypes(o).Count > 0);
    }

    /// <summary>The first <see cref="WorldObjectDef"/> whose worldObjectClass is an
    /// <see cref="Outpost_Defensive"/>, or null.</summary>
    public static WorldObjectDef FindDefensiveOutpostDef()
    {
        return DefDatabase<WorldObjectDef>.AllDefsListForReading.FirstOrDefault(d =>
            d.worldObjectClass is not null && typeof(Outpost_Defensive).IsAssignableFrom(d.worldObjectClass));
    }

    /// <summary>The defensive outpost def VOE ships. Fails the test if it is missing.</summary>
    public static WorldObjectDef RequireDefensiveOutpostDef() =>
        TestAssert.NotNull(FindDefensiveOutpostDef(), "VOE ships a defensive outpost def, but none is loaded");

    /// <summary>
    /// A live player <see cref="Outpost_Defensive"/>, or a freshly created transient one carrying the
    /// Empire defensive comp. Fails the test if neither is possible. Shared by every defensive-outpost
    /// test so the fixture (and its VEF create recipe) lives in one place.
    /// </summary>
    public static Outpost_Defensive EnsureDefensiveOutpost()
    {
        Outpost_Defensive existing = Find.WorldObjects.AllWorldObjects
            .OfType<Outpost_Defensive>()
            .FirstOrDefault(o => o.Faction == Faction.OfPlayer);
        return existing ?? RequireTransientDefensiveOutpost();
    }

    /// <summary>
    /// A live player <see cref="Outpost_Defensive"/> with at least one capable pawn, or a freshly created
    /// transient one with a capable garrison. Fails the test if neither is possible.
    /// </summary>
    public static Outpost_Defensive EnsureMannedDefensiveOutpost()
    {
        Outpost_Defensive existing = Find.WorldObjects.AllWorldObjects
            .OfType<Outpost_Defensive>()
            .FirstOrDefault(o => o.Faction == Faction.OfPlayer && o.CapablePawns.Any());
        return existing ?? RequireTransientDefensiveOutpost();
    }

    /// <summary>A transient defensive outpost with <paramref name="pawnCount"/> capable pawns. Fails the
    /// test if it cannot be built.</summary>
    public static Outpost_Defensive RequireTransientDefensiveOutpost(int pawnCount = 3) =>
        TestAssert.NotNull(RequireTransientOutpost(RequireDefensiveOutpostDef(), pawnCount) as Outpost_Defensive,
            "The defensive outpost def did not produce an Outpost_Defensive");

    /// <summary>
    /// A player-owned <see cref="WorldObjectDef"/> whose worldObjectClass is an <see cref="Outpost"/>.
    /// When <paramref name="requireConversion"/> is true, only defs carrying an
    /// <see cref="OutpostConversionExtension"/> are considered, preferring one that allows any settlement
    /// type (the explicit mappings may name types from a submod that is not loaded). Null if none exist.
    /// </summary>
    public static WorldObjectDef FindOutpostDef(bool requireConversion)
    {
        WorldObjectDef fallback = null;
        foreach (WorldObjectDef wod in DefDatabase<WorldObjectDef>.AllDefsListForReading)
        {
            if (wod.worldObjectClass is null) continue;
            if (!typeof(Outpost).IsAssignableFrom(wod.worldObjectClass)) continue;
            if (!requireConversion) return wod;

            OutpostConversionExtension ext = wod.GetModExtension<OutpostConversionExtension>();
            if (ext is null) continue;
            if (ext.allowAnySettlementType) return wod;
            fallback ??= wod;
        }
        return fallback;
    }

    /// <summary>
    /// Creates a real player outpost on a randomly-found valid surface tile, mirroring VEF's
    /// <c>Dialog_CreateCamp</c> recipe (make -> name -> tile -> faction -> register -> add pawns).
    /// Generates colonists until <paramref name="pawnCount"/> of them are capable at the outpost (a
    /// random colonist can be rejected or lack the outpost's skills), within a bounded number of tries.
    /// With <paramref name="near"/>, the tile is instead a free passable tile within
    /// <paramref name="maxDistance"/> tiles of it (on its layer).
    /// Returns null if no suitable def/tile was found or creation threw; prefer
    /// <see cref="RequireTransientOutpost"/>, which fails the test instead. This mutates live world state.
    /// </summary>
    public static Outpost CreateTransientOutpost(WorldObjectDef def = null, int pawnCount = 3, bool requireConversion = false,
        PlanetTile? near = null, int maxDistance = 0)
    {
        def ??= FindOutpostDef(requireConversion);
        if (def is null) return null;

        try
        {
            for (int attempt = 0; attempt < 500; attempt++)
            {
                PlanetTile tile;
                if (near is PlanetTile root)
                {
                    if (!TileFinder.TryFindPassableTileWithTraversalDistance(root, 1, maxDistance, out tile,
                            t => !Find.WorldObjects.AnyWorldObjectAt(t)))
                        return null;
                }
                else
                {
                    tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
                }
                if (!tile.Valid) continue;
                if (Find.WorldObjects.AnyWorldObjectAt(tile)) continue;

                Outpost outpost = (Outpost)WorldObjectMaker.MakeWorldObject(def);
                outpost.Name = "VOE Test Outpost";
                outpost.Tile = tile;
                outpost.SetFaction(Faction.OfPlayer);
                Find.WorldObjects.Add(outpost);

                for (int i = 0; i < pawnCount * 10 && outpost.CapablePawns.Count() < pawnCount; i++)
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    // AddPawn can reject a pawn (ideology/vehicle/skill gate); discard the leftover so
                    // the fixture doesn't leak a holderless pawn.
                    if (!outpost.AddPawn(pawn))
                        pawn.Destroy(DestroyMode.Vanish);
                }
                return outpost;
            }
        }
        catch (Exception ex)
        {
            VOELog.Warning($"VOEDestructiveTestUtil.CreateTransientOutpost threw (ignored): {ex}");
        }
        return null;
    }

    /// <summary>
    /// <see cref="CreateTransientOutpost"/> that fails the test if no outpost could be created or it ended
    /// up with fewer than <paramref name="pawnCount"/> capable pawns.
    /// </summary>
    public static Outpost RequireTransientOutpost(WorldObjectDef def = null, int pawnCount = 3, bool requireConversion = false,
        PlanetTile? near = null, int maxDistance = 0)
    {
        def ??= TestAssert.NotNull(FindOutpostDef(requireConversion),
            requireConversion ? "No convertible VOE outpost def is loaded" : "No VOE outpost def is loaded");
        Outpost outpost = TestAssert.NotNull(CreateTransientOutpost(def, pawnCount, near: near, maxDistance: maxDistance),
            $"Could not create a transient {def.defName} outpost");
        int capable = outpost.CapablePawns.Count();
        if (capable < pawnCount)
        {
            SafeRemoveOutpost(outpost);
            TestAssert.Fail($"Transient {def.defName} outpost has {capable} capable pawns; the fixture needs {pawnCount}");
        }
        return outpost;
    }

    /// <summary>
    /// A transient, garrisoned outpost of <paramref name="def"/> (default: a convertible def) whose tile
    /// admits at least one settlement type it can convert into. Retries on a fresh tile when the first
    /// lands somewhere no type allows; fails the test if none does.
    /// </summary>
    public static Outpost RequireConvertibleOutpost(WorldObjectDef def = null)
    {
        def ??= TestAssert.NotNull(FindOutpostDef(requireConversion: true), "No convertible VOE outpost def is loaded");
        TestAssert.NotNull(def.GetModExtension<OutpostConversionExtension>(),
            $"{def.defName} carries no OutpostConversionExtension");

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Outpost outpost = RequireTransientOutpost(def);
            if (OutpostConversionUtil.GetConvertibleTypes(outpost).Count > 0) return outpost;
            SafeRemoveOutpost(outpost);
        }
        TestAssert.Fail($"No transient {def.defName} outpost landed on a tile any of its settlement types allows");
        return null;
    }

    /// <summary>Removes an outpost via the real teardown path, swallowing+logging any exception so
    /// cleanup-as-an-action never masks the test's actual result.</summary>
    public static void SafeRemoveOutpost(Outpost outpost)
    {
        if (outpost is null || outpost.Destroyed) return;
        try
        {
            outpost.Destroy();
        }
        catch (Exception ex)
        {
            VOELog.Warning($"VOEDestructiveTestUtil.SafeRemoveOutpost threw (ignored): {ex}");
        }
    }
}
