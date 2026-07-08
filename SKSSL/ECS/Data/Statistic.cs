// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

using System.Globalization;

namespace SKSSL.ECS;

/// <summary>
/// A proper value-centric statistic.
/// </summary>
public readonly struct Statistic
{
    /// <summary>
    /// Creates a view over a statistic spread across multiple arrays
    /// </summary>
    public Statistic(
        string name,
        double baseValue,
        double currentValue,
        double minValue,
        double maxValue,
        StatisticModifier[] modifiers)
    {
        Name = name;
        BaseValue = baseValue;
        CurrentValue = currentValue;
        MinValue = minValue;
        MaxValue = maxValue;
        Modifiers = modifiers;
    }

    #region Fields

    public string Name { get; init; }
    public string Description { get; init; }
    public double BaseValue { get; init; }
    public double CurrentValue { get; init; }
    public double MinValue { get; init; }
    public double MaxValue { get; init; }

    /// Modifier data (can be a separate array of structs or multiple arrays)
    public readonly StatisticModifier[] Modifiers { get; init; }

    #endregion

    /// <summary>
    /// Recalculate CurrentValue from Base + Modifiers
    /// </summary>
    public void Recalculate()
    {
        // WIP: Doing this.
        double value = BaseValue;

        /*// Handle = (set) modifiers (last one wins)
        StatisticModifier setMod = Modifiers.Last();
        if (true)
        {
            value = setMod.ModifierText;
        }
        else
        {
            double additive = 0;
            double multiplier = 1;

            foreach (StatisticModifier mod in Modifiers)
            {
                switch (mod.Operator)
                {
                    case ModifierOperator.Additive:
                        additive += mod.ModifierText;
                        break;
                    case ModifierOperator.Multiplicative:
                        multiplier *= mod.ModifierText;
                        break;
                }
            }

            value = (value + additive) * multiplier;
        }*/

        //CurrentValue = Math.Clamp(value, MinValue, MaxValue);
    }

    public double GetValue() => BaseValue; // WIP: Improve this?

    public string GetValueAsString() => GetValue().ToString(CultureInfo.InvariantCulture);
}