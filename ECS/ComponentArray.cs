using System.Runtime.CompilerServices;
// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

/// <summary>
/// Interface for indexable component array that stores Component IDs.
/// </summary>
public interface IterArray
{
    object? this[int index] { get; }
    public int Increment();
    object GetAt(int index); // Returns boxed component for generic AddComponent
    void RemoveAt(int index);
    ref T1 GetRefAt<T1>(int index) where T1 : ISKComponent;
    int Count { get; }
}

/// <summary>
/// Contains the component instances for each registered entity.
/// </summary>
/// <remarks>This list is instantiated. It gets pretty complicated, but is essentially used to store component type data.</remarks>
/// <typeparam name="T">Type of components being stored in this particular list.</typeparam>
public class IterArray<T> : IterArray where T : class, ISKComponent
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
    private T[] _items;

    public ref T1 GetRefAt<T1>(int index) where T1 : ISKComponent
    {
        if ((uint)index >= (uint)Count)
            throw new IndexOutOfRangeException(
                $"GetRefAt index #{index} out of range in ComponentArray<{typeof(T).Name}>.");

        // Enforce that the caller is requesting the correct type for this array
        if (typeof(T1) != typeof(T))
            throw new InvalidCastException(
                $"Cannot get component of type {typeof(T1).Name} from ComponentArray<{typeof(T).Name}>. " +
                "Types must match exactly.");

        // This is the only way to safely return ref T1 when T1 == T
        return ref Unsafe.As<T, T1>(ref _items[index]);
    }

    /// <summary>
    /// Number of entries present within the component array.
    /// </summary>
    public int Count { get; private set; } = 0;

    /// <summary>
    /// Expands list of available items.
    /// </summary>
    /// <returns>References <see cref="_items"/> slot.</returns>
    public int Increment()
    {
        // Double item space every time it's over max.
        if (Count >= _items.Length)
            Array.Resize(ref _items, _items.Length * 2);
        return ++Count;
    }

    /// <summary>
    /// Removes component by setting it to default (nulls out value).
    /// Index remains valid but component is considered "removed".
    /// You MUST check IsValid(index) before using GetAt().
    /// </summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Length)
            throw new IndexOutOfRangeException($"Index {index} out of bounds (array size: {_items.Length})");

        // WARN: Possible crash here.
        _items[index] = default;
    }

    /// <param name="index">Index of desired registered type.</param>
    /// <returns>Type definition at index.</returns>
    /// <exception cref="IndexOutOfRangeException">If (<see cref="Count"/> &gt; index &lt; 0 )</exception>
    public object GetAt(int index)
    {
        if (index < 0 || index >= Count)
            throw new IndexOutOfRangeException($"GetAt index #{index} out of range.");
        return _items[index];
    }

    public ref T this[int index] => ref _items[index];
    object IterArray.this[int index] => _items[index];
}