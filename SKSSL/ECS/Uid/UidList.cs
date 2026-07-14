using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Xna.Framework;
using static System.Math;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UseCollectionExpression

namespace SKSSL.ECS;

/// <summary>
/// A dynamic type of list split in a Struct of Arrays format, which can be inherited and expanded.
/// Operate using the <see cref="New"/>; <see cref="Set"/>; <see cref="Destroy"/>; <see cref="Clear"/> functions.
/// </summary>
/// <typeparam name="T">Object type that Uids are assigned to. Stored in internal linear list on HEAP.</typeparam>
/// <code>
/// (Below is Pseudo-Code)
/// var uidList = new UidList of type MyObject();
/// var uid = uidList.New();
/// object myObject = new();
/// uidList.Set(myObject, uid);
/// uidList.Destroy(uid)
/// </code>
/// <remarks>
/// Best used for "Active" lists where dynamacy is a requirement. Definitions lists and registries
/// should not need Unique IDs.
/// </remarks>
public class UidList<T> : IEnumerable<T> where T : class
{
    /// One handle can be instantiated multiple times.
    private readonly Dictionary<string, HashSet<PackableUid>> _activeHandles = new();

    /// All objects stored in this UidList which are assigned unique identification numbers.
    private readonly List<T> _denseEntries = new();

    /// Public-Access list to entries contained in this UidList, which cannot be modified directly.
    public IReadOnlyList<T> Entries => _denseEntries;

    // Per-slot handle metadata.
    /*
     * Index to Dense maps the Object Uid to a Dense Index.
     * Dense to Index holds slots for Uids, and the index of the DenseToIndex list allows one to iterate through these
     *  active Uids.
     * ──────────────────────────────────────────────────────
     * |   L I F E S P A N   O F   A   U N I Q U E   I D    |
     * | State              | Generation    | _indexToDense |
     * | ────────────────── | ────────────  | ───────────── |
     * | Never allocated    | 0             | -1            |
     * | Reserved (New())   | >0            | -1            |
     * | Alive (Get())      | >0            | >=0           |
     * | Destroyed          | incremented   | -1            |
     * ──────────────────────────────────────────────────────
     */
    private int[] _indexToDense = Array.Empty<int>(); // Maps stable UID index to actual position in _denseEntries.
    private int[] _denseToIndex = Array.Empty<int>(); // Reverse Mapping dense index -> UID.Index for swap & destroy.
    private string?[] _idToHandles = Array.Empty<string>();
    private int[] _generations = Array.Empty<int>();
    private int[] _freeList = new int[SSLGame.Config.DESTROY_CACHE_LIMIT];
    private int _freeCount = 0;
    private int _nextUidIndex = 0;

    #region Operational Methods

    public void Clear()
    {
        _activeHandles.Clear();
        ObjectClear();
        _freeCount = 0;

        int length = _indexToDense.Length;
        Array.Clear(_indexToDense, 0, length);
        Array.Clear(_denseToIndex, 0, length);
        Array.Clear(_idToHandles, 0, length);

        for (int i = 0; i < length; i++)
        {
            _indexToDense[i] = -1;
            _generations[i]++;
        }
    }

    /// <inheritdoc cref="Get(PackableUid)"/>
    public T Get(int index, int generation) => Get(new GenericUid(index, generation));

    /// <summary>
    /// Fast access. Throws on invalid/stale UID.
    /// Use <see cref="TryGet"/> for safer access.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public T Get(PackableUid uid)
    {
        if (!TryGet(uid, out T? item))
            throw new InvalidOperationException($"Invalid or stale UID: {uid}");
        return item;
    }

    /// <summary>
    /// Safe way to retrieve an item by UID.
    /// </summary>
    public bool TryGet(PackableUid uid, [NotNullWhen(true)] out T? item)
    {
        item = default;
        if (!IsAlive(uid)) return false;

        int denseIndex = _indexToDense[uid.Index];
        item = ObjectConstruct(denseIndex);
        return true;
    }
    
