using YamlDotNet.Serialization;

namespace SKSSL.ECS;

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