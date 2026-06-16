using FactionColonies;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// When the "found only via outposts" rule is on, blocks any direct settlement founding with a
    /// clear reason. The Found screen normally swaps its Settle button for "Send a Caravan" (see
    /// <see cref="OutpostFoundingScreen"/>); this validator is the belt-and-suspenders guard for any
    /// other founding path.
    /// </summary>
    public class FoundingRestrictionValidator : ISettlementFoundingValidator
    {
        public bool CanFoundSettlement(PlanetTile tile, WorldSettlementDef type, out string reason, float costMultiplier)
        {
            reason = null;
            // An outpost conversion is the intended way to found a settlement when the restriction is
            // on, so don't block it — only block genuine direct-founding attempts.
            if (OutpostConversionUtil.IsConverting) return true;
            if (EmpireVOESettings.OutpostConversionActive && EmpireVOESettings.requireOutpostForSettlement)
            {
                reason = "VOE_MustFoundViaOutpost".Translate();
                return false;
            }
            return true;
        }

        public string GetAdditionalCostDescription(PlanetTile tile, WorldSettlementDef type, float costMultiplier)
        {
            return null;
        }

        public void OnSettlementFounded(PlanetTile tile, WorldSettlementDef type, float costMultiplier)
        {
        }
    }
}
