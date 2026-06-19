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
    /// IRaidTarget wrapper for any VOE outpost. Makes the outpost eligible for
    /// Empire's raid targeting system with the same 24-hour warning as settlements.
    /// MilitaryLevel is priced off the live garrison via <see cref="OutpostMilitaryUtil"/>
    /// (the base mod's cost->level curve), so it stays in lockstep with the value shown in the
    /// military tab (DefensiveAutoDefender) and the world-map infobox.
    /// </summary>
    public class OutpostRaidTarget : IRaidTarget
    {
        private readonly Outpost outpost;

        public OutpostRaidTarget(Outpost outpost)
        {
            this.outpost = outpost;
        }

        public WorldObject WorldObject => outpost;

        public string Name => outpost.Name ?? outpost.def.label;

        public PlanetTile Tile => outpost.Tile;

        public int MilitaryLevel => OutpostMilitaryUtil.MilitaryLevel(outpost);

        public bool IsUnderAttack { get; set; }

        public void OnRaidWon(BattleResult result)
        {
            Find.LetterStack.ReceiveLetter(
                "FCVOE_OutpostDefended".Translate(Name),
                "FCVOE_OutpostDefendedDesc".Translate(Name),
                LetterDefOf.PositiveEvent,
                new LookTargets(outpost));
        }

        public void OnRaidLost(BattleResult result)
        {
            int injuries = Math.Max(1, outpost.PawnCount / 3);
            var pawns = outpost.AllPawns.InRandomOrder().Take(injuries).ToList();
            foreach (Pawn pawn in pawns)
            {
                BodyPartRecord part = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut);
                if (part != null)
                {
                    pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, Rand.Range(8f, 25f), 0f,
                        -1f, null, part));
                }
            }

            int itemCount = outpost.Things.Count();
            if (itemCount > 0)
            {
                int itemsToDestroy = Math.Min(itemCount, Rand.Range(1, 4));
                foreach (Thing thing in outpost.Things.InRandomOrder().Take(itemsToDestroy).ToList())
                {
                    thing.Destroy();
                }
            }

            Find.LetterStack.ReceiveLetter(
                "FCVOE_OutpostRaided".Translate(Name),
                "FCVOE_OutpostRaidedDesc".Translate(Name, injuries),
                LetterDefOf.ThreatBig,
                new LookTargets(outpost));
        }
    }
}
