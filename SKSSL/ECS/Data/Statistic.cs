using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

/// (De)serializable Statistic object.
[YamlSerializable]
public class Statistic : Prototype, ICloneable<Statistic>
{
    // ->+ Handle ID    
    [YamlMember(Alias = "name")] public string NameKey = string.Empty;
    [YamlMember(Alias = "description")] public string DescriptionKey = string.Empty;
    [YamlMember(Alias = "initial")] public double BaseValue = 0;
    [YamlMember(Alias = "minimum")] public double MinValue = double.MinValue;
    [YamlMember(Alias = "maximum")] public double MaxValue = double.MaxValue;

    /// List of modifier IDs.
    [YamlMember(Alias = "modifiers")] public List<string> Modifiers = [];

    public Statistic Clone()
    {
        var clone = new Statistic
        {
            NameKey = NameKey,
            DescriptionKey = DescriptionKey,
            BaseValue = BaseValue,
            MinValue = MinValue,
            MaxValue = MaxValue,
            Modifiers = Modifiers.ToList(),
        };
        clone.CopyFrom(this);
        return clone;
    }
}