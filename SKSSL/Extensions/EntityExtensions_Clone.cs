using System.Diagnostics.Contracts;
using SKSSL.ECS;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Extensions;

/// <summary>
/// Extends the functionality of records and <see cref="Entity"/> objects with Cloning methods.
/// </summary>
public static partial class EntityExtensions
{
    private static EntityManager Manager(this Entity entity) => entity.Context.EntityManager;

    /// <summary>
    /// Creates a shallow clone of the given entity without any type casting.
    /// </summary>
    /// <param name="original">The existing record instance to clone.</param>
    /// <returns>A new instance with all properties copied, or null if type cast T wasn't successful.</returns>
    [Pure]
    public static Entity? Clone(this Entity original) => original.Manager().Clone(original);

    /// <summary>
    /// Creates a shallow clone of the given entity, and type-casts it.
    /// Calls <see cref="Clone(SKSSL.ECS.Entity)"/> for entity clone that's type-casted.
    /// </summary>
    /// <typeparam name="T">Public record type this object is cast to.</typeparam>
    /// <param name="original">The existing record instance to clone.</param>
    /// <returns>A new instance with all properties copied, or null if type cast T wasn't successful.</returns>
    [Pure]
    public static T? CloneEntityAs<T>(this Entity original) where T : Entity => Clone(original) as T;
}