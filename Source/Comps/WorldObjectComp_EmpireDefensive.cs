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
                return "VOE_DefenseStatusAttacked".Translate();
            if (IsOnCooldown)
                return "VOE_DefenseStatusCooldown".Translate(CooldownTicksLeft.ToTimeString());
            return "VOE_DefenseStatusReady".Translate();
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
