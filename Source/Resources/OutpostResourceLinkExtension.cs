using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// DefModExtension attached (via XML patch) to each linkable VOE outpost WorldObjectDef. Declares which
    /// Empire <see cref="FactionColonies.ResourceTypeDef"/>(s) the outpost feeds when linked to a settlement,
    /// and which skill(s) drive the per-pawn additive contribution.
    /// <para>References are direct def cross-references (not lenient defName strings): the resources are
    /// Empire's own RTD_* defs and the skills are vanilla SkillDefs, both always present when this mod
    /// loads. Removing mods mid-playthrough is out of scope.</para>
    /// </summary>
    public class OutpostResourceLinkExtension : DefModExtension
    {
        /// <summary>Resources this outpost contributes to while linked (e.g. RTD_Mining).</summary>
        public List<FactionColonies.ResourceTypeDef> resources;

        /// <summary>Primary skill driving the additive bonus (e.g. Mining, Plants).</summary>
        public SkillDef skill;

        /// <summary>Optional secondary skill; per pawn the higher of the two levels is used (e.g. Hunting = Shooting/Animals).</summary>
        public SkillDef secondarySkill;
    }
}
