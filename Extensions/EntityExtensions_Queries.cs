using SKSSL.ECS;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Extensions;

/// <summary>
/// Query Extensions for SKSSL ECS. Queries entities based on Component contents.
/// </summary>
public static partial class EntityExtensions
{
    #region GetEntitiesWith

    public static IEnumerable<SKEntity> GetEntitiesWith<T1>()
        => GetEntitiesWith(typeof(T1));

    public static IEnumerable<SKEntity> GetEntitiesWith<T1, T2>()
        => GetEntitiesWith(typeof(T1), typeof(T2));

    public static IEnumerable<SKEntity> GetEntitiesWith<T1, T2, T3>()
        => GetEntitiesWith(typeof(T1), typeof(T2), typeof(T3));

    public static IEnumerable<SKEntity> GetEntitiesWith<T1, T2, T3, T4>()
        => GetEntitiesWith(typeof(T1), typeof(T2), typeof(T3), typeof(T4));

    /// Core implementation (supports any number of components)
    /// Get all entities that have all of the specified component types.
    public static IEnumerable<SKEntity> GetEntitiesWith(params Type[] componentTypes)
    {
        foreach (SKEntity entity in Entities)
            if (componentTypes.All(type => entity.HasComponent(type)))
                yield return entity;
    }

    #endregion

    #region GetEntitiesWithComponents

    // Returns entities + the requested components in one go
    public static IEnumerable<(SKEntity entity, T1 comp1)> GetEntitiesWithComponents<T1>()
        where T1 : class, ISKComponent
    {
        foreach (SKEntity entity in Entities)
            if (entity.TryGetComponent(out T1? comp1) && comp1 != null)
                yield return (entity, comp1);
    }

    public static IEnumerable<(SKEntity entity, T1 comp1, T2 comp2)> GetEntitiesWithComponents<T1, T2>()
        where T1 : class, ISKComponent where T2 : class, ISKComponent
    {
        foreach (SKEntity entity in Entities)
            if (entity.TryGetComponent(out T1? comp1) &&
                entity.TryGetComponent(out T2? comp2) &&
                comp1 != null && comp2 != null)
                yield return (entity, comp1, comp2);
    }

    public static IEnumerable<(SKEntity entity, T1 c1, T2 c2, T3 c3)> GetEntitiesWithComponents<T1, T2, T3>()
        where T1 : class, ISKComponent where T2 : class, ISKComponent where T3 : class, ISKComponent
    {
        foreach (SKEntity entity in Entities)
            if (entity.TryGetComponent(out T1? c1) &&
                entity.TryGetComponent(out T2? c2) &&
                entity.TryGetComponent(out T3? c3) &&
                c1 != null && c2 != null && c3 != null)
                yield return (entity, c1, c2, c3);
    }

    #endregion
}