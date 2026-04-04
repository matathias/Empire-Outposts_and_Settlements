using System;
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
    /// WorldComponent that persists per-outpost auto-defend state, per-settlement
    /// delivery destinations, per-settlement financing outposts, and provides
    /// lookup from Outpost instances to their interface wrappers.
    /// Auto-discovered by RimWorld (WorldComponent subclasses are instantiated automatically).
    /// </summary>
    public class WorldComponent_VOETracker : WorldComponent
    {
        // --- Military state (serialized) ---
        private Dictionary<int, bool> autoDefendStates = new Dictionary<int, bool>();

        // --- Delivery state (serialized) ---
        // settlement loadID → outpost loadID hash. Absence = deliver to tax map.
        private Dictionary<int, int> deliveryDestinations = new Dictionary<int, int>();
        // settlement loadID → outpost loadID hash. Absence = no financing outpost.
        private Dictionary<int, int> financingOutposts = new Dictionary<int, int>();

        // --- Science link state (serialized) ---
        // settlement ID → science outpost loadID hash. Absence = no linked science outpost.
        private Dictionary<int, int> scienceLinks = new Dictionary<int, int>();

        // --- Cooldown state (serialized) ---
        // outpost ID hash → tick when cooldown ends
        private Dictionary<int, int> cooldownEndTicks = new Dictionary<int, int>();

        // --- Redirect flags (not serialized, consumed same tick) ---
        private static readonly HashSet<int> redirectedSettlements = new HashSet<int>();

        // --- Wrapper registries (not serialized, rebuilt on SpawnSetup) ---
        private static readonly Dictionary<Outpost, OutpostRaidTarget> raidTargets = new Dictionary<Outpost, OutpostRaidTarget>();
        private static readonly Dictionary<Outpost_Defensive, DefensiveAutoDefender> autoDefenders = new Dictionary<Outpost_Defensive, DefensiveAutoDefender>();
        private static readonly Dictionary<Outpost_Defensive, DefensiveTabEntry> tabEntries = new Dictionary<Outpost_Defensive, DefensiveTabEntry>();

        private static WorldComponent_VOETracker instance;

        public WorldComponent_VOETracker(World world) : base(world)
        {
            instance = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref autoDefendStates, "voeAutoDefendStates", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref deliveryDestinations, "voeDeliveryDestinations", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref financingOutposts, "voeFinancingOutposts", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref scienceLinks, "voeScienceLinks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cooldownEndTicks, "voeCooldownEndTicks", LookMode.Value, LookMode.Value);
            if (autoDefendStates is null)
                autoDefendStates = new Dictionary<int, bool>();
            if (deliveryDestinations is null)
                deliveryDestinations = new Dictionary<int, int>();
            if (financingOutposts is null)
                financingOutposts = new Dictionary<int, int>();
            if (scienceLinks is null)
                scienceLinks = new Dictionary<int, int>();
            if (cooldownEndTicks is null)
                cooldownEndTicks = new Dictionary<int, int>();
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
            if (raidTargets.TryGetValue(outpost, out OutpostRaidTarget target))
            {
                RaidTargetRegistry.Unregister(target);
                raidTargets.Remove(outpost);
            }

            if (outpost is Outpost_Science && instance?.scienceLinks != null)
            {
                int outpostHash = outpost.GetUniqueLoadID().GetHashCode();
                List<int> toRemove = new List<int>();
                foreach (KeyValuePair<int, int> kvp in instance.scienceLinks)
                {
                    if (kvp.Value == outpostHash)
                        toRemove.Add(kvp.Key);
                }
                foreach (int key in toRemove)
                    instance.scienceLinks.Remove(key);
            }

            if (outpost is Outpost_Defensive defensive)
            {
                if (autoDefenders.TryGetValue(defensive, out DefensiveAutoDefender defender))
                {
                    AutoDefenderRegistry.Unregister(defender);
                    autoDefenders.Remove(defensive);
                }
                if (tabEntries.TryGetValue(defensive, out DefensiveTabEntry tab))
                {
                    MilitaryTabRegistry.Unregister(tab);
                    tabEntries.Remove(defensive);
                }
            }
        }

        public static OutpostRaidTarget GetRaidTarget(Outpost outpost)
        {
            raidTargets.TryGetValue(outpost, out OutpostRaidTarget target);
            return target;
        }

        public static bool GetAutoDefend(Outpost_Defensive outpost)
        {
            if (instance is null) return false;
            if (instance.autoDefendStates.TryGetValue(outpost.GetUniqueLoadID().GetHashCode(), out bool value))
                return value;
            return false;
        }

        public static void SetAutoDefend(Outpost_Defensive outpost, bool value)
        {
            if (instance is null) return;
            instance.autoDefendStates[outpost.GetUniqueLoadID().GetHashCode()] = value;
        }

        // --- Delivery destination accessors ---

        public static Outpost GetDeliveryDestination(WorldSettlementFC settlement)
        {
            return GetOutpost(settlement, instance?.deliveryDestinations);
        }
        public static Outpost GetFinancingOutpost(WorldSettlementFC settlement)
        {
            return GetOutpost(settlement, instance?.financingOutposts);
        }
        private static Outpost GetOutpost(WorldSettlementFC settlement, Dictionary<int, int> dict)
        {
            if (dict is null || settlement is null)
                return null;
            if (!dict.TryGetValue(settlement.ID, out int outpostId))
                return null;
            foreach (Outpost o in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
            {
                if (o.GetUniqueLoadID().GetHashCode() == outpostId)
                    return o;
            }
            dict.Remove(settlement.ID);
            return null;
        }


        public static void SetDeliveryDestination(WorldSettlementFC settlement, Outpost outpost)
        {
            SetOutpost(settlement, outpost, instance?.deliveryDestinations);
        }
        public static void SetFinancingOutpost(WorldSettlementFC settlement, Outpost outpost)
        {
            SetOutpost(settlement, outpost, instance?.financingOutposts);
        }
        private static void SetOutpost(WorldSettlementFC settlement, Outpost outpost, Dictionary<int, int> dict)
        {
            if (dict is null || settlement is null) return;
            if (outpost is null)
                dict.Remove(settlement.ID);
            else
                dict[settlement.ID] = outpost.GetUniqueLoadID().GetHashCode();
        }

        /// <summary>
        /// Returns all distinct financing outposts currently assigned to any settlement.
        /// Used by GetSilver postfix to include financing outpost silver in total.
        /// </summary>
        public static IEnumerable<Outpost> GetAllDistinctFinancingOutposts()
        {
            if (instance == null || instance.financingOutposts == null)
                yield break;
            HashSet<int> seen = new HashSet<int>();
            foreach (int outpostId in instance.financingOutposts.Values)
            {
                if (!seen.Add(outpostId)) continue;
                foreach (Outpost o in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
                {
                    if (o.GetUniqueLoadID().GetHashCode() == outpostId)
                    {
                        yield return o;
                        break;
                    }
                }
            }
        }

        // --- Redirect flag accessors ---

        public static bool IsRedirected(int settlementTile)
        {
            return redirectedSettlements.Contains(settlementTile);
        }

        public static void SetRedirected(int settlementTile)
        {
            redirectedSettlements.Add(settlementTile);
        }

        public static void ClearRedirected(int settlementTile)
        {
            redirectedSettlements.Remove(settlementTile);
        }

        // --- Cooldown accessors ---

        public static bool IsOnCooldown(Outpost_Defensive outpost)
        {
            if (instance?.cooldownEndTicks is null) return false;
            int key = outpost.GetUniqueLoadID().GetHashCode();
            if (!instance.cooldownEndTicks.TryGetValue(key, out int endTick)) return false;
            if (Find.TickManager.TicksGame >= endTick)
            {
                instance.cooldownEndTicks.Remove(key);
                return false;
            }
            return true;
        }

        public static int GetCooldownTicksLeft(Outpost_Defensive outpost)
        {
            if (instance?.cooldownEndTicks is null) return 0;
            int key = outpost.GetUniqueLoadID().GetHashCode();
            if (!instance.cooldownEndTicks.TryGetValue(key, out int endTick)) return 0;
            return Math.Max(0, endTick - Find.TickManager.TicksGame);
        }

        public static void SetCooldown(Outpost_Defensive outpost, int durationTicks)
        {
            if (instance is null) return;
            int key = outpost.GetUniqueLoadID().GetHashCode();
            instance.cooldownEndTicks[key] = Find.TickManager.TicksGame + durationTicks;
        }

        public static void ClearCooldown(Outpost_Defensive outpost)
        {
            instance?.cooldownEndTicks?.Remove(outpost.GetUniqueLoadID().GetHashCode());
        }

        // --- Science link accessors ---

        public static Outpost_Science GetLinkedScienceOutpost(WorldSettlementFC settlement)
        {
            if (instance?.scienceLinks is null || settlement is null)
                return null;
            if (!instance.scienceLinks.TryGetValue(settlement.ID, out int outpostHash))
                return null;
            foreach (Outpost_Science o in Find.WorldObjects.AllWorldObjects.OfType<Outpost_Science>())
            {
                if (o.GetUniqueLoadID().GetHashCode() == outpostHash)
                    return o;
            }
            instance.scienceLinks.Remove(settlement.ID);
            return null;
        }

        public static WorldSettlementFC GetLinkedSettlement(Outpost_Science outpost)
        {
            if (instance?.scienceLinks is null || outpost is null)
                return null;
            int outpostHash = outpost.GetUniqueLoadID().GetHashCode();
            FactionFC faction = FactionCache.FactionComp;
            if (faction is null) return null;
            foreach (KeyValuePair<int, int> kvp in instance.scienceLinks)
            {
                if (kvp.Value != outpostHash) continue;
                foreach (WorldSettlementFC s in faction.settlements)
                {
                    if (s.ID == kvp.Key)
                        return s;
                }
                instance.scienceLinks.Remove(kvp.Key);
                return null;
            }
            return null;
        }

        public static void SetScienceLink(WorldSettlementFC settlement, Outpost_Science outpost)
        {
            if (instance?.scienceLinks is null || settlement is null) return;
            if (outpost is null)
            {
                instance.scienceLinks.Remove(settlement.ID);
            }
            else
            {
                // Enforce 1:1: clear any existing link for this outpost
                int outpostHash = outpost.GetUniqueLoadID().GetHashCode();
                int existingKey = -1;
                foreach (KeyValuePair<int, int> kvp in instance.scienceLinks)
                {
                    if (kvp.Value == outpostHash)
                    {
                        existingKey = kvp.Key;
                        break;
                    }
                }
                if (existingKey != -1)
                    instance.scienceLinks.Remove(existingKey);
                instance.scienceLinks[settlement.ID] = outpostHash;
            }
        }

        public static bool IsLinkedToSettlement(Outpost_Science outpost)
        {
            if (instance?.scienceLinks is null || outpost is null)
                return false;
            int outpostHash = outpost.GetUniqueLoadID().GetHashCode();
            foreach (int val in instance.scienceLinks.Values)
            {
                if (val == outpostHash) return true;
            }
            return false;
        }

        // --- Reverse-lookup helpers (for inspect string) ---

        public static List<string> GetFinancedSettlementNames(Outpost outpost)
        {
            List<string> names = new List<string>();
            if (instance?.financingOutposts is null || outpost is null) return names;
            int outpostHash = outpost.GetUniqueLoadID().GetHashCode();
            FactionFC faction = FactionCache.FactionComp;
            if (faction is null) return names;
            foreach (KeyValuePair<int, int> kvp in instance.financingOutposts)
            {
                if (kvp.Value != outpostHash) continue;
                foreach (WorldSettlementFC s in faction.settlements)
                {
                    if (s.ID == kvp.Key)
                    {
                        names.Add(s.Name);
                        break;
                    }
                }
            }
            return names;
        }

        public static List<string> GetDeliverySourceNames(Outpost outpost)
        {
            List<string> names = new List<string>();
            if (instance?.deliveryDestinations is null || outpost is null) return names;
            int outpostHash = outpost.GetUniqueLoadID().GetHashCode();
            FactionFC faction = FactionCache.FactionComp;
            if (faction is null) return names;
            foreach (KeyValuePair<int, int> kvp in instance.deliveryDestinations)
            {
                if (kvp.Value != outpostHash) continue;
                foreach (WorldSettlementFC s in faction.settlements)
                {
                    if (s.ID == kvp.Key)
                    {
                        names.Add(s.Name);
                        break;
                    }
                }
            }
            return names;
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

            SilverPaymentRegistry.Unregister(OutpostFinancer.Instance);
            ThreatScalingRegistry.Unregister(VOECompatInit.ThreatContributor);
        }

        /// <summary>
        /// Re-registers all outposts currently on the world map. Called when the user
        /// re-enables integration at runtime via settings.
        /// </summary>
        public static void ReregisterAll()
        {
            if (Find.World is null) return;
            SilverPaymentRegistry.Register(OutpostFinancer.Instance);
            ThreatScalingRegistry.Register(VOECompatInit.ThreatContributor);
            List<Outpost> outposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>().ToList();
            foreach (Outpost outpost in outposts)
            {
                if (raidTargets.ContainsKey(outpost)) continue;

                OutpostRaidTarget target = new OutpostRaidTarget(outpost);
                RegisterOutpost(outpost, target);

                if (outpost is Outpost_Defensive defensive)
                {
                    DefensiveAutoDefender defender = new DefensiveAutoDefender(defensive);
                    DefensiveTabEntry tab = new DefensiveTabEntry(defensive, defender);
                    RegisterDefensive(defensive, defender, tab);
                }
            }
        }
    }
}
