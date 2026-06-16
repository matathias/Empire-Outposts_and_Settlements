using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// Per-encampment precomputed Medicine data backing the merc heal-rate bonus.
    /// </summary>
    public class EncampmentData
    {
        public readonly Outpost_Encampment encampment;
        public readonly double healRateContribution;

        public EncampmentData(Outpost_Encampment enc)
        {
            encampment = enc;
            List<Pawn> pawns = enc.CapablePawns.ToList();
            if (pawns.Count == 0)
            {
                healRateContribution = 0;
                return;
            }

            foreach (Pawn p in pawns)
            {
                if (p.skills is null) continue;
                int level = EffectiveLevel(p.skills.GetSkill(SkillDefOf.Medicine)?.Level ?? 0);
                healRateContribution += level * EmpireVOESettings.encampmentHealRatePerLevel;
            }
        }

        private static int EffectiveLevel(int level)
        {
            return level >= EmpireVOESettings.skillFloor ? level : 0;
        }
    }

    /// <summary>
    /// Per-settlement cache entry containing nearby encampment data.
    /// </summary>
    public class EncampmentCacheEntry
    {
        public readonly List<EncampmentData> encampments;
        public readonly double totalHealRateBonus;

        public EncampmentCacheEntry(WorldSettlementFC settlement)
        {
            encampments = new List<EncampmentData>();
            totalHealRateBonus = 0;

            foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
            {
                if (!(wo is Outpost_Encampment enc)) continue;
                if (enc.PawnCount == 0) continue;
                float distance = Find.WorldGrid.ApproxDistanceInTiles(enc.Tile, settlement.Tile);
                if (distance > EmpireVOESettings.encampmentRange) continue;

                EncampmentData data = new EncampmentData(enc);
                encampments.Add(data);
                totalHealRateBonus += data.healRateContribution;
            }
        }
    }

    /// <summary>
    /// Static lazy cache mapping settlement tiles to their nearby encampment data.
    /// Invalidated on encampment pawn roster changes, outpost creation/destruction, and settings changes.
    /// </summary>
    public static class EncampmentCache
    {
        private static readonly Dictionary<PlanetTile, EncampmentCacheEntry> cache = new Dictionary<PlanetTile, EncampmentCacheEntry>();

        public static EncampmentCacheEntry GetOrBuild(WorldSettlementFC settlement)
        {
            if (settlement is null) return null;
            if (cache.TryGetValue(settlement.Tile, out EncampmentCacheEntry entry))
                return entry;

            entry = new EncampmentCacheEntry(settlement);
            cache[settlement.Tile] = entry;
            return entry;
        }

        public static void Invalidate()
        {
            cache.Clear();
        }
    }
}
