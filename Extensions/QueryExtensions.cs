using SKSSL.Scenes;

namespace SKSSL.Registry;

/// <summary>
/// Extensions to <see cref="BaseScene"/> that allow one to get entities within a scene definition.
/// </summary>
public static class QueryExtensions
{
    /// <summary>
    /// Get all entities with a single component type.
    /// </summary>
    public static IEnumerable<SKEntity> With<T>(this BaseWorld world) where T : struct, ISKComponent
    {
        int typeId = ComponentRegistry.GetComponentTypeId<T>();

        foreach (SKEntity entity in world._entityManager.AllEntities)
        {
            if (entity.ComponentIndices[typeId] != -1)
                yield return entity;
        }
    }

    /// <summary>
    /// Get all entities with two component types
    /// </summary>
    public static IEnumerable<SKEntity> With<T1, T2>(this BaseWorld world)
        where T1 : struct, ISKComponent
        where T2 : struct, ISKComponent
    {
        int id1 = ComponentRegistry.GetComponentTypeId<T1>();
        int id2 = ComponentRegistry.GetComponentTypeId<T2>();

        foreach (SKEntity entity in world._entityManager.AllEntities)
        {
            if (entity.ComponentIndices[id1] != -1 &&
                entity.ComponentIndices[id2] != -1)
                yield return entity;
        }
    }

    /// <summary>
    /// Get all entities with three component types.
    /// </summary>
    public static IEnumerable<SKEntity> With<T1, T2, T3>(this BaseWorld world)
        where T1 : struct, ISKComponent
        where T2 : struct, ISKComponent
        where T3 : struct, ISKComponent
    {
        int id1 = ComponentRegistry.GetComponentTypeId<T1>();
        int id2 = ComponentRegistry.GetComponentTypeId<T2>();
        int id3 = ComponentRegistry.GetComponentTypeId<T3>();

        foreach (SKEntity entity in world._entityManager.AllEntities)
        {
            if (entity.ComponentIndices[id1] != -1 &&
                entity.ComponentIndices[id2] != -1 &&
                entity.ComponentIndices[id3] != -1)
                yield return entity;
        }
    }

    // Add more overloads if needed (up to 5-6 is common)
}