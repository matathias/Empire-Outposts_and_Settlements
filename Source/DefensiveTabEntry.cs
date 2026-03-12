using FactionColonies;
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

        public WorldObject WorldObject
        {
            get { return outpost; }
        }

        public string Name
        {
            get { return outpost.Name ?? outpost.def.label; }
        }

        public int MilitaryLevel
        {
            get { return defender.MilitaryLevel; }
        }

        public bool AutoDefend
        {
            get { return VOETracker.GetAutoDefend(outpost); }
            set { VOETracker.SetAutoDefend(outpost, value); }
        }

        public bool IsUnderAttack
        {
            get
            {
                OutpostRaidTarget target = VOETracker.GetRaidTarget(outpost);
                return target != null && target.IsUnderAttack;
            }
        }

        public bool IsBusy
        {
            get { return !defender.CanAutoDefend && !IsUnderAttack; }
        }

        public string StatusLabel
        {
            get
            {
                if (IsUnderAttack) return "UnderAttack".Translate();
                if (IsBusy) return "Busy".Translate();
                return "Ready".Translate();
            }
        }

        public Color AccentColor
        {
            get
            {
                if (IsUnderAttack) return AccentUtil.MilUnderAttack;
                if (IsBusy) return AccentUtil.MilActiveMission;
                return AccentUtil.MilReady;
            }
        }
    }
}
