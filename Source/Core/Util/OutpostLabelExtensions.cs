using Outposts;

namespace EmpireVOE;

internal static class OutpostLabelExtensions
{
    /// <summary>
    /// A capitalised label that is never null. VEF's <c>Outpost.Label</c> returns its <c>Name</c>,
    /// which is null for an outpost created outside the camp dialog (debug spawns, tests, other
    /// mods), so <c>LabelCap</c> would be null; this falls back to the outpost def's label instead.
    /// Other world objects use their normal <c>LabelCap</c>.
    /// </summary>
    public static string DisplayLabelCap(this WorldObject worldObject) =>
        worldObject is Outpost outpost ? outpost.RenamableLabel.CapitalizeFirst() : worldObject.LabelCap;
}
