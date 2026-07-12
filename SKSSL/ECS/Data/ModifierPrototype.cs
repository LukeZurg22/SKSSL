using SKSSL.Mathematics;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

public class ModifierPrototype
{
    /// <summary>
    /// The operator that will dictate how this modifier is applied to a variable.
    /// </summary>
    [YamlMember(Alias = "operator")] public readonly ModifierOperator Operator;

    /// <summary>
    /// The step or stage in which this modifier is applied.
    /// </summary>
    [YamlMember(Alias = "step")] public readonly ModifierStep Step;

    /// <summary>
    /// Modifiers are strings. The shunting yard algorithm is used to decipher them.
    /// </summary>
    [YamlMember(Alias = "modifier")] public readonly string Expression;

    public ModifierPrototype()
    {
        
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="operator">
    /// Optional forced-operator. If not provided, will default to NoOp. and will read the first character, expecting
    /// an operator.
    /// </param>
    public ModifierPrototype(string expression, ModifierOperator @operator = ModifierOperator.NoOperator)
    {
        Operator = @operator;
        Expression = expression;

        if (@operator != ModifierOperator.NoOperator)
        {
            if (expression.StartsWith('='))
            {
                Operator = ModifierOperator.Override;
            }
            else if (expression.StartsWith('/'))
            {
                Operator = ModifierOperator.Divide;
            }
            else if (expression.StartsWith('*'))
            {
                Operator = ModifierOperator.Multiply;
            }
            else if (expression.StartsWith('+'))
            {
                Operator = ModifierOperator.Add;
            }
            else if (expression.StartsWith('-'))
            {
                Operator = ModifierOperator.Subtract;
                // WARN: Unary Minus may be an issue. "I.e. X {-5+9}..." where the whole string is evaluated as X-(5+9)
            }

            Expression = expression[1..].Trim();
        }
        else
        {
            Operator = @operator;
            Expression = expression.Trim();
        }
    }

    /// <summary>
    /// Parse strings like "+25", "*0.8", "=-10", "15" (defaults to additive)
    /// </summary>
    public ref double ModifyValue(ref double value)
    {
        if (string.IsNullOrWhiteSpace(Expression))
        {
            return ref value;
        }

        // Attempt to hot-evaluate the expression as a simple number and save the headache of using the Shunting
        //  Yard Algorithm. Subtraction is treated as an additive unary minus.
        if (!double.TryParse(Expression, out double result))
        {
            // WARN: MAY INVOLVE CIRCULAR CALLS FROM HERE. THERE ARE NO DEPTH CHECKS, YET!!
            // Evaluate this expression.
            ShuntingYard.Evaluate(Expression, out result);
        }

        switch (Operator)
        {
            case ModifierOperator.Add:
                value += result;
                break;
            case ModifierOperator.Subtract:
                value -= result;
                break;
            case ModifierOperator.Override:
                value = result;
                break;
            case ModifierOperator.Multiply:
                value *= result;
                break;
            case ModifierOperator.Divide:
                value /= result;
                break;
        }

        return ref value;
    }
}