using System.Collections.Generic;

namespace EmpireVOE
{
    /// <summary>
    /// Shared helpers for the EmpireVOE tests: a snapshot/restore for the mutable
    /// <see cref="EmpireVOESettings"/> statics (so non-destructive tests can pin a known value and
    /// still leave settings as they found them), plus small fixture builders.
    /// </summary>
    public static class VOETestHelper
    {
        /// <summary>Captured values of the settings statics that the tests may overwrite.</summary>
        public struct SettingsSnapshot
        {
            public float additivePerLevel;
            public int skillFloor;
            public float encampmentHealRatePerLevel;
            public float reducedFoundingCostFactor;
            public bool enableConversionDelay;
            public int conversionDelayDays;
            public float townFlatAdditive;
        }

        public static SettingsSnapshot SnapshotSettings()
        {
            SettingsSnapshot s;
            s.additivePerLevel = EmpireVOESettings.additivePerLevel;
            s.skillFloor = EmpireVOESettings.skillFloor;
            s.encampmentHealRatePerLevel = EmpireVOESettings.encampmentHealRatePerLevel;
            s.reducedFoundingCostFactor = EmpireVOESettings.reducedFoundingCostFactor;
            s.enableConversionDelay = EmpireVOESettings.enableConversionDelay;
            s.conversionDelayDays = EmpireVOESettings.conversionDelayDays;
            s.townFlatAdditive = EmpireVOESettings.townFlatAdditive;
            return s;
        }

        public static void RestoreSettings(SettingsSnapshot s)
        {
            EmpireVOESettings.additivePerLevel = s.additivePerLevel;
            EmpireVOESettings.skillFloor = s.skillFloor;
            EmpireVOESettings.encampmentHealRatePerLevel = s.encampmentHealRatePerLevel;
            EmpireVOESettings.reducedFoundingCostFactor = s.reducedFoundingCostFactor;
            EmpireVOESettings.enableConversionDelay = s.enableConversionDelay;
            EmpireVOESettings.conversionDelayDays = s.conversionDelayDays;
            EmpireVOESettings.townFlatAdditive = s.townFlatAdditive;
        }

        /// <summary>
        /// A throwaway <see cref="OutpostConversionExtension"/> for the conversion-mapping tests.
        /// Reference identity / defName resolution is all the tests rely on, so it never needs to be
        /// attached to a real def.
        /// </summary>
        public static OutpostConversionExtension MakeConversionExtension(bool allowAny, params string[] allowedDefNames)
        {
            return new OutpostConversionExtension
            {
                allowAnySettlementType = allowAny,
                allowedSettlementTypes = allowedDefNames != null ? new List<string>(allowedDefNames) : null
            };
        }
    }
}
