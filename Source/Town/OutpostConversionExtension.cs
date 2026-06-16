using System.Collections.Generic;
using FactionColonies;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// DefModExtension attached (via XML patch) to VOE outpost WorldObjectDefs. Declares which
    /// Empire settlement types this outpost can be converted into.
    /// <para><see cref="allowedSettlementTypes"/> holds plain defName strings (not cross-refs) so the
    /// mapping can name types from any submod without erroring when that submod is absent — unloaded
    /// names are simply dropped at resolve time.</para>
    /// </summary>
    public class OutpostConversionExtension : DefModExtension
    {
        /// <summary>Town outposts: convert into any unlocked, layer-compatible settlement type.</summary>
        public bool allowAnySettlementType = false;

        /// <summary>Production outposts: explicit defNames of settlement types this outpost can become.</summary>
        public List<string> allowedSettlementTypes;

        /// <summary>Town outposts: also grant the flat per-resource additive (loss-of-uniqueness bonus).</summary>
        public bool grantsTownAdditive = false;

        [Unsaved] private List<WorldSettlementDef> resolvedCache;

        /// <summary>
        /// Resolves <see cref="allowedSettlementTypes"/> defName strings to loaded WorldSettlementDefs,
        /// dropping any that don't exist (submod not installed). Cached after first call.
        /// </summary>
        public List<WorldSettlementDef> GetExplicitTypes()
        {
            if (resolvedCache != null) return resolvedCache;
            resolvedCache = new List<WorldSettlementDef>();
            if (allowedSettlementTypes != null)
            {
                foreach (string name in allowedSettlementTypes)
                {
                    if (name.NullOrEmpty()) continue;
                    WorldSettlementDef d = DefDatabase<WorldSettlementDef>.GetNamedSilentFail(name);
                    if (d != null) resolvedCache.Add(d);
                }
            }
            return resolvedCache;
        }
    }
}
