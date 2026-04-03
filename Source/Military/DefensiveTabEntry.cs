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

        public DefensiveTabEntry(Outpost_Defensive outpost, DefensiveAutoDefender defender)
        {
            this.outpost = outpost;
            this.defender = defender;
        }

        public WorldObject WorldObject => outpost;

        public string Name => outpost.Name ?? outpost.def.label;

        public int MilitaryLevel => defender.MilitaryLevel;

        public bool AutoDefend
        {
            get => WorldComponent_VOETracker.GetAutoDefend(outpost);
            set => WorldComponent_VOETracker.SetAutoDefend(outpost, value);
        }

        public bool IsUnderAttack => WorldComponent_VOETracker.GetRaidTarget(outpost)?.IsUnderAttack ?? false;

        public bool IsBusy => defender.Busy || IsUnderAttack || WorldComponent_VOETracker.IsOnCooldown(outpost);

        public string StatusLabel
        {
            get
            {
                if (IsUnderAttack) return "FCMilStatusUnderAttack".Translate();
                if (WorldComponent_VOETracker.IsOnCooldown(outpost))
                {
                    string label = "FCMilStatusCooldown".Translate();
                    int ticksLeft = WorldComponent_VOETracker.GetCooldownTicksLeft(outpost);
                    if (ticksLeft > 0) label += " " + ticksLeft.ToTimeString();
                    return label;
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
