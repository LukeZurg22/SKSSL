namespace SKSSL.ECS;

/// <summary>
/// Interface for indexable component array that stores Component IDs.
/// </summary>
public interface IterArray
{
    object? this[int index] { get; }
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

    /// <summary>
    /// Number of entries present within the component array.
    /// </summary>
    public int Count { get; private set; } = 0;

    /// <summary>
    /// Expands list of available items.
    /// </summary>
    /// <returns>Reference to <see cref="_items"/> slot.</returns>
    public ref T Add()
    {
        // Double item space every time it's over max.
        if (Count >= _items.Length)
            Array.Resize(ref _items, _items.Length * 2);

        return ref _items[Count++];
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
    public ref T GetAt(int index)
    {
        if (index < 0 || index >= Count)
            throw new IndexOutOfRangeException($"{nameof(GetAt)} index #{index} out of range.");

        return ref _items[index];
    }

    public ref T this[int index] => ref _items[index];
    object IterArray.this[int index] => _items[index];
}