using System;
using System.Diagnostics.Contracts;
using System.Reflection;
using SKSSL.ECS;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Extensions;

/// <summary>
/// Extends the functionality of records and <see cref="Entity"/> objects with Cloning methods.
/// </summary>
public static partial class EntityExtensions
{
    [Pure]
    public static T Clone<T>(this T val) where T : struct => val;

    /// <summary>
    /// Creates a shallow clone of the given record instance.
    /// </summary>
    /// <param name="original">The existing record instance to clone.</param>
    /// <returns>A new instance with all properties copied, or null if type cast T wasn't successful.</returns>
    [Pure]
    public static object? Clone(object original)
    {
        Type type = original.GetType();
        var clone = Activator.CreateInstance(type);

        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (prop.CanRead && prop.CanWrite)
                prop.SetValue(clone, prop.GetValue(original));

        // I know this acts like MemberwiseClone(), however it stays anyway because i'm just that unreasonable.

        return clone;
    }

    /// <summary>
    /// Creates a shallow clone of the given entity without any type casting.
    /// </summary>
    /// <param name="original">The existing record instance to clone.</param>
    /// <returns>A new instance with all properties copied, or null if type cast T wasn't successful.</returns>
    [Pure]
    public static Entity Clone(this Entity original)
    {
        Type type = original.GetType();

        // TODO: Replace this with something more effective?

        if (Activator.CreateInstance(type) is not Entity clone)
            throw new InvalidCastException($"Type-cast failed to create Entity in {nameof(Clone)}");

        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (prop.CanRead && prop.CanWrite)
                prop.SetValue(clone, prop.GetValue(original));

        return clone;
    }

    /// <summary>
    /// Creates a shallow clone of the given entity, and type-casts it.
    /// Calls <see cref="Clone(SKSSL.ECS.Entity)"/> for entity clone that's type-casted.
    /// </summary>
    /// <typeparam name="T">Public record type this object is cast to.</typeparam>
    /// <param name="original">The existing record instance to clone.</param>
    /// <returns>A new instance with all properties copied, or null if type cast T wasn't successful.</returns>
    [Pure]
    public static T CloneEntityAs<T>(this Entity original) where T : Entity => (T)Clone(original);
}