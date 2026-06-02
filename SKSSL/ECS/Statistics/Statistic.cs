using System.Collections.Generic;
using SKSSL.YAML;

namespace SKSSL.ECS.Statistics;

public struct Statistic
{
    public string ID { get; set; }
    public string Name { get; set; }
    public double BaseValue { get; set; }
    public double CurrentValue { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    
    // Optional: list of modifiers, or use a separate Modifier component
    public List<StatisticModifier> Modifiers { get; set; }
}

public struct StatisticModifier
{
    
}