    /// <summary>
    /// Feeling brave? Sure you are!
    /// </summary>
    public T Get(string handle, int index = 0)
    {
        if (string.IsNullOrEmpty(handle) || !_activeHandles.TryGetValue(handle, out var uidList))
            throw new Exception($"Invalid handle {handle} on attempt to access UidList \'Get\' call.");
        return Get(uidList.ToList()[index]);
    }

    /// <summary>
    /// Returns all live objects associated with a specific handle.
    /// Fast enumeration over the handle's UIDs.
    /// </summary>
    public IEnumerable<T> GetAll(string handle)
    {
        if (string.IsNullOrEmpty(handle) || !_activeHandles.TryGetValue(handle, out var uidList))
            yield break;

        foreach (PackableUid uid in uidList)
            if (TryGet(uid, out T? item))
                yield return item;
    }

    /// <returns>Uid-Object Tuple enumerable using handle.</returns>
    public IEnumerable<(PackableUid Uid, T Value)> GetAllKVP(string handle)
    {
        if (string.IsNullOrEmpty(handle) || !_activeHandles.TryGetValue(handle, out var uidList))
            yield break;

        foreach (PackableUid uid in uidList)
            if (TryGet(uid, out T? item))
                yield return (uid, item);
    }

    /// Create unique ID for storage. Reserves this Uid for an entity, which is assigned shortly after.
    public PackableUid New()
    {
        int uidIndex;
        // Reuse a previously destroyed slot.
        if (_freeCount > 0)
        {
            uidIndex = _freeList[--_freeCount];
        }
        // Allocate a brand new slot.
        else
        {
            uidIndex = _nextUidIndex++;
            EnsureCapacity(uidIndex + 1);
        }

        // Increment generation on first use of a recycled slot.
        if (_generations[uidIndex] == 0)
            _generations[uidIndex] = 1;

        return new GenericUid(uidIndex, _generations[uidIndex]);
    }

    /// <summary>
    /// Add instance of an object of Type T.
    /// </summary>
    /// <param name="instance">Instance of the object that is wished to be stored.</param>
    /// <param name="uid">Uid of the object to assign it.</param>
    /// <param name="handle">Optional handle to bundle uids under handle groupings.</param>
    public void Set(T instance, PackableUid? uid = null, string handle = "")
    {
        ArgumentNullException.ThrowIfNull(instance);

        // If there is no explicit Uid provided, then use InternalUidObject Uid, or generate a new one if it is not
        //  a type that contains an internal Uid.
        uid ??= instance is InternalUidObject iUidObject ? iUidObject.Uid : New();
        if (!IsReserved(uid))
            throw new InvalidOperationException("Invalid UID not reserved");

        int uidIndex = uid.Index;
        EnsureCapacity(uidIndex + 1);

        // Accomodate for the first time this slot is used.
        if (_indexToDense[uidIndex] < 0)
        {
            int denseIndex = Count;
            ObjectAddOrSet(instance);
            _indexToDense[uidIndex] = denseIndex;
            _denseToIndex[denseIndex] = uidIndex;
        }
        else ObjectAddOrSet(instance, _indexToDense[uidIndex]);

        // Handle grouping.
        if (!_activeHandles.TryGetValue(handle, out var list))
        {
            list = new HashSet<PackableUid>();
            _activeHandles[handle] = list;
        }

        // Prevent duplicates.
        list.Add(uid);

        _idToHandles[uidIndex] = string.IsNullOrEmpty(handle) ? null : handle;
    }

    #region Modifiable Operative Methods

    /// Overridable way to clear internally-stored entries in this UidList.
    protected virtual void ObjectClear() => _denseEntries.Clear();

    /// Overridable way to count entries in this UidList.
    protected virtual int Count => _denseEntries.Count;

    /// Overridable way of returning an object found in this UidList. For specialized constructable objects.
    protected virtual T ObjectConstruct(int denseIndex) => _denseEntries[denseIndex];

    /// Overridable way of removing an object in the internally-stored list.
    public virtual void ObjectRemove(int denseIndex) => _denseEntries.RemoveAt(denseIndex);

