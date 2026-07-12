// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

using System;
using System.Globalization;

namespace SKSSL.ECS;

/// <summary>
/// A proper statistic.
/// </summary>
public readonly struct StatisticWrapper
{
    /// <summary>
    /// Creates a view over a statistic spread across multiple arrays
    /// </summary>
    public StatisticWrapper(
        string handle,
        string name,
        double baseValue,
        double minValue,
        double maxValue,
        ModifierPrototype[] modifiers)
    {
        Handle = handle;
        Name = name;
        BaseValue = baseValue;
        MinValue = minValue;
        MaxValue = maxValue;
        Modifiers = modifiers;
    }

    #region Fields

    public string Handle { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public double BaseValue { get; init; }
    public double MinValue { get; init; }
    public double MaxValue { get; init; }

    /// Modifier data (can be a separate array of structs or multiple arrays)
    public readonly ModifierPrototype[] Modifiers { get; init; }

    #endregion

    /// <returns>Full value of statistic with respect to its modifiers.</returns>
    public double GetValue()
    {
        var value = BaseValue;

        // Sort the list first by applicative step, then by precedence implicit by the operator position in the
        //  enumerable type.
        Array.Sort(Modifiers, (a, b) =>
        {
            int stage = a.Step.CompareTo(b.Step);
            return stage != 0 ? stage : b.Operator.CompareTo(a.Operator);
        });
        
        foreach (ModifierPrototype modifier in Modifiers)
            modifier.ModifyValue(ref value);
        return value;
    }

    public string GetValueAsString() => GetValue().ToString(CultureInfo.InvariantCulture);
}