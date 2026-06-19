using System;
using FactionColonies;
using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_EmpireDefensive : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_EmpireDefensive()
        {
            compClass = typeof(WorldObjectComp_EmpireDefensive);
        }
    }

    /// <summary>
    /// Empire integration comp for defensive outposts. Manages auto-defend toggle,
    /// post-defense cooldown, and registration of IAutoDefender/IMilitaryTabEntry wrappers.
    /// </summary>
    public class WorldObjectComp_EmpireDefensive : WorldObjectComp
    {
        public bool autoDefend;
        private int cooldownEndTick;

        // Committed-to-an-op state, owned here (not on the [Unsaved] DefensiveAutoDefender) so it
        // survives save/reload during the pre-battle warning window. Set on pledge/engagement, cleared
        // on replacement/completion. See DefensiveAutoDefender's IAutoDefender hooks.
        public bool defending;
        public WorldObject defendingTarget;

        [Unsaved] public DefensiveAutoDefender defender;
        [Unsaved] public DefensiveTabEntry tabEntry;

        private Outpost_Defensive Outpost => (Outpost_Defensive)parent;

        public bool IsOnCooldown
        {
            get
            {
                if (cooldownEndTick <= 0) return false;
                if (Find.TickManager.TicksGame >= cooldownEndTick)
                {
                    cooldownEndTick = 0;
                    return false;
                }
                return true;
            }
        }

        public int CooldownTicksLeft
        {
            get
            {
                if (cooldownEndTick <= 0) return 0;
                return Math.Max(0, cooldownEndTick - Find.TickManager.TicksGame);
            }
        }

        /// <summary>
        /// True when the garrison is standing a passive watch and may project the defensive aura: not set
        /// to auto-defend, not currently deployed on a defense, and not regrouping on cooldown.
        /// </summary>
        public bool ProvidesAura => !autoDefend && !defending && !IsOnCooldown;

        /// <summary>
        /// True when this outpost is actually projecting its aura right now: the feature is on, the garrison is
        /// standing a passive watch, and there are pawns to stand it. (Outpost-side readiness — does not check
        /// whether a settlement is actually in range.)
        /// </summary>
        public bool IsProjectingAura => EmpireVOESettings.DefensiveAuraActive && ProvidesAura && Outpost.PawnCount > 0;

        /// <summary>The militaryBaseLevel bonus this outpost contributes to each nearby settlement.</summary>
        public double AuraMilitaryBonus => DefensiveAuraEntry.OutpostBonus(Outpost);

        // Aura eligibility (ProvidesAura) is dynamic: autoDefend toggles, defenses begin/end, and cooldowns
        // elapse — the last with no event of its own. Poll for a flip and dirty the aura cache so the passive
        // militaryBaseLevel bonus tracks garrison availability. Invalidate clears the settlement stat cache too.
        [Unsaved] private bool lastAuraEligible;

        public void SetCooldown(int durationTicks)
        {
            cooldownEndTick = Find.TickManager.TicksGame + durationTicks;
        }

        public void ClearCooldown()
        {
            cooldownEndTick = 0;
        }

        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
            if (!EmpireVOESettings.MilitaryActive) return;
            Register();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!EmpireVOESettings.DefensiveAuraActive) return;
            if (!parent.IsHashIntervalTick(250)) return;   // ~4s; hashed per outpost so checks spread across ticks
            bool eligible = ProvidesAura;
            if (eligible == lastAuraEligible) return;
            lastAuraEligible = eligible;
            DefensiveAuraCache.Invalidate();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoDefend, "voeAutoDefend");
            Scribe_Values.Look(ref cooldownEndTick, "voeCooldownEndTick");
            Scribe_Values.Look(ref defending, "voeDefending");
            Scribe_References.Look(ref defendingTarget, "voeDefendingTarget");
        }

        public override void PostPostRemove()
        {
            base.PostPostRemove();
            Unregister();
        }

        /// <summary>
        /// Returns a defensive status line for the inspect string.
        /// Called by WorldObjectComp_EmpireOutpost.CompInspectStringExtra.
        /// </summary>
        public string GetStatusInspectString(bool isUnderAttack)
        {
            if (isUnderAttack)
                return "FCVOE_DefenseStatusAttacked".Translate();
            if (IsOnCooldown)
                return "FCVOE_DefenseStatusCooldown".Translate(CooldownTicksLeft.ToTimeString());
            return "FCVOE_DefenseStatusReady".Translate();
        }

        // --- Registration ---

        internal void Register()
        {
            if (defender is object) return;
            defender = new DefensiveAutoDefender(Outpost);
            tabEntry = new DefensiveTabEntry(Outpost, defender);
            EmpireRegistry.Register(defender);
            EmpireRegistry.Register(tabEntry);
        }

        internal void Unregister()
        {
            if (defender is object)
            {
                EmpireRegistry.Unregister(defender);
                defender = null;
            }
            if (tabEntry is object)
            {
                EmpireRegistry.Unregister(tabEntry);
                tabEntry = null;
            }
        }
    }
}
