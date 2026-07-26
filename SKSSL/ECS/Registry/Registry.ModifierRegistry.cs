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

    /// Can the modifier stack with others sharing the same handle?
    public bool[] _canStack = [];


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
            Handle = _indexToHandle[index],
            Step = _steps[index],
            Operator = _operators[index],
            Duration = _durations[index],
            Expression = _expressions[index],
            CanStack = _canStack[index]
        };
        return true;
    }

    public bool CanStack(string handle)
    {
        _handleToIndex.TryGetValue(handle, out var id);
        return _canStack[id];
    }

    public override bool Contains(string handle) => _handleToIndex.TryGetValue(handle, out int _);

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
            Array.Resize(ref _canStack, nextId);
                // Then, insert the values.
                nextId -= 1; // For zero-based indexing.
                _handleToIndex.Add(handle, nextId);
                _indexToHandle[nextId] = handle;
                _steps[nextId] = entry.Step;
                _operators[nextId] = entry.Operator;
                _durations[nextId] = entry.Duration;
                _expressions[nextId] = entry.Expression;
                _canStack[nextId] = true;
        //@formatter:on
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
        Array.Clear(_canStack);
    }
}