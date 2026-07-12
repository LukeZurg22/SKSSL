using YamlDotNet.Serialization;

namespace SKSSL.ECS;

public class ModifierPrototype : Prototype
{
    [YamlMember(Alias = "type")] public override string Type { get; set; } = "Modifier";

    /// <summary>
    /// The operator that will dictate how this modifier is applied to a variable.
    /// </summary>
    [YamlMember(Alias = "operator")] public ModifierOperator Operator;

    /// <summary>
    /// The step or stage in which this modifier is applied.
    /// </summary>
    [YamlMember(Alias = "step")] public ModifierStep Step = ModifierStep.Final;

    /// <summary>
    /// Modifiers are strings. The shunting yard algorithm is used to decipher them.
    /// </summary>
    [YamlMember(Alias = "modifier")] public string Expression;
}