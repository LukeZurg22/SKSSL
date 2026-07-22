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
public class StatisticRegistry : Registry<Statistic>
{
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

    /// <remarks>
    /// Like the TryGet(uid), except much safer. Utilizes handles, which are not the most optimal, however.
    /// </remarks>
    // ReSharper disable once UnusedMethodReturnValue.Global
    public override bool TryGet(string handle, [NotNullWhen(true)] out Statistic? statistic)
    {
        statistic = default;
        // Using handle back to ID, get index, then construct wrapper.
        return _handleToIndex.TryGetValue(handle, out var index) && TryGet(index, out statistic);
    }

    /// <summary>
    /// Direct way to obtain an statistic definition using its index.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="statistic"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    private bool TryGet(int index, [NotNullWhen(true)] out Statistic? statistic)
    {
        // Short-circuit.
        if (index >= _handleToIndex.Count || _handles[index].IsNullOrEmpty())
        {
            statistic = default; // Fake statistic for compiler.
            return false;
        }

        // Recreates the statistic prototype, which may have been disposed-of earlier.
        statistic = new Statistic
        {
            Source = _sources[index],
            Handle = _handles[index],
            NameKey = _names[index],
            DescriptionKey = _descriptions[index],
            BaseValue = _baseValues[index],
            MaxValue = _maxValues[index],
            MinValue = _minValues[index],
            Modifiers = _modifierHandles[index].ToList(),
        };
        return true;
    }

    /// Avoid calling this manually unless you know what you are doing.
    public override object Register(string handle, Statistic entry)
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
        
            newSize -= 1; // For zero-based indexing.
            // Add entries to flourished structure of arrays .
            _handleToIndex.Add(handle, newSize);
            _sources[newSize] = entry.Source;
            _handles[newSize] = handle;
            _names[newSize] = entry.NameKey;
            _descriptions[newSize] = entry.DescriptionKey;
            _baseValues[newSize] = entry.BaseValue;
            _minValues[newSize] = entry.MinValue;
            _maxValues[newSize] = entry.MaxValue;
            _modifierHandles[newSize] = entry.Modifiers.ToHashSet();
        //@formatter:on

        return entry;
    }

    #region Utility

    public override bool Contains(string handle) => _handleToIndex.ContainsKey(handle);

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