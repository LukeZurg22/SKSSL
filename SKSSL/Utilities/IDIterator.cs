namespace SKSSL.Utilities;

/// <summary>
/// Convenient iterator.
/// </summary>
/// <param name="InitialId"></param>
/// <param name="Maximum"></param>
public class IDIterator(int InitialId = 0, int Maximum = -1)
{
    #region Operator Overloads

    /// Add two Iterator values together to assign new ID.
    public static implicit operator int(IDIterator iterator) => iterator.ID;

    /// Add integer value to an Iterator to produce a new one.
    public static implicit operator IDIterator(int id) => new(id);

    /// ++Operator against iterator dictates <see cref="Iterate"/> call.
    public static IDIterator operator ++(IDIterator iterator) => iterator.Iterate();

    #endregion

    /// Initialized integer ID value with initial ID from constructor.
    public int ID { get; private set; } = InitialId;

    /// Increments internal ID value by one, up to a maximum, if there is any defined.
    public int Iterate()
    {
        int nextId = ID++;
        return Maximum != -1 && ID > Maximum
            ? throw new IndexOutOfRangeException("Iterator exceeds maximum value on iterate call!")
            : nextId;
    }

    /// <inheritdoc cref="System.Object.ToString"/>
    public override string ToString() => ID.ToString();
}