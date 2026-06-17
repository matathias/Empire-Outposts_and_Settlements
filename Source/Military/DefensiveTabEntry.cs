using FactionColonies;
using FactionColonies.util;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// IMilitaryTabEntry wrapper for Outpost_Defensive. Displays the outpost in
    /// Empire's military tab with military level, auto-defend toggle, and status.
    /// </summary>
    public class DefensiveTabEntry : IMilitaryTabEntry
    {
        private readonly Outpost_Defensive outpost;
        private readonly DefensiveAutoDefender defender;
        private readonly WorldObjectComp_EmpireDefensive comp;

        public DefensiveTabEntry(Outpost_Defensive outpost, DefensiveAutoDefender defender)
        {
            this.outpost = outpost;
            this.defender = defender;
            comp = outpost.GetComponent<WorldObjectComp_EmpireDefensive>();
        }

        public WorldObject WorldObject => outpost;

        public string Name => outpost.Name ?? outpost.def.label;

        public int MilitaryLevel => defender.MilitaryLevel;

        public bool AutoDefend
        {
            get => comp.autoDefend;
            set => comp.autoDefend = value;
        }

        public bool IsUnderAttack => outpost.GetComponent<WorldObjectComp_EmpireOutpost>()?.raidTarget?.IsUnderAttack ?? false;

        public bool IsBusy => defender.Busy || IsUnderAttack || comp.IsOnCooldown;

        public string StatusLabel
        {
            get
            {
                if (IsUnderAttack) return "FCMilStatusUnderAttack".Translate();
                if (comp.IsOnCooldown)
                {
                    // Outpost pawns are returned instantly, so the squad "Returning Home" text is wrong
                    // here — use a dedicated external-defender cooldown string.
                    return "VOE_StatusRegrouping".Translate(comp.CooldownTicksLeft.ToTimeString());
                }
                if (defender.Busy)
                {
                    string target = defender.DefendingTargetName;
                    if (target?.Length > 0)
                        return "VOE_StatusDefending".Translate(target);
                    return "FCMilStatusBusy".Translate();
                }
                if (AutoDefend) return "FCMilStatusReady".Translate();
                return "VOE_DefenseIdle".Translate();
            }
        }

        public Color AccentColor
        {
            get
            {
                if (IsUnderAttack) return AccentUtil.MilUnderAttack;
                if (IsBusy) return AccentUtil.MilActiveMission;
                if (AutoDefend) return AccentUtil.MilReady;
                return AccentUtil.MilInactive;
            }
        }
    }
}
