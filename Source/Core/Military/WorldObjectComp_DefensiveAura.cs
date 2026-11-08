namespace EmpireVOE;

public class WorldObjectCompProperties_DefensiveAura : WorldObjectCompProperties
{
    public WorldObjectCompProperties_DefensiveAura()
    {
        compClass = typeof(WorldObjectComp_DefensiveAura);
    }
}

/// <summary>
/// Passive "shield" tier of the defensive-outpost integration: nearby garrisoned <c>Outpost_Defensive</c>
/// add to this settlement's <see cref="FCStatDefOf.militaryBaseLevel"/>, stiffening its auto-resolved
/// defense when raided — even when the outpost never deploys. Complements the active
/// <see cref="DefensiveAutoDefender"/>. Reads from <see cref="DefensiveAuraCache"/>; shares its gating with
/// <see cref="WorldObjectComp_EncampmentBonus"/>.
/// </summary>
public class WorldObjectComp_DefensiveAura : WorldObjectComp_OutpostProximityBonus
{
    protected override bool Active => EmpireVOESettings.DefensiveAuraActive;
    protected override FCStatDef Stat => FCStatDefOf.militaryBaseLevel;

    protected override IOutpostProximityBonus GetEntry(WorldSettlementFC settlement)
        => DefensiveAuraCache.GetOrBuild(settlement);

    protected override string DescLine(FCStatDef stat, IOutpostProximityBonus entry)
        => TextUtil.AdditiveBonusLine(entry.Bonus, "FCVOE_DefensiveAuraSource".Translate(entry.SourceCount));
}