    /// Overridable way of adding or setting an object in the internally-stored list.
    protected virtual void ObjectAddOrSet(T @object, int denseIndex = -1)
    {
        switch (denseIndex)
        {
            case -1:
                _denseEntries.Add(@object);
                break;
            default:
                _denseEntries[denseIndex] = @object;
                break;
        }
    }

    public virtual void Update(GameTime gameTime)
    {
    }

    #endregion

    /// Attempt to replace an existing uid object instance.
    /// <remarks>This does NOT accomodate objects that have uids of their own.</remarks>
    public void Replace(T instance, PackableUid uid)
    {
        // Objects with internal Uids are a nightmare. You can't easily replace them. One must instead delete, then
        //  re-add them, or simply replace their internal parts without messing with the thing as a whole.
        if (instance is InternalUidObject)
            throw new InvalidOperationException(
                $"Attempted to call {nameof(UidList<T>)} replace using an object that contains an internal Uid!");

        ArgumentNullException.ThrowIfNull(instance);
        if (!IsAlive(uid))
            throw new InvalidOperationException("Attempted to replace UID that was not in active use!");

        int denseIndex = _indexToDense[uid.Index];
        ObjectAddOrSet(instance, denseIndex);
    }

    public void Destroy(PackableUid uid)
    {
        int uidIndex = uid.Index;
        if (!IsReserved(uid)) return;

        int denseIndex = _indexToDense[uidIndex];
        if (denseIndex < 0) return;

        // Remove from handle group.
        var handle = _idToHandles[uidIndex];
        if (!string.IsNullOrEmpty(handle) && _activeHandles.TryGetValue(handle, out var list))
        {
            list.Remove(uid);
            if (list.Count == 0) _activeHandles.Remove(handle);
        }

        int lastDenseIndex = Count - 1;

        if (denseIndex != lastDenseIndex)
        {
            // Swap with last element
            T lastObject = ObjectConstruct(lastDenseIndex);
            ObjectAddOrSet(lastObject, denseIndex);

            int movedUidIndex = _denseToIndex[lastDenseIndex];

            // Update mappings for the moved object
            _indexToDense[movedUidIndex] = denseIndex;
            _denseToIndex[denseIndex] = movedUidIndex;
        }

        // Invalidate the destroyed UID slot.
        ObjectRemove(lastDenseIndex);
        _denseToIndex[lastDenseIndex] = -1;
        _indexToDense[uidIndex] = -1;
        _generations[uidIndex]++;
        _idToHandles[uidIndex] = null;

        // Recycle UID index.
        if (_freeCount >= _freeList.Length)
            Array.Resize(ref _freeList, Max(32, _freeList.Length * 2));

        _freeList[_freeCount++] = uidIndex;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Ensures that internal storage fields have the capacity to hold the objects and their IDs.
    /// </summary>
    /// <param name="mandatory">Capacity required to accomodate.</param>
    /// <remarks>_denseToIndex is only valid up to current dense count.</remarks>
    private void EnsureCapacity(int mandatory)
    {
        if (mandatory <= _indexToDense.Length) return;

        int oldSize = _indexToDense.Length;
        int newSize = Max(mandatory, Max(64, oldSize == 0 ? 64 : oldSize * 2));

        Array.Resize(ref _indexToDense, newSize);
        Array.Resize(ref _denseToIndex, newSize);
        Array.Resize(ref _generations, newSize);
        Array.Resize(ref _idToHandles, newSize);

        // Initialize only the new slots
        for (int i = oldSize; i < newSize; i++)
        {
            _indexToDense[i] = -1;
            _denseToIndex[i] = -1;
            _idToHandles[i] = null;
            _generations[i] = 0; // Start at generation 0.
        }
    }

    /// <returns>True if Uid is valid; false if not.</returns>
    private bool IsReserved(PackableUid uid)
    {
        int index = uid.Index;
        return index >= 0 && index < _generations.Length && _generations[index] == uid.Generation;
    }

    private bool IsAlive(PackableUid uid)
    {
        int index = uid.Index;
        return _generations[index] > 0 && _indexToDense[index] >= 0;
    }

    /// Enumerator that only works when the UidList data structure for Dense Entries is not changed.
    public IEnumerator<T> GetEnumerator() => _denseEntries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}