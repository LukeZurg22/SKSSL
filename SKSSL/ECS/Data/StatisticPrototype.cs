using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

/// <summary>
/// Deserializable statistic object.
/// </summary>
[YamlSerializable]
public class StatisticPrototype : Prototype
{
    // ->+ Handle ID    
    [YamlMember(Alias = "name")] public string NameKey = string.Empty;
    [YamlMember(Alias = "description")] public string DescriptionKey = string.Empty;
    [YamlMember(Alias = "stacks")] public bool Stacks = false; // If this statistic can exist more than once in a list.
    [YamlMember(Alias = "base")] public double BaseValue = 0;
    [YamlMember(Alias = "initial")] public double InitialValue = 0;
    [YamlMember(Alias = "minimum")] public double MinValue = double.MinValue;
    [YamlMember(Alias = "maximum")] public double MaxValue = double.MaxValue;

    /// List of modifier IDs.
    [YamlMember(Alias = "modifiers")] public List<string> Modifiers = [];
}