using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Tracks whether optional mods that EmpireVOE integrates with are active, for dynamic UI rendering
    /// (e.g. showing the power-outpost conversion setting only when VOE Power Outposts is installed).
    /// Mirrors the base mod's <c>FactionColonies.FactionCompat</c>.
    /// </summary>
    public static class EmpireVOECompat
    {
        private static bool isPowerOutpostsActive = false;

        public static bool PowerOutpostsActive => isPowerOutpostsActive;

        public static void CheckForMods()
        {
            isPowerOutpostsActive = ModActive("MrHydralisk.VOEPowerGrid");
            VOELog.Message($"[EmpireVOECompat] VOE Power Outposts Active: {PowerOutpostsActive}");
        }

        private static bool ModActive(string packageId)
        {
            // ignorePostfix so a Steam Workshop copy (PackageId gets a "_steam" suffix) still matches;
            // exact equality would miss it and leave PowerOutpostsActive false while LoadFolders'
            // suffix-trimmed IfModActive still loads the compat.
            return ModLister.GetActiveModWithIdentifier(packageId, true) is object;
        }
    }
}
