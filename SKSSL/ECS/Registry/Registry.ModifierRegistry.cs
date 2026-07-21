using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using SKSSL.Extensions;
using SKSSL.Mathematics;

namespace SKSSL.ECS.Registry;

public class ModifierRegistry : Registry<Modifier>
{
    private readonly Dictionary<string, int> _handleToIndex = new(); // For reverse-searching.
    private string[] _indexToHandle = [];

    /// Storing pre-calculated simple numerical values to avoid expression parsing.
    private double?[] _cachedValues = [];

    /// The step or stage in which this modifier is applied.
    private ModifierStep[] _steps = [];

    /// The operator that will dictate how this modifier is applied to a variable.
    private ModifierOperator[] _operators = [];

    /// How long this modifier will persist in game time.
    private float[] _durations = [];

    /// Modifiers are string expressions. These expressions can also refer back to <see cref="Statistic"/>s,
    /// which can have more modifiers.
    /// <remarks>This is a self-looping, self-made nightmare.</remarks>
    /// <seealso cref="ShuntingYard"/>
    private string[] _expressions = [];

    public override bool TryGet(string handle, [NotNullWhen(true)] out Modifier? definition)
    {
        _handleToIndex.TryGetValue(handle, out var id);
        return TryGet(id, out definition);
    }

    private bool TryGet(int index, [NotNullWhen(true)] out Modifier? definition)
    {
        // Short-circuit.
        if (index >= _handleToIndex.Count || _indexToHandle[index].IsNullOrEmpty())
        {
            definition = default; // Fake for compiler.
            return false;
        }

        definition = new Modifier
        {
            Step = _steps[index],
            Operator = _operators[index],
            Duration = _durations[index],
            Expression = _expressions[index],
            CachedValue = _cachedValues[index],
        };
        return true;
    }

    public override object Register(string handle, Modifier entry)
    {
        // Expand all arrays by one.
        //@formatter:off
            int nextId = _indexToHandle.Length + 1; // Will force size to always have at least one more space.
            Array.Resize(ref _indexToHandle, nextId);
            Array.Resize(ref _steps, nextId);
            Array.Resize(ref _operators, nextId);
            Array.Resize(ref _durations, nextId);
            Array.Resize(ref _expressions, nextId);
            Array.Resize(ref _cachedValues, nextId);
                // Then, insert the values.
                nextId -= 1; // For zero-based indexing.
                _handleToIndex.Add(handle, nextId);
                _indexToHandle[nextId] = handle;
                _steps[nextId] = entry.Step;
                _operators[nextId] = entry.Operator;
                _durations[nextId] = entry.Duration;
                _expressions[nextId] = entry.Expression;
        //@formatter:on

        // Attempt to pre-cache value of this modifier.
        // TODO: Move to generic parse function that will instead attempt to calculate a string for a constant.
        //  If there would be any variables, then there can't be any caching.
        if (double.TryParse(entry.Expression, out double cached))
            _cachedValues[nextId] = cached;
        else
        {
            // Value is not a simple number!
            _cachedValues[nextId] = null;
            // This will need to be calculated sometime during the runtime. There is no "linking" step where modifiers
            //  are pre-parsed.
        }

        return entry;
    }

    public override void Clear()
    {
        // Clear SoA. 
        _handleToIndex.Clear();
        Array.Clear(_indexToHandle);
        Array.Clear(_steps);
        Array.Clear(_operators);
        Array.Clear(_durations);
        Array.Clear(_expressions);
    }

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