using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Outposts;
using RimWorld.Planet;
using Verse;
using VOE;

namespace EmpireVOE
{
    /// <summary>
    /// WorldComponent that persists per-outpost auto-defend state and provides
    /// lookup from Outpost instances to their interface wrappers.
    /// Auto-discovered by RimWorld (WorldComponent subclasses are instantiated automatically).
    /// </summary>
    public class VOETracker : WorldComponent
    {
        private Dictionary<int, bool> autoDefendStates = new Dictionary<int, bool>();

        private static readonly Dictionary<Outpost, OutpostRaidTarget> raidTargets
            = new Dictionary<Outpost, OutpostRaidTarget>();
        private static readonly Dictionary<Outpost_Defensive, DefensiveAutoDefender> autoDefenders
            = new Dictionary<Outpost_Defensive, DefensiveAutoDefender>();
        private static readonly Dictionary<Outpost_Defensive, DefensiveTabEntry> tabEntries
            = new Dictionary<Outpost_Defensive, DefensiveTabEntry>();

        private static VOETracker instance;

        public VOETracker(World world) : base(world)
        {
            instance = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref autoDefendStates, "voeAutoDefendStates",
                LookMode.Value, LookMode.Value);
            if (autoDefendStates == null)
                autoDefendStates = new Dictionary<int, bool>();
        }

        public static void RegisterOutpost(Outpost outpost, OutpostRaidTarget target)
        {
            raidTargets[outpost] = target;
            RaidTargetRegistry.Register(target);
        }

        public static void RegisterDefensive(Outpost_Defensive outpost,
            DefensiveAutoDefender defender, DefensiveTabEntry tab)
        {
            autoDefenders[outpost] = defender;
            tabEntries[outpost] = tab;
            AutoDefenderRegistry.Register(defender);
            MilitaryTabRegistry.Register(tab);
        }

        public static void UnregisterOutpost(Outpost outpost)
        {
            OutpostRaidTarget target;
            if (raidTargets.TryGetValue(outpost, out target))
            {
                RaidTargetRegistry.Unregister(target);
                raidTargets.Remove(outpost);
            }

            Outpost_Defensive defensive = outpost as Outpost_Defensive;
            if (defensive != null)
            {
                DefensiveAutoDefender defender;
                if (autoDefenders.TryGetValue(defensive, out defender))
                {
                    AutoDefenderRegistry.Unregister(defender);
                    autoDefenders.Remove(defensive);
                }
                DefensiveTabEntry tab;
                if (tabEntries.TryGetValue(defensive, out tab))
                {
                    MilitaryTabRegistry.Unregister(tab);
                    tabEntries.Remove(defensive);
                }
            }
        }

        public static OutpostRaidTarget GetRaidTarget(Outpost outpost)
        {
            OutpostRaidTarget target;
            raidTargets.TryGetValue(outpost, out target);
            return target;
        }

        public static bool GetAutoDefend(Outpost_Defensive outpost)
        {
            if (instance == null) return false;
            bool value;
            if (instance.autoDefendStates.TryGetValue(outpost.GetUniqueLoadID().GetHashCode(), out value))
                return value;
            return false;
        }

        public static void SetAutoDefend(Outpost_Defensive outpost, bool value)
        {
            if (instance == null) return;
            instance.autoDefendStates[outpost.GetUniqueLoadID().GetHashCode()] = value;
        }

        /// <summary>
        /// Unregisters all outpost wrappers from all registries. Called when the user
        /// disables integration at runtime via settings.
        /// </summary>
        public static void UnregisterAll()
        {
            foreach (OutpostRaidTarget target in raidTargets.Values)
                RaidTargetRegistry.Unregister(target);

            foreach (DefensiveAutoDefender defender in autoDefenders.Values)
                AutoDefenderRegistry.Unregister(defender);

            foreach (DefensiveTabEntry tab in tabEntries.Values)
                MilitaryTabRegistry.Unregister(tab);

            raidTargets.Clear();
            autoDefenders.Clear();
            tabEntries.Clear();
        }

        /// <summary>
        /// Re-registers all outposts currently on the world map. Called when the user
        /// re-enables integration at runtime via settings.
        /// </summary>
        public static void ReregisterAll()
        {
            if (Find.World == null) return;
            List<Outpost> outposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>().ToList();
            foreach (Outpost outpost in outposts)
            {
                if (raidTargets.ContainsKey(outpost)) continue;

                OutpostRaidTarget target = new OutpostRaidTarget(outpost);
                RegisterOutpost(outpost, target);

                Outpost_Defensive defensive = outpost as Outpost_Defensive;
                if (defensive != null)
                {
                    DefensiveAutoDefender defender = new DefensiveAutoDefender(defensive);
                    DefensiveTabEntry tab = new DefensiveTabEntry(defensive, defender);
                    RegisterDefensive(defensive, defender, tab);
                }
            }
        }
    }
}
