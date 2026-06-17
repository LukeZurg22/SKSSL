using System.Collections.Generic;

namespace SKSSL.ECS;

/// <summary>
/// Statistic object to deserialize.
/// </summary>
public struct Statistic
{
    public string ID;
    public string Name;
    public double BaseValue;
    public double CurrentValue;
    public double MinValue;
    public double MaxValue;

    /// List of modifiers.
    public List<StatisticModifier>? Modifiers;
}

public struct StatisticModifier
{
    
}