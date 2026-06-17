using System.Collections.Generic;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EmpireVOE
{
    public class WorldObjectCompProperties_TownRecruiting : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_TownRecruiting()
        {
            compClass = typeof(WorldObjectComp_TownRecruiting);
        }
    }

    /// <summary>
    /// Per-town "xenotype-pure recruiting" state, attached only to VOE Town outposts.
    /// When the toggle is on, Patch_OutpostTown_Produce forces each recruit to match the
    /// recruiting resident's xenotype (or race, for non-humans) at a reduced recruit chance.
    /// This is a Town-behavior tweak in the "Towns" family, independent of the integration
    /// master toggle.
    /// </summary>
    public class WorldObjectComp_TownRecruiting : WorldObjectComp
    {
        public bool xenotypePure;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref xenotypePure, "voeXenotypePureRecruiting", false);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (parent.Faction != Faction.OfPlayer) yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "VOE_TownXenoPureLabel".Translate(),
                defaultDesc = "VOE_TownXenoPureDesc".Translate(),
                icon = TexLoad.iconCustomize,
                isActive = () => xenotypePure,
                toggleAction = () => xenotypePure = !xenotypePure
            };
        }
    }
}
