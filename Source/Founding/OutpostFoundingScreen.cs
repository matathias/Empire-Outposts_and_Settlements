using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Hooks the base "Found New Settlement" screen so that, when founding is restricted to outposts,
    /// the Settle button becomes "Send a Caravan" and a companion window lists the outpost types the
    /// player must establish to found the selected settlement type.
    /// </summary>
    public static class OutpostFoundingScreen
    {
        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            FoundingScreenHooks.SettleButtonOverride = GetSettleButtonOverride;
            FoundingScreenHooks.SelectionChanged += OnSelectionChanged;
        }

        private static bool RestrictionActive =>
            EmpireVOESettings.OutpostConversionActive && EmpireVOESettings.requireOutpostForSettlement;

        private static FoundingButtonOverride GetSettleButtonOverride(PlanetTile tile, WorldSettlementDef type)
        {
            if (!RestrictionActive) return null;
            return new FoundingButtonOverride
            {
                Label = "VOE_SendCaravan".Translate(),
                OnClick = SendCaravan
            };
        }

        private static void OnSelectionChanged(PlanetTile tile, WorldSettlementDef type)
        {
            // Requirements depend on the selected settlement TYPE, not the tile, so a valid tile
            // isn't required (the player may just be reading requirements before sending a caravan).
            if (RestrictionActive && type != null)
                FCWindow_OutpostRequirements.RefreshFor(type);
            else
                FCWindow_OutpostRequirements.TryClose();
        }

        /// <summary>
        /// Opens RimWorld's Form Caravan dialog so the player can travel to the chosen tile and
        /// establish an outpost there. Requires a player home map; prompts to pick one if several exist.
        /// </summary>
        private static void SendCaravan()
        {
            List<Map> homeMaps = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            if (homeMaps.Count == 0)
            {
                Messages.Message("VOE_SendCaravanNoColony".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Find.WindowStack.WindowOfType<CreateColonyWindowFc>()?.Close();

            if (homeMaps.Count == 1)
            {
                OpenFormCaravan(homeMaps[0]);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Map map in homeMaps)
            {
                Map m = map;
                options.Add(new FloatMenuOption(m.Parent.LabelCap, () => OpenFormCaravan(m)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void OpenFormCaravan(Map map)
        {
            Find.WindowStack.Add(new Dialog_FormCaravan(map));
        }
    }
}
