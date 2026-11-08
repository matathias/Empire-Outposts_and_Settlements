namespace EmpireVOE;

public class WorldObjectCompProperties_EncampmentBonus : WorldObjectCompProperties
{
    public WorldObjectCompProperties_EncampmentBonus()
    {
        compClass = typeof(WorldObjectComp_EncampmentBonus);
    }
}

/// <summary>
/// Provides a merc heal rate multiplier bonus to the settlement based on nearby
/// Outpost_Encampment Medicine skill, using the same scaling as Specialists.
/// Reads from EncampmentCache for performance.
/// </summary>
public class WorldObjectComp_EncampmentBonus : WorldObjectComp_OutpostProximityBonus
{
    protected override bool Active => EmpireVOESettings.EncampmentActive;
    protected override FCStatDef Stat => FCStatDefOf.mercHealRateMultiplier;

    protected override IOutpostProximityBonus GetEntry(WorldSettlementFC settlement)
        => EncampmentCache.GetOrBuild(settlement);

    protected override string DescLine(FCStatDef stat, IOutpostProximityBonus entry)
        => TextUtil.MultiplierBonusLine(
            stat.IdentityValue + entry.Bonus,
            "FCVOE_EncampmentHealRateSource".Translate(entry.SourceCount));
}
