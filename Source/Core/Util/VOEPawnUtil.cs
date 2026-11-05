namespace EmpireVOE;

/// <summary>
/// Helpers for pawns that VEF's <c>Outpost.AddPawn</c> rejects (it returns false for
/// ideology-unwilling and vehicle pawns). A rejected pawn is left with no holder. Rather than
/// strand it in the world pawn pool — where the player can't reach it and it just pollutes the
/// save — we ship it to the player's colony through the base mod's delivery system.
/// </summary>
internal static class VOEPawnUtil
{
    /// <summary>
    /// Delivers pawns to the player's colony via the base mod's tax-delivery routing
    /// (caravan/pod/shuttle/tax-spot), announced with a letter. Travels from <paramref name="source"/>.
    /// </summary>
    internal static void DeliverPawnsToPlayer(List<Pawn> pawns, PlanetTile source, string label, string text)
    {
        if (pawns is null || pawns.Count == 0) return;
        Letter let = LetterMaker.MakeLetter(label, text, LetterDefOf.NeutralEvent);
        DeliveryEvent.CreateDeliveryEvent(pawns.ConvertAll<Thing>(p => p), source, let);
    }
}
