using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    private readonly Dictionary<string, int> _handleToIndex = new(); // For reverse-searching.
    private string[] _sources = Array.Empty<string>();
    private string[] _handles = Array.Empty<string>();
    private string[] _names = Array.Empty<string>(); // Localization keys (name).
    private string[] _descriptions = Array.Empty<string>(); // Localization keys (desc).
    private double[] _baseValues = Array.Empty<double>();
    private double[] _minValues = Array.Empty<double>();
    private double[] _maxValues = Array.Empty<double>();
    private HashSet<string>[] _modifierHandles = Array.Empty<HashSet<string>>();

    // WIP:
    //  Add string parsing for statistics, which all begin with an [op] followed by a string.
    //      -> Once statistics are stored, that does NOT mean they should initialize or begin updating.
    //          Some init() method or whatever is needed to force cross-reference updates -is needed.
    //          Recursive statistic calls is a danger to us all! Some kind of precaution is needed.
    //              Circular check? Simply store ID and pass-down. If the ID is ever called again, then it's circular.
    //  Add the ability to recalculate / re-parse statistics.

    /// Like the TryGet(uid), except much more unreliable and failure-prone.
    public bool TryGet(string handle, [NotNullWhen(true)] out StatisticPrototype? statistic)
    {
        bool found = false;
        statistic = default;

        // Using handle back to ID, get index, then construct wrapper.
        if (_handleToIndex.TryGetValue(handle, out var index))
            found = TryGet(index, out statistic);

        return found;
    }

    /// <summary>
    /// Safer way to obtain an statistic definition using its ID.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="statistic"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public bool TryGet(int index, [NotNullWhen(true)] out StatisticPrototype? statistic)
    {
        // Short-circuit.
        if (index >= _handleToIndex.Count || _handles[index].IsNullOrEmpty())
        {
            statistic = default; // Fake statistic for compiler.
            return false;
        }

        // Recreates the statistic prototype, which may have been disposed-of earlier.
        var wrapper = new StatisticPrototype
        {
            Source = _sources[index],
            Handle = _handles[index],
            NameKey = _names[index],
            DescriptionKey = _descriptions[index],
            BaseValue = _baseValues[index],
            MaxValue = _maxValues[index],
            MinValue = _minValues[index],
            Modifiers = _modifierHandles[index].ToList()
        };
        statistic = wrapper;
        return true;
    }

    /// Next index in the statistics definitions lists.
    private int nextIndex = 0;

    /// Avoid calling this manually unless you know what you are doing.
    public override object Register(string handle, StatisticPrototype entry)
    {
        //@formatter:off
        // Expand all arrays by one.
            int newSize = _handles.Length + 1;
            Array.Resize(ref _sources, newSize);
            Array.Resize(ref _handles, newSize);
            Array.Resize(ref _names, newSize);
            Array.Resize(ref _descriptions, newSize);
            Array.Resize(ref _baseValues, newSize);
            Array.Resize(ref _minValues, newSize);
            Array.Resize(ref _maxValues, newSize);
            Array.Resize(ref _modifierHandles, newSize);
        
        // Add entry.
            _sources[nextIndex] = entry.Source;
            _handleToIndex.Add(handle, nextIndex);
            _handles[nextIndex] = handle;
            _names[nextIndex] = entry.NameKey;
            _descriptions[nextIndex] = entry.DescriptionKey;
            _baseValues[nextIndex] = entry.BaseValue;
            _minValues[nextIndex] = entry.MinValue;
            _maxValues[nextIndex] = entry.MaxValue;
        //@formatter:on
        return ++nextIndex;
    }

    #region Utility

    /// <summary>
    /// Remove all entities contained in statistic Manager.
    /// </summary>
    public override void Clear()
    {
        // Clear SoA. 
        _handleToIndex.Clear();
        Array.Clear(_sources);
        Array.Clear(_handles);
        Array.Clear(_names);
        Array.Clear(_descriptions);
        Array.Clear(_baseValues);
        Array.Clear(_minValues);
        Array.Clear(_maxValues);

        // Clean up modifier list's lists first.
        foreach (var modList in _modifierHandles) modList.Clear();
        Array.Clear(_modifierHandles); // Finish with the modifier list. 
    }

    #endregion
}