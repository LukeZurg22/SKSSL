using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace SKSSL.ECS;

/// <summary>
/// Interface for indexable array object that stores IDs.
/// </summary>
public interface IterArray
{
    object? this[int index] { get; }

    // ReSharper disable once UnusedMethodReturnValue.Global
    public void Set<T>(int index, T value);

    [Pure]
    object GetAt(int index);

    void RemoveAt(int index);

    // ReSharper disable once UnusedMemberInSuper.Global
    ref T1 GetRefAt<T1>(int index) where T1 : class;
    uint Count { get; }
}

/// <summary>
/// Contains the component instances for each registered entity.
/// </summary>
/// <remarks>This list is instantiated. It gets pretty complicated, but is essentially used to store component type data.</remarks>
/// <typeparam name="T">Type of components being stored in this particular list.</typeparam>
public class IterArray<T> : IterArray where T : class
{
    /// <summary>
    /// Constructor of Component Array that creates empty array on instantiation.
    /// </summary>
    /// <param name="capacity">Number of items maximum this array can contain.</param>
    public IterArray(int capacity) => _items = new T[capacity];

    /// Empty constructor that forces default capacity to 1024.
    public IterArray() : this(1024)
    {
    }

    /// Private list of contained items.
    protected T[] _items;

    [Pure]
    public ref T1 GetRefAt<T1>(int index) where T1 : class
    {
        if ((uint)index > Count)
            throw new IndexOutOfRangeException(
                $"GetRefAt index #{index} out of range in ComponentArray<{typeof(T).Name}>.");

        // Enforce that the caller is requesting the correct type for this array
        if (typeof(T1) != typeof(T))
            throw new InvalidCastException(
                $"Cannot get object of type {typeof(T1).Name} from IterArray<{typeof(T).Name}>. " +
                "Types must match exactly.");

        // This is the only way to safely return ref T1 when T1 == T
        return ref Unsafe.As<T, T1>(ref _items[index]);
    }

    /// <summary>
    /// Number of entries present within the component array.
    /// </summary>
    public uint Count { get; private set; } = 0;

    public void Set<T1>(int index, T1 value)
    {
        // If the new index is out of the items list's length, then auto-increase it.
        if (index > _items.Length)
            Count++;

        // Automatically expand if needed.
        if (Count >= _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
        }

        _items[index] = (value as T)!;
        // Casting here anyway.
    }

    /// <summary>
    /// Removes component by setting it to default (nulls out value).
    /// Index remains valid but component is considered "removed".
    /// You MUST check IsValid(index) before using GetAt().
    /// </summary>
    public void RemoveAt(int index)
    {
        if (IsOutOfRange(index))
            throw new IndexOutOfRangeException($"Index {index} out of bounds (array size: {_items.Length})");

        _items[index] = default!;
    }

    /// <param name="index">Index of desired registered type.</param>
    /// <returns>Type definition at index.</returns>
    /// <exception cref="IndexOutOfRangeException">If (<see cref="Count"/> &gt; index &lt; 0 )</exception>
    [Pure]
    public object GetAt(int index)
    {
        if (IsOutOfRange(index))
            throw new IndexOutOfRangeException($"GetAt index #{index} out of range.");
        return _items[index];
    }

    [Pure]
    private bool IsOutOfRange(int index) => index < 0 || index > Count;

    public ref T this[int index] => ref _items[index];
    object IterArray.this[int index] => _items[index];
}