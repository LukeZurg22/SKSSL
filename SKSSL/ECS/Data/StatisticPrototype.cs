using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

/// <summary>
/// Deserialized statistic object.
/// </summary>
[YamlSerializable]
public class StatisticPrototype : Prototype
{
    // ->+ Handle ID    
    [YamlMember(Alias = "name")] public string NameKey = string.Empty;
    [YamlMember(Alias = "description")] public string DescriptionKey = string.Empty;
    [YamlMember] public double BaseValue = 0;
    [YamlMember] public double CurrentValue = 0;
    [YamlMember] public double MinValue = double.MinValue;
    [YamlMember] public double MaxValue = double.MaxValue;

    /// List of modifiers.
    [YamlMember] public List<StatisticModifier>? Modifiers = [];
}

public class StatisticModifier
{
    public readonly ModifierOperator Operator;

    /// <summary>
    /// Modifiers are strings. The shunting yard algorithm is used to decipher them.
    /// </summary>
    [YamlMember(Alias = "modifier")] public readonly string ModifierText;

    public StatisticModifier(ModifierOperator op, string modifierText)
    {
        Operator = op;
        ModifierText = modifierText;
    }

    /// <summary>
    /// Parse strings like "+25", "*0.8", "=-10", "15" (defaults to additive)
    /// </summary>
    public double? GetValue(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        var op = ModifierOperator.Additive;
        string valueStr = input;

        if (input.StartsWith('='))
        {
            op = ModifierOperator.Equalitative;
            valueStr = input[1..];
        }
        else if (input.StartsWith('*'))
        {
            op = ModifierOperator.Multiplicative;
            valueStr = input[1..];
        }
        else if (input.StartsWith('+'))
        {
            valueStr = input[1..];
        }
        else if (input.StartsWith('-'))
            op = ModifierOperator.Additive; // negative additive = subtract

        //if (double.TryParse(valueStr, out double value))
        //    return new StatisticModifier(op, value);

        return null;
    }
}

/// <summary>
/// Ignore "_" = 0,<br/>
/// Additive "+" = 1,<br/>
/// Multiplicative "*" = 2,<br/>
/// Equalitative "=" = 3<br/>
/// <br/>
/// Subtraction is done with adding negative numbers.
/// Likewise, division is done using multiplication with floating-point numbers.
/// </summary>
/// <code>
/// Examples:
///     "+-5"
///     "*0.3"
///     "*-3.14"
///     "=7"
///     "=-25"
/// </code>
[YamlSerializable]
public enum ModifierOperator : byte
{
    /// Ignores the modifier. One probably shouldn't do this.
    [YamlMember(Alias = "nop")] NoOperator = 0,

    /// Append - for subtraction.
    [YamlMember(Alias = "+")] Additive = 1,

    /// Lower to decimal places for division.
    [YamlMember(Alias = "*")] Multiplicative = 2,

    /// Simply just *assigns* the value, above all else.
    [YamlMember(Alias = "=")] Equalitative = 3,
}