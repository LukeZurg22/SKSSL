using System;
using System.Threading;

namespace SKSSL.Utilities;

/// <summary>
/// Convenient iterator.
/// </summary>
/// <param name="InitialId"></param>
/// <param name="Maximum"></param>
public class IDIterator(uint InitialId = 0, uint Maximum = 0)
{
    #region Operator Overloads

    /// Add two Iterator values together to assign new ID.
    public static implicit operator uint(IDIterator iterator) => iterator.ID;

    /// Add uinteger value to an Iterator to produce a new one.
    public static implicit operator IDIterator(uint id) => new(id);

    /// ++Operator against iterator dictates <see cref="Iterate"/> call.
    public static IDIterator operator ++(IDIterator iterator) => iterator.Iterate();

    #endregion

    /// Initialized integer ID value with initial ID from constructor.
    public uint ID = InitialId;

    /// Increments internal ID value by one, up to a maximum, if there is any defined.
    public uint Iterate()
        => Maximum != 0 && ID + 1 > Maximum
            ? throw new IndexOutOfRangeException("Iterator exceeds maximum value on iterate call!")
            : Interlocked.Increment(ref ID);

    /// <inheritdoc cref="System.Object.ToString"/>
    public override string ToString() => ID.ToString();
}