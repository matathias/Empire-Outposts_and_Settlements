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

        // Busy/target state lives on the comp so it persists across save/reload (this object is [Unsaved]).
        public bool Busy => comp.defending;
        public string DefendingTargetName => comp.defendingTarget is object ? comp.defendingTarget.LabelCap : "";

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
                                     && !comp.defending
                                     && !comp.IsOnCooldown;

        public MilitaryForce CreateDefendingForce()
        {
            double level = MilitaryLevel;
            double efficiency = CalculateEfficiency();
            return new MilitaryForce(level, efficiency, null, Faction.OfPlayer);
        }

        public void OnDefensePledged(WorldObject target)
        {
            comp.defending = true;
            comp.defendingTarget = target;
        }

        public void OnDefenseStarted(WorldObject target)
        {
            comp.defending = true;
            comp.defendingTarget = target;
        }

        public void OnDefenseReplaced()
        {
            comp.defending = false;
            comp.defendingTarget = null;
        }

        public void OnDefenseComplete(bool won, BattleResult result)
        {
            comp.defending = false;
            comp.defendingTarget = null;

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
        /// Military Level = priced off the live garrison via the base mod's cost->level curve, so an
        /// outpost's level is comparable to settlement squads (and ranks fairly in auto-defender
        /// selection). SquadPowerRegistry.LevelFromPawns sums each capable pawn's MarketValue (body +
        /// gear + implants), effectiveness-weighted, then inverts CalculateSquadBudget. Empty outpost
        /// reads 0; any non-empty garrison floors at 1. Combat *efficiency* stays skill-based
        /// (CalculateEfficiency) — level is "how much force", efficiency is "how well they fight".
        /// </summary>
        private int CalculateMilitaryLevel()
        {
            if (outpost.PawnCount == 0) return 0;
            return Math.Max(1, (int)Math.Round(SquadPowerRegistry.LevelFromPawns(outpost.CapablePawns)));
        }

        /// <summary>
        /// Efficiency = skill-dominant (better fighters hit harder per round).
        /// Pawn count has no effect - purely about quality.
        /// Linear in skill: slope 0.05 from skill 0 to 20, then half that (slope 0.025) above 20.
        /// Anchored at: skill 0 -> 0.5, skill 10 -> 1.0, skill 20 -> 1.5.
        /// Above 20: keeps rising at half the rate (for mods with skills beyond the vanilla cap).
        /// </summary>
        private double CalculateEfficiency()
        {
            double avgSkill = outpost.CapablePawns
                .Select(p => (double)Math.Max(
                    p.skills.GetSkill(SkillDefOf.Shooting).Level,
                    p.skills.GetSkill(SkillDefOf.Melee).Level))
                .DefaultIfEmpty(5)
                .Average();
            return VOEFormulas.Efficiency(avgSkill);
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
