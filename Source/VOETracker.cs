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
    public class VOETracker : WorldComponent
    {
        // --- Military state (serialized) ---
        private Dictionary<int, bool> autoDefendStates = new Dictionary<int, bool>();

        // --- Delivery state (serialized) ---
        // settlement loadID → outpost loadID hash. Absence = deliver to tax map.
        private Dictionary<int, int> deliveryDestinations = new Dictionary<int, int>();
        // settlement loadID → outpost loadID hash. Absence = no financing outpost.
        private Dictionary<int, int> financingOutposts = new Dictionary<int, int>();

        // --- Cooldown state (serialized) ---
        // outpost ID hash → tick when cooldown ends
        private Dictionary<int, int> cooldownEndTicks = new Dictionary<int, int>();

        // --- Redirect flags (not serialized, consumed same tick) ---
        private static readonly HashSet<int> redirectedSettlements = new HashSet<int>();

        // --- Wrapper registries (not serialized, rebuilt on SpawnSetup) ---
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
            Scribe_Collections.Look(ref deliveryDestinations, "voeDeliveryDestinations",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref financingOutposts, "voeFinancingOutposts",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cooldownEndTicks, "voeCooldownEndTicks",
                LookMode.Value, LookMode.Value);
            if (autoDefendStates == null)
                autoDefendStates = new Dictionary<int, bool>();
            if (deliveryDestinations == null)
                deliveryDestinations = new Dictionary<int, int>();
            if (financingOutposts == null)
                financingOutposts = new Dictionary<int, int>();
            if (cooldownEndTicks == null)
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

        // --- Delivery destination accessors ---

        public static Outpost GetDeliveryDestination(WorldSettlementFC settlement)
        {
            if (instance == null || instance.deliveryDestinations == null || settlement == null)
                return null;
            int outpostId;
            if (!instance.deliveryDestinations.TryGetValue(settlement.ID, out outpostId))
                return null;
            foreach (Outpost o in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
            {
                if (o.GetUniqueLoadID().GetHashCode() == outpostId)
                    return o;
            }
            // Outpost gone — clear stale entry
            instance.deliveryDestinations.Remove(settlement.ID);
            return null;
        }

        public static void SetDeliveryDestination(WorldSettlementFC settlement, Outpost outpost)
        {
            if (instance == null || settlement == null) return;
            if (outpost == null)
                instance.deliveryDestinations.Remove(settlement.ID);
            else
                instance.deliveryDestinations[settlement.ID] = outpost.GetUniqueLoadID().GetHashCode();
        }

        // --- Per-settlement financing outpost accessors ---

        public static Outpost GetFinancingOutpost(WorldSettlementFC settlement)
        {
            if (instance == null || instance.financingOutposts == null || settlement == null)
                return null;
            int outpostId;
            if (!instance.financingOutposts.TryGetValue(settlement.ID, out outpostId))
                return null;
            foreach (Outpost o in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
            {
                if (o.GetUniqueLoadID().GetHashCode() == outpostId)
                    return o;
            }
            instance.financingOutposts.Remove(settlement.ID);
            return null;
        }

        public static void SetFinancingOutpost(WorldSettlementFC settlement, Outpost outpost)
        {
            if (instance == null || settlement == null) return;
            if (outpost == null)
                instance.financingOutposts.Remove(settlement.ID);
            else
                instance.financingOutposts[settlement.ID] = outpost.GetUniqueLoadID().GetHashCode();
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
            if (instance == null || instance.cooldownEndTicks == null) return false;
            int key = outpost.GetUniqueLoadID().GetHashCode();
            int endTick;
            if (!instance.cooldownEndTicks.TryGetValue(key, out endTick)) return false;
            if (Find.TickManager.TicksGame >= endTick)
            {
                instance.cooldownEndTicks.Remove(key);
                return false;
            }
            return true;
        }

        public static int GetCooldownTicksLeft(Outpost_Defensive outpost)
        {
            if (instance == null || instance.cooldownEndTicks == null) return 0;
            int key = outpost.GetUniqueLoadID().GetHashCode();
            int endTick;
            if (!instance.cooldownEndTicks.TryGetValue(key, out endTick)) return 0;
            return Math.Max(0, endTick - Find.TickManager.TicksGame);
        }

        public static void SetCooldown(Outpost_Defensive outpost, int durationTicks)
        {
            if (instance == null) return;
            int key = outpost.GetUniqueLoadID().GetHashCode();
            instance.cooldownEndTicks[key] = Find.TickManager.TicksGame + durationTicks;
        }

        public static void ClearCooldown(Outpost_Defensive outpost)
        {
            if (instance == null) return;
            instance.cooldownEndTicks.Remove(outpost.GetUniqueLoadID().GetHashCode());
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
        }

        /// <summary>
        /// Re-registers all outposts currently on the world map. Called when the user
        /// re-enables integration at runtime via settings.
        /// </summary>
        public static void ReregisterAll()
        {
            if (Find.World == null) return;
            SilverPaymentRegistry.Register(OutpostFinancer.Instance);
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
