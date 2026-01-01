using System.Reflection;
using SKSSL.ECS;

namespace SKSSL.Extensions;

/// <summary>
/// Extends the functionality of records and <see cref="SKEntity"/> objects with Cloning methods.
/// </summary>
public static class RecordExtensions
{
    /// <summary>
    /// Creates a shallow clone of the given record instance.
    /// </summary>
    /// <param name="original">The existing record instance to clone.</param>
    /// <returns>A new instance with all properties copied, or null if type cast T wasn't successful.</returns>
    public static object? Clone(object original)
    {
        Type type = original.GetType();
        var clone = Activator.CreateInstance(type);

        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (prop.CanRead && prop.CanWrite)
                prop.SetValue(clone, prop.GetValue(original));

        return clone;
    }
    
    /// <summary>
    /// Creates a shallow clone of the given entity without any type casting.
    /// </summary>
    /// <param name="original">The existing record instance to clone.</param>
    /// <returns>A new instance with all properties copied, or null if type cast T wasn't successful.</returns>
    public static SKEntity CloneEntity(SKEntity original)
    {
        Type type = original.GetType();

        if (Activator.CreateInstance(type) is not SKEntity clone)
            throw new Exception($"Type-cast failed to create SKEntity in {nameof(CloneEntity)}");
        
        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (prop.CanRead && prop.CanWrite)
                prop.SetValue(clone, prop.GetValue(original));
        
        return clone;
    }
    
    /// <summary>
    /// Creates a shallow clone of the given entity, and type-casts it.
    /// Calls <see cref="CloneEntity"/> for entity clone that's type-casted.
    /// </summary>
    /// <typeparam name="T">Public record type this object is casted to.</typeparam>
    /// <param name="original">The existing record instance to clone.</param>
    /// <returns>A new instance with all properties copied, or null if type cast T wasn't successful.</returns>
    public static object? CloneEntityAs<T>(this SKEntity original) where T : SKEntity => CloneEntity(original) as T;
}