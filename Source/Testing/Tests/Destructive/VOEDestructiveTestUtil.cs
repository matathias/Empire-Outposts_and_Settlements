using System;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// EmpireVOE-specific helpers for the destructive test tier. Builds on the base mod's
    /// <see cref="DestructiveTestUtil"/> (faction guard + invariant battery) and adds outpost fixtures:
    /// finding a live player outpost, or creating a transient one with the same recipe VEF's
    /// <c>Dialog_CreateCamp</c> uses. Transient outposts are NOT cleaned up unless the test calls
    /// <see cref="SafeRemoveOutpost"/>.
    /// </summary>
    public static class VOEDestructiveTestUtil
    {
        /// <summary>First player-owned outpost in the world, or null (caller skips).</summary>
        public static Outpost FirstPlayerOutpost()
        {
            return Find.WorldObjects.AllWorldObjects
                .OfType<Outpost>()
                .FirstOrDefault(o => o.Faction == Faction.OfPlayer);
        }

        /// <summary>First player outpost with a non-empty conversion mapping, or null (caller skips).</summary>
        public static Outpost FirstConvertibleOutpost()
        {
            return Find.WorldObjects.AllWorldObjects
                .OfType<Outpost>()
                .FirstOrDefault(o => o.Faction == Faction.OfPlayer
                    && o.def.GetModExtension<OutpostConversionExtension>() is object
                    && OutpostConversionUtil.GetConvertibleTypes(o).Count > 0);
        }

        /// <summary>
        /// A player-owned <see cref="WorldObjectDef"/> whose worldObjectClass is an <see cref="Outpost"/>.
        /// When <paramref name="requireConversion"/> is true, only defs carrying an
        /// <see cref="OutpostConversionExtension"/> are considered. Null if none exist.
        /// </summary>
        public static WorldObjectDef FindOutpostDef(bool requireConversion)
        {
            foreach (WorldObjectDef wod in DefDatabase<WorldObjectDef>.AllDefsListForReading)
            {
                if (wod.worldObjectClass is null) continue;
                if (!typeof(Outpost).IsAssignableFrom(wod.worldObjectClass)) continue;
                if (requireConversion && wod.GetModExtension<OutpostConversionExtension>() is null) continue;
                return wod;
            }
            return null;
        }

        /// <summary>
        /// Creates a real player outpost on a randomly-found valid surface tile, mirroring VEF's
        /// <c>Dialog_CreateCamp</c> recipe (make -> name -> tile -> faction -> register -> add pawns).
        /// Returns null if no suitable def/tile was found or creation threw (caller skips). This mutates
        /// live world state.
        /// </summary>
        public static Outpost CreateTransientOutpost(WorldObjectDef def = null, int pawnCount = 3, bool requireConversion = false)
        {
            if (def is null) def = FindOutpostDef(requireConversion);
            if (def is null) return null;

            try
            {
                for (int attempt = 0; attempt < 500; attempt++)
                {
                    PlanetTile tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
                    if (!tile.Valid) continue;
                    if (Find.WorldObjects.AnyWorldObjectAt(tile)) continue;

                    Outpost outpost = (Outpost)WorldObjectMaker.MakeWorldObject(def);
                    outpost.Name = "VOE Test Outpost";
                    outpost.Tile = tile;
                    outpost.SetFaction(Faction.OfPlayer);
                    Find.WorldObjects.Add(outpost);

                    for (int i = 0; i < pawnCount; i++)
                    {
                        Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        outpost.AddPawn(pawn);
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
}
