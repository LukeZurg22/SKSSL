using YamlDotNet.Serialization;

namespace SKSSL.ECS;

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

    /// Addition.
    [YamlMember(Alias = "+")] Add = 1,

    /// Subtraction.
    [YamlMember(Alias = "-")] Subtract = 2,

    /// Lower to decimal places for division.
    [YamlMember(Alias = "*")] Multiply = 3,

    /// Division.
    [YamlMember(Alias = "/")] Divide = 4,

    /// Power-of operator. // TODO: Not currently supported.
    [YamlMember(Alias = "^")] Power = 5,

    /// Simply just *assigns* the value, above all else.
    [YamlMember(Alias = "=")] Override = 244,
}

[YamlSerializable]
public enum ModifierStep : byte
{
    /// "Apply [OPERATOR]X to base value."
    Base,

    /// Applied just after base value.
    Additive,

    /// Larger, more commonplace percentile changes.
    Multiplicative,

    /// Absolute changes, typically very small ones involving addition or low percentages.
    Final,
}