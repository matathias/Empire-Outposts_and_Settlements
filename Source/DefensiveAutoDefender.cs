using System;
using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// IAutoDefender wrapper for Outpost_Defensive. Allows defensive outposts to
    /// auto-defend nearby Empire settlements (and other IRaidTargets).
    /// Not used for Outpost_Artillery — that has its own purpose.
    /// </summary>
    public class DefensiveAutoDefender : IAutoDefender
    {
        private readonly Outpost_Defensive outpost;
        private bool busy;

        public DefensiveAutoDefender(Outpost_Defensive outpost)
        {
            this.outpost = outpost;
        }

        public WorldObject WorldObject
        {
            get { return outpost; }
        }

        public int MilitaryLevel
        {
            get { return CalculateMilitaryLevel(); }
        }

        public int Range
        {
            get { return outpost.Range; }
        }

        public bool CanAutoDefend
        {
            get
            {
                return VOETracker.GetAutoDefend(outpost)
                    && outpost.PawnCount > 1
                    && !outpost.Packing
                    && !busy;
            }
        }

        public militaryForce CreateDefendingForce()
        {
            double level = MilitaryLevel;
            double efficiency = CalculateEfficiency();
            return new militaryForce(level, efficiency, null, Faction.OfPlayer);
        }

        public void OnDefenseStarted()
        {
            busy = true;
        }

        public void OnDefenseReplaced()
        {
            busy = false;
        }

        public void OnDefenseComplete(bool won, BattleResult result)
        {
            busy = false;
            if (!won)
            {
                int injuries = Math.Max(1, outpost.PawnCount / 4);
                foreach (Pawn pawn in outpost.AllPawns.InRandomOrder().Take(injuries))
                {
                    BodyPartRecord part = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut);
                    if (part != null)
                    {
                        pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, Rand.Range(5f, 15f), 0f,
                            -1f, null, part));
                    }
                }
            }
        }

        /// <summary>
        /// Military Level = pawn-count-dominant (more bodies = bigger force pool).
        /// Formula: pawnCount * (0.5 + avgSkill / 40)
        /// Skill provides secondary scaling but pawn count always dominates.
        /// 6 mid-skill pawns always beat 3 high-skill pawns.
        /// </summary>
        private int CalculateMilitaryLevel()
        {
            int pawnCount = outpost.PawnCount;
            if (pawnCount == 0) return 0;
            double avgCombat = outpost.CapablePawns
                .Select(p => (double)Math.Max(
                    p.skills.GetSkill(SkillDefOf.Shooting).Level,
                    p.skills.GetSkill(SkillDefOf.Melee).Level))
                .DefaultIfEmpty(0)
                .Average();
            return Math.Max(1, (int)(pawnCount * (0.5 + avgCombat / 40.0)));
        }

        /// <summary>
        /// Efficiency = skill-dominant (better fighters hit harder per round).
        /// Pawn count has no effect — purely about quality.
        /// Piecewise: quadratic 0-20, sqrt tail above 20 for diminishing returns.
        /// Anchored at: skill 0 → 0.3, skill 10 → 1.0, skill 20 → 1.6.
        /// Above 20: continues scaling but tapers strongly (for mods with skills beyond 20).
        /// </summary>
        private double CalculateEfficiency()
        {
            double avgSkill = outpost.CapablePawns
                .Select(p => (double)Math.Max(
                    p.skills.GetSkill(SkillDefOf.Shooting).Level,
                    p.skills.GetSkill(SkillDefOf.Melee).Level))
                .DefaultIfEmpty(5)
                .Average();
            if (avgSkill <= 20.0)
                return 0.3 + 0.075 * avgSkill - 0.0005 * avgSkill * avgSkill;
            return 1.6 + 0.05 * Math.Sqrt(avgSkill - 20.0);
        }
    }
}
