using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable UnusedMember.Global

// ReSharper disable UseCollectionExpression

namespace SKSSL.ECS;

/// <summary>
/// A dynamic type of list split in a Struct of Arrays format, which can be inherited and expanded.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <remarks>
/// Best used for "Active" lists where dynamacy is a requirement. Definitions lists and registries
/// should not need Unique IDs.
/// </remarks>
public class UidList<T> : IEnumerable<T>
{
    /// One handle can be instantiated multiple times.
    private readonly Dictionary<string, List<PackableUid>> _activeHandles = new();

    private IList<T> _items = new List<T>();
    public ref IList<T> AllEntries => ref _items;

    private string?[] _idToHandles = Array.Empty<string>();
    private int[] _generations = new int[1024];
    private int[] _freeList = new int[SSLGame.Config.DESTROY_CACHE_LIMIT];
    private int _freeCount = 0;

    public int Count => _items.Count;

    public virtual void Clear()
    {
        var packableUids = _activeHandles.Values.GetEnumerator().Current;
        foreach (PackableUid id in packableUids)
            Destroy(id);
        _activeHandles.Clear();

        // Handle Generational ID stuff.
        Array.Clear(_generations);
        _freeCount = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            _generations[i]++; // Invalidate all old ID's.
            _freeList[_freeCount++] = i;
        }
    }

    /// <summary>
    /// Dangerous fast access. Throws if the UID is invalid or stale.
    /// Use <see cref="TryGet"/> for safer access.
    /// </summary>
    public T Get(PackableUid uid)
    {
        int index = uid.Index;
        int generation = uid.Generation;

        // Another "IsValid" check, but divided.
        if (index < 0 || index >= _items.Count)
            throw new IndexOutOfRangeException($"Invalid UID index: {index}");

        if (_generations[index] != generation)
            throw new InvalidOperationException(
                $"Stale UID (generation mismatch). UID was destroyed or reused. Index: {index}");

        T item = _items[index]!;

        if (item == null)
            throw new InvalidOperationException($"Slot is empty for UID index: {index}");

        return item;
    }

    /// <summary>
    /// Safe way to retrieve an item by UID.
    /// </summary>
    public bool TryGet(PackableUid uid, [NotNullWhen(true)] out T? item)
    {
        int index = uid.Index;
        item = default;

        if (!IsValid(uid))
            return false; // Short-Circuit.

        item = _items[index]!;
        return true;
    }

    /// Create unique ID for statistic.
    public PackableUid New(string handle = "")
    {
        int index;

        if (_freeCount > 0)
        {
            // Reuse old indices.
            index = _freeList[--_freeCount];
        }
        else
        {
            index = _activeHandles.Count;

            // Ensure free list has plenty of space by doubling it every time it reaches over limit.
            if (_freeCount >= _freeList.Length)
                Array.Resize(ref _freeList, _freeList.Length * 2);

            Array.Resize(ref _idToHandles, index + 1);
            Array.Resize(ref _generations, index + 1);
        }

        int generation = _generations[index];

        var guid = new GenericUid(index, generation);
        if (!_activeHandles.ContainsKey(handle)) _activeHandles.Add(handle, new List<PackableUid>());
        _activeHandles[handle].Add(guid);
        return guid;
    }

    public void Destroy(PackableUid uid)
    {
        int index = uid.Index;
        int generation = uid.Generation;

        if (index >= _activeHandles.Count)
            return;

        // Validate generation (prevent double-destroy bugs)
        if (_generations[index] != generation)
            return;

        // Remove this UID from all handles' known IDs.
        _items.RemoveAt(index);
        var handle = _idToHandles[index];
        if (handle == null)
            return;

        // Remove entry handles.
        _activeHandles[handle].Remove(uid);
        _idToHandles[index] = string.Empty;

        // Invalidate old IDs.
        _generations[index]++;

        // Ensure free list has plenty of space.
        if (_freeCount >= _freeList.Length) Array.Resize(ref _freeList, _freeList.Length * 2);

        // Add slot back to free list.
        _freeList[_freeCount++] = index;
    }

    /// <returns>True if Uid is valid; false if not.</returns>
    public bool IsValid(PackableUid uid)
    {
        int index = uid.Index;
        if (index < 0 || index >= _items.Count)
            return false;

        return _generations[index] == uid.Generation && _items[index] != null;
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}