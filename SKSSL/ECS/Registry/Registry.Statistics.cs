using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SKSSL.Extensions;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UseCollectionExpression
// ReSharper disable UnusedType.Global

namespace SKSSL.ECS.Registry;

/// <remarks>
/// Rather than permit KeyLists to the realm of the Raw Prototypes registry, they are allocated here.
/// This works 1:1 with the raw registry with the added benefit of being isolated into their own, and with
/// special handling for localized key-list lists.
/// </remarks>
public class StatisticRegistry : Registry<StatisticPrototype>
{
    //MasterRegistryManager.Registries[typeof(Registry<Statistic>)]

    /*
     * Statistics definitions are loaded from files and stored here.
     */

    // SoA format for storing the various parts of Statistics definitions. Makes them difficult to expand upon.
    private readonly Dictionary<string, uint> _handleToId = new(); // For reverse-searching.
    private string[] _handles = Array.Empty<string>();
    private string[] _names = Array.Empty<string>(); // Localization keys (name).
    private string[] _descriptions = Array.Empty<string>(); // Localization keys (desc).
    private double[] _baseValues = Array.Empty<double>();
    private double[] _minValues = Array.Empty<double>();
    private double[] _maxValues = Array.Empty<double>();
    private List<string>[] _modifiers = new List<string>[1024];

    // WIP:
    //  Add string parsing for statistics, which all begin with an [op] followed by a string.
    //      -> Remove the operator/
    //      -> Once statistics are stored, that does NOT mean they should initialize or begin updating.
    //          Some init() method or whatever is needed to force cross-reference updates -is needed.
    //          Recursive statistic calls is a danger to us all! Some kind of precaution is needed.
    //              Circular check? Simply store ID and pass-down. If the ID is ever called again, then it's circular.
    //  Add the ability to recalculate / re-parse statistics.

    /// Like the TryGet(uid), except much more unreliable and failure-prone.
    public bool TryGet(string handle, out StatisticWrapper statisticWrapper)
    {
        bool found = false;
        statisticWrapper = default;

        // Using handle back to ID, get index, then construct wrapper.
        if (_handleToId.TryGetValue(handle, out var index))
            found = TryGet(index, out statisticWrapper);

        return found;
    }

    /// <summary>
    /// Safer way to obtain an statistic definition using its ID.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="statisticWrapper"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public bool TryGet(uint index, out StatisticWrapper statisticWrapper)
    {
        statisticWrapper = default; // Fake statistic for compiler.

        // Short-circuit.
        if (index >= _handleToId.Count || _handles[index].IsNullOrEmpty())
            return false;

        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        _modifiers[index] ??= new List<string>();

        // Fill out real statistic reference.
        var wrapper = new StatisticWrapper
        {
            Name = _names[index],
            Handle = _handles[index],
            Description = _descriptions[index],
            BaseValue = _baseValues[index],
            MaxValue = _maxValues[index],
            MinValue = _minValues[index],
            // TODO: Get modifier reference.
            //Modifiers = _modifiers[index].ToArray()
        };
        statisticWrapper = wrapper;
        return true;
    }

    /// Next index in the statistics definitions lists.
    private uint nextIndex = 0;

    /// Avoid calling this manually unless you know what you are doing.
    public override object Register(string handle, StatisticPrototype entry)
    {
        _handleToId.Add(handle, nextIndex);
        _handles[nextIndex] = handle;
        _names[nextIndex] = entry.NameKey;
        _descriptions[nextIndex] = entry.DescriptionKey;
        _baseValues[nextIndex] = entry.BaseValue;
        _minValues[nextIndex] = entry.MinValue;
        _maxValues[nextIndex] = entry.MaxValue;
        return ++nextIndex;
    }

    /// [ Called by Source Generator ]
    [UsedImplicitly]
    public override void Link()
    {
        // WIP: Populate modifiers.
    }

    #region Utility

    /// <summary>
    /// Remove all entities contained in statistic Manager.
    /// </summary>
    public override void Clear()
    {
        // Clear SoA. 
        _handleToId.Clear();
        Array.Clear(_handles);
        Array.Clear(_names);
        Array.Clear(_descriptions);
        Array.Clear(_baseValues);
        Array.Clear(_minValues);
        Array.Clear(_maxValues);

        // Clean up modifier list's lists first.
        foreach (var modList in _modifiers)
        {
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            modList?.Clear();
        }

        Array.Clear(_modifiers); // Finish with the modifier list. 
    }

    #endregion
}