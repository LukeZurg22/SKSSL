using System;
using System.Collections.Generic;
using System.Linq;
using static SKSSL.ECS.UidPacker;
using static SKSSL.SSLGame;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UseCollectionExpression
// ReSharper disable UnusedMember.Global
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

    /// <summary>
    /// Total instances of all IDs and handles.
    /// </summary>
    public readonly Dictionary<string, List<StatisticUid>> AllHandles = new(2048);

    // Statistics parts.
    private string?[] _handles = Array.Empty<string>(); // For reverse-handles searching.
    private string[] _names = Array.Empty<string>(); // Localization keys (name).
    private string[] _descriptions = Array.Empty<string>(); // Localization keys (desc).
    private double[] _baseValues = Array.Empty<double>();
    private double[] _currentValues = Array.Empty<double>();
    private double[] _minValues = Array.Empty<double>();
    private double[] _maxValues = Array.Empty<double>();
    private List<StatisticModifier>[] _modifiers = new List<StatisticModifier>[1024];

    private int[] _generations = new int[1024];
    private int[] _freeList = new int[Config.DESTROY_STATISTIC_CACHE_LIMIT];
    private int _freeCount = 0;

    // WIP:
    //  Add string parsing for statistics, which all begin with an [op] followed by a string.
    //      -> Remove the operator/
    //      -> Once statistics are stored, that does NOT mean they should initialize or begin updating.
    //          Some init() method or whatever is needed to force cross-reference updates -is needed.
    //          Recursive statistic calls is a danger to us all! Some kind of precaution is needed.
    //              Circular check? Simply store ID and pass-down. If the ID is ever called again, then it's circular.
    //  Add the ability to recalculate / re-parse statistics.

    /// Like the TryGet(uid), except much more unreliable and failure-prone.
    public bool TryGet(string handle, out Statistic statistic)
    {
        bool test = false;
        statistic = default;

        // Nothing in here? Well, that's not good!
        // Use the first entry and hope it's good enough.
        if (!AllHandles.TryGetValue(handle, out var value))
        {
            test = false;
        }

        if (value != null && value.Count == 0)
        {
            test = false;
        }

        if (value != null)
        {
            test = TryGet(value.First(), out statistic);
        }

        return test;
    }

    /// <summary>
    /// Safer way to obtain an statistic definition using its ID.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="statistic"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public bool TryGet(StatisticUid uid, out Statistic statistic)
    {
        int index = UnpackIndex(uid);
        int generation = UnpackGeneration(uid);

        statistic = default; // Fake statistic for compiler.

        // Short-circuit.
        if ((uint)index >= (uint)_handles.Length
            || _generations[index] != generation
            || _handles[index] == null)
            return false;


        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        _modifiers[index] ??= new List<StatisticModifier>();

        // Fill out real statistic reference.
        var statistica = new Statistic
        {
            Name = _names[index],
            Description = _descriptions[index],
            BaseValue = _baseValues[index],
            CurrentValue = _currentValues[index],
            MaxValue = _maxValues[index],
            MinValue = _minValues[index],
            Modifiers = _modifiers[index].ToArray()
        };
        statistic = statistica;
        return true;
    }

    /// <summary>
    /// For when the developer just wants a darned statistic without going through any fuss with [de]serialization. 
    /// </summary>
    /// <param name="handle">The ID that which the statistic will be referenced-by.</param>
    /// <param name="value">The base value of the statistic.</param>
    public StatisticUid Register(string handle, double value)
    {
        StatisticPrototype pseudoPrototype = new()
        {
            Handle = handle,
            NameKey = string.Empty, // No custom name. Just leave blank.
            DescriptionKey = string.Empty, // No custom description, either! 
            BaseValue = value,
            CurrentValue = value,
            Source = "game",
        };
        return (StatisticUid)Register(handle, pseudoPrototype);
    }

    /// Avoid calling this manually unless you know what you are doing.
    public override object Register(string handle, StatisticPrototype entry)
    {
        StatisticUid id = CreateUID();

        // Add ID to handles super-list.
        if (AllHandles.TryGetValue(handle, out var handleIds)) handleIds.Add(id);
        else AllHandles.Add(handle, [id]);

        _handles[id] = handle;
        _names[id] = entry.NameKey;
        _descriptions[id] = entry.DescriptionKey;
        _baseValues[id] = entry.BaseValue;
        _currentValues[id] = entry.CurrentValue;
        _minValues[id] = entry.MinValue;
        _maxValues[id] = entry.MaxValue;

        return id;
    }

    #region Utility

    /// Create unique ID for statistic.
    private StatisticUid CreateUID()
    {
        int index;

        if (_freeCount > 0)
        {
            // Reuse old indices.
            index = _freeList[--_freeCount];
        }
        else
        {
            index = _handles.Length;

            // Ensure free list has plenty of space.
            if (_freeCount >= _freeList.Length) Array.Resize(ref _freeList, _freeList.Length * 2);

            Array.Resize(ref _handles, index + 1);
            Array.Resize(ref _handles, index + 1);
            Array.Resize(ref _names, index + 1);
            Array.Resize(ref _descriptions, index + 1);
            Array.Resize(ref _baseValues, index + 1);
            Array.Resize(ref _currentValues, index + 1);
            Array.Resize(ref _minValues, index + 1);
            Array.Resize(ref _maxValues, index + 1);
            Array.Resize(ref _modifiers, index + 1);
            Array.Resize(ref _generations, index + 1);
        }

        int generation = _generations[index];
        return new StatisticUid(index, generation);
    }

    public void Destroy(StatisticUid uid)
    {
        int index = (int)(uid.Value & 0xFFFF);
        int generation = (int)(uid.Value >> 16);

        if ((uint)index >= (uint)_handles.Length)
            return;

        // Validate generation (prevents double-destroy bugs)
        if (_generations[index] != generation)
            return;

        // Remove this UID from all handles' known IDs.
        var handle = _handles[index];
        if (handle == null)
            return;

        AllHandles[handle].Remove(uid);

        // Remove entries.
        _handles[index] = string.Empty;
        _names[index] = string.Empty;
        _descriptions[index] = string.Empty;
        _baseValues[index] = 0;
        _currentValues[index] = 0;
        _minValues[index] = double.MinValue;
        _maxValues[index] = double.MaxValue;

        // Invalidate old IDs.
        _generations[index]++;

        // Ensure free list has plenty of space.
        if (_freeCount >= _freeList.Length) Array.Resize(ref _freeList, _freeList.Length * 2);

        // Add slot back to free list.
        _freeList[_freeCount++] = index;
    }

    public bool IsValid(StatisticUid uid)
    {
        int index = uid.Index;

        if ((uint)index >= (uint)_handles.Length)
            return false;

        var handle = _handles[index];
        if (handle == null)
            return false;

        return _generations[index] == uid.Generation;
    }

    /// <summary>
    /// Remove all entities contained in statistic Manager.
    /// </summary>
    public override void Clear()
    {
        // Clear SoA. 
        Array.Clear(_handles);
        Array.Clear(_names);
        Array.Clear(_descriptions);
        Array.Clear(_baseValues);
        Array.Clear(_currentValues);
        Array.Clear(_minValues);
        Array.Clear(_maxValues);

        // Clean up modifier list's lists first.
        foreach (var modList in _modifiers)
        {
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            modList?.Clear();
        }

        Array.Clear(_modifiers); // Finish with the modifier list. 

        Array.Clear(_generations);
        _freeCount = 0;
        for (int i = 0; i < _handles.Length; i++)
        {
            _generations[i]++; // Invalidate all old ID's.
            _freeList[_freeCount++] = i;
        }
    }

    #endregion
}