namespace SKSSL.ECS.Registry;

public class ModifierRegistry : Registry<ModifierPrototype>
{
    
    
    // TODO: Add modifier parsing / processing.
    
    /*
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
    }*/
}