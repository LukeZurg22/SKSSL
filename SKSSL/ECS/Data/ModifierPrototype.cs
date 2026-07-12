using SKSSL.Mathematics;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

public class ModifierPrototype : Prototype
{
    //->+ source
    //->+ type
    //->+ handle

    /// <summary>
    /// The operator that will dictate how this modifier is applied to a variable.
    /// </summary>
    [YamlMember(Alias = "operator")] public ModifierOperator Operator = ModifierOperator.Add;

    /// <summary>
    /// The step or stage in which this modifier is applied.
    /// </summary>
    [YamlMember(Alias = "step")] public ModifierStep Step = ModifierStep.Final;

    /// <summary>
    /// Modifiers are string expressions. The <see cref="ShuntingYard"/> Algorithm is used to expand the expression.
    /// </summary>
    [YamlMember(Alias = "modifier")] public string Expression  = string.Empty;
}