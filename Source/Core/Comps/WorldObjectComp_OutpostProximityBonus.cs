namespace EmpireVOE;

/// <summary>
/// Base for settlement comps that add a bonus from nearby outposts to one settlement stat. Returns the
/// stat's identity (and no tooltip line) when the feature is off, for any other stat, when the parent is
/// not an Empire settlement, or when no outpost contributes; otherwise identity plus the entry's bonus.
/// </summary>
public abstract class WorldObjectComp_OutpostProximityBonus : WorldObjectComp, IStatModifierProvider
{
    /// <summary>Whether the feature behind this bonus is enabled in the mod settings.</summary>
    protected abstract bool Active { get; }

    /// <summary>The single settlement stat this comp modifies.</summary>
    protected abstract FCStatDef Stat { get; }

    protected abstract IOutpostProximityBonus GetEntry(WorldSettlementFC settlement);

    /// <summary>The tooltip line (without the trailing newline) for a contributing entry.</summary>
    protected abstract string DescLine(FCStatDef stat, IOutpostProximityBonus entry);

    private IOutpostProximityBonus ContributingEntry(FCStatDef stat)
    {
        if (!Active) return null;
        if (stat != Stat) return null;
        if (parent is not WorldSettlementFC settlement) return null;

        IOutpostProximityBonus entry = GetEntry(settlement);
        if (entry is null || entry.SourceCount == 0) return null;
        return entry;
    }

    double IStatModifierProvider.GetStatModifier(FCStatDef stat)
    {
        IOutpostProximityBonus entry = ContributingEntry(stat);
        return entry is null ? stat.IdentityValue : stat.IdentityValue + entry.Bonus;
    }

    string IStatModifierProvider.GetStatModifierDesc(FCStatDef stat)
    {
        IOutpostProximityBonus entry = ContributingEntry(stat);
        return entry is null ? null : $"{DescLine(stat, entry)}\n";
    }
}
