using SKSSL.Mathematics;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

public class Modifier : Prototype, ICloneable<Modifier>
{
    //->+ source
    //->+ type
    //->+ handle

    /// The step or stage in which this modifier is applied.
    [YamlMember(Alias = "step")] public ModifierStep Step = ModifierStep.Final;

    /// The operator that will dictate how this modifier is applied to a variable.
    [YamlMember(Alias = "operator")] public ModifierOperator Operator = ModifierOperator.Add;

    /// How long this modifier will persist in game time.
    [YamlMember(Alias = "duration")] public float Duration = 1000f;

    /// If this statistic can exist more than once in a list.
    [YamlMember(Alias = "stacks")] public bool CanStack = false;

    /// Modifiers are string expressions. These expressions can also refer back to <see cref="Statistic"/>s,
    /// which can have more modifiers.
    /// <remarks>This is a self-looping, self-made nightmare.</remarks>
    /// <seealso cref="ShuntingYard"/>
    [YamlMember(Alias = "expression")] public string Expression = string.Empty;

    [YamlIgnore] public double? CachedValue = null;

    public Modifier Clone()
    {
        var prototype = new Modifier
        {
            Source = Source,
            Type = Type,
            Step = Step,
            Operator = Operator,
            Duration = Duration,
            Expression = Expression,
            CanStack = CanStack,
            CachedValue = CachedValue
        };
        prototype.CopyFrom(this); // Copy base data.
        return prototype;
    }
}