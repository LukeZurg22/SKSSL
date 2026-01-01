// IMPL: Add query extensions for EntitySystem.

using SKSSL.ECS;
using SKSSL.Registry;
using SKSSL.Scenes;
using ComponentRegistry = SKSSL.ECS.ComponentRegistry;

public static class EntitySystemQueryExtensions
{
//    public static Query<T1> Query<T1>(this EntitySystem system)
//        where T1 : struct, ISKComponent
//        => new Query<T1>(system.World);
//
//    public static Query<T1, T2> Query<T1, T2>(this EntitySystem system)
//        where T1 : struct, ISKComponent
//        where T2 : struct, ISKComponent
//        => new Query<T1, T2>(system.World);
//
//    public static Query<T1, T2, T3> Query<T1, T2, T3>(this EntitySystem system)
//        where T1 : struct, ISKComponent
//        where T2 : struct, ISKComponent
//        where T3 : struct, ISKComponent
//        => new Query<T1, T2, T3>(system.World);

    /// <summary>
    /// Get all entities with a single component type.
    /// </summary>
    public static IEnumerable<SKEntity> Query<T>(this BaseWorld world) where T : struct, ISKComponent
    {
        int typeId = ComponentRegistry.GetComponentTypeId<T>();

        foreach (SKEntity entity in world._entityManager.AllEntities)
        {
            if (entity.ComponentIndices[typeId] != -1)
                yield return entity; // TODO: Make it return Entity and Component(?)
        }
    }

    /// <summary>
    /// Get all entities with two component types
    /// </summary>
    public static IEnumerable<SKEntity> Query<T1, T2>(this BaseWorld world)
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
    public static IEnumerable<SKEntity> Query<T1, T2, T3>(this BaseWorld world)
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


}