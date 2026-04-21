using System;
using System.Collections.Generic;
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
        private readonly WorldObjectComp_EmpireDefensive comp;
        private bool _busy;
        private WorldObject _defendingTarget;

        public bool Busy => _busy;
        public string DefendingTargetName => _defendingTarget is object ? _defendingTarget.LabelCap : "";

        public DefensiveAutoDefender(Outpost_Defensive outpost)
        {
            this.outpost = outpost;
            comp = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
        }

        public WorldObject WorldObject => outpost;

        public int MilitaryLevel => CalculateMilitaryLevel();

        public int Range => outpost.Range;

        public bool CanAutoDefend => comp.autoDefend
                                     && outpost.PawnCount > 1
                                     && !outpost.Packing
                                     && !_busy
                                     && !comp.IsOnCooldown;

        public MilitaryForce CreateDefendingForce()
        {
            double level = MilitaryLevel;
            double efficiency = CalculateEfficiency();
            return new MilitaryForce(level, efficiency, null, Faction.OfPlayer);
        }

        public void OnDefenseStarted(WorldObject target)
        {
            _busy = true;
            _defendingTarget = target;
        }

        public void OnDefenseReplaced()
        {
            _busy = false;
            _defendingTarget = null;
        }

        public void OnDefenseComplete(bool won, BattleResult result)
        {
            _busy = false;
            _defendingTarget = null;

            // 2-day base cooldown (shorter than Empire's 3-day since outposts are simpler)
            int cooldown = GenDate.TicksPerDay * 2;
            if (!won)
            {
                int injuries = Math.Max(1, outpost.PawnCount / 4);
                foreach (Pawn pawn in outpost.AllPawns.InRandomOrder().Take(injuries))
                {
                    BodyPartRecord part = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut);
                    if (part is object)
                    {
                        pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, Rand.Range(5f, 15f), 0f,
                            -1f, null, part));
                    }
                }
                // Extra day cooldown on loss
                cooldown += GenDate.TicksPerDay;
            }
            comp.SetCooldown(cooldown);
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

        public List<Pawn> GetDefendingPawns()
        {
            if (outpost.PawnCount <= 1) return null;

            List<Pawn> pawns = outpost.CapablePawns.ToList();
            if (pawns.Count > 1)
                pawns.RemoveAt(pawns.Count - 1); // keep at least 1 behind

            foreach (Pawn pawn in pawns)
                outpost.RemovePawn(pawn);

            return pawns;
        }

        public void ReturnDefendingPawns(List<Pawn> pawns)
        {
            if (pawns is null) return;
            foreach (Pawn pawn in pawns)
            {
                if (pawn is object && !pawn.Dead && !pawn.Destroyed)
                    outpost.AddPawn(pawn);
            }
        }
    }
}
