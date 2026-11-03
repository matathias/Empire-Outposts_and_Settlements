using UnityEngine;
using Verse;

namespace EmpireVOE
{
    /// <summary>
    /// Startup-loaded texture handles for EmpireVOE. Loading must happen in a [StaticConstructorOnStartup]
    /// type so the textures exist by the time any gizmo asks for them.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VOETex
    {
        public static readonly Texture2D Trail = ContentFinder<Texture2D>.Get("UI/Icons/trail");
    }
}
