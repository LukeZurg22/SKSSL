using SKSSL.ECS;
using SKSSL.Scenes;

// ReSharper disable InvalidXmlDocComment
// ReSharper disable UnusedMember.Global

namespace SKSSL.Extensions;

/// <summary>
/// Query Extensions for SKSSL ECS. Queries entities based on Component contents.
/// </summary>
public static partial class EntityExtensions
{
    /*
     * There may look to be a lot of duplications. There technically are. Half of these are not intended to be called
     * directly, but instead as extensions to worlds.
     */

    #region QueryEntitiesWith (From World)

    /// <summary>
    /// Yields all entities containing component types.
    /// </summary>
    /// <param name="world">Reflected world instance to query.</param>
    /// <typeparam name="T1">Component to search.</typeparam>
    /// <returns>All entities containing provided component type.</returns>
    public static IEnumerable<SKEntity> QueryEntitiesWith<T1>(this BaseWorld world)
        => QueryEntitiesWith(world, typeof(T1));

    ///<inheritdoc cref="QueryEntitiesWith{T1}"/>
    /// <typeparam name="T2">Another component to search.</typeparam>
    public static IEnumerable<SKEntity> QueryEntitiesWith<T1, T2>(this BaseWorld world)
        => QueryEntitiesWith(world, typeof(T1), typeof(T2));

    ///<inheritdoc cref="QueryEntitiesWith{T1,T2}"/>
    /// <typeparam name="T3">Yet another component to search.</typeparam>
    public static IEnumerable<SKEntity> QueryEntitiesWith<T1, T2, T3>(this BaseWorld world)
        => QueryEntitiesWith(world, typeof(T1), typeof(T2), typeof(T3));

    ///<inheritdoc cref="QueryEntitiesWith{T1,T2,T3}"/>
    /// <typeparam name="T4">A fourth component to also search for.</typeparam>
    public static IEnumerable<SKEntity> QueryEntitiesWith<T1, T2, T3, T4>(this BaseWorld world)
        => QueryEntitiesWith(world, typeof(T1), typeof(T2), typeof(T3), typeof(T4));

    /// Core implementation (supports any number of components)
    /// Get all entities that have all of the specified component types.
    public static IEnumerable<SKEntity> QueryEntitiesWith(this BaseWorld world, params Type[] componentTypes)
    {
        foreach (SKEntity entity in world.ECS.EntityManager.AllEntities)
            if (componentTypes.All(type => entity.HasComponent(type)))
                yield return entity;
    }

    #endregion

    #region QueryEntitiesComponents

    /// <summary>
    /// Yields all entities containing component types along side the components queried into a Tuple.
    /// </summary>
    /// <param name="world">Reflected world instance to query.</param>
    /// <typeparam name="T1">Component to search.</typeparam>
    /// <returns>All entities that contain provided component type, and those entities in a Tuple.</returns>
    public static IEnumerable<(SKEntity entity, T1 comp1)> QueryEntitiesComponents<T1>(this BaseWorld world)
        where T1 : ISKComponent
    {
        var entities = new EntityContext(world).ActiveEntities;
        foreach (SKEntity entity in entities)
            if (entity.TryGetComponent(out T1? comp1) && comp1 != null)
                yield return (entity, comp1);
    }

    ///<inheritdoc cref="QueryEntitiesComponents{T1}"/>
    /// <typeparam name="T2">Another component to search.</typeparam>
    public static IEnumerable<(SKEntity entity, T1 comp1, T2 comp2)> QueryEntitiesComponents<T1, T2>(
        this BaseWorld world)
        where T1 : ISKComponent where T2 : ISKComponent
    {
        var entities = new EntityContext(world).ActiveEntities;
        foreach (SKEntity entity in entities)
            if (entity.TryGetComponent(out T1? comp1) &&
                entity.TryGetComponent(out T2? comp2) &&
                comp1 != null && comp2 != null)
                yield return (entity, comp1, comp2);
    }

    ///<inheritdoc cref="QueryEntitiesComponents{T1,T2}"/>
    /// <typeparam name="T3">Yet one more component to search.</typeparam>
    public static IEnumerable<(SKEntity entity, T1 c1, T2 c2, T3 c3)> QueryEntitiesComponents<T1, T2, T3>(
        this BaseWorld world)
        where T1 : ISKComponent where T2 : ISKComponent where T3 : ISKComponent
    {
        var entities = new EntityContext(world).ActiveEntities;
        foreach (SKEntity entity in entities)
            if (entity.TryGetComponent(out T1? c1) &&
                entity.TryGetComponent(out T2? c2) &&
                entity.TryGetComponent(out T3? c3) &&
                c1 != null && c2 != null && c3 != null)
                yield return (entity, c1, c2, c3);
    }

    #endregion

    #region Query Components

    /// <summary>
    /// Yields all active instances of this component type.
    /// </summary>
    /// <typeparam name="T1">Component to search.</typeparam>
    /// <returns>An enumerable of components.</returns>
    public static IEnumerable<T1> QueryComponents<T1>(this BaseWorld world)
        where T1 : ISKComponent
    {
        foreach (SKEntity entity in world.ECS.EntityManager.AllEntities)
            if (entity.TryGetComponent(out T1? comp1) && comp1 != null)
                yield return comp1;
    }

    /// <summary>
    /// Yields all active instances of components that are paired inside an entity.
    /// </summary>
    /// <typeparam name="T1">Component to search.</typeparam>
    /// <typeparam name="T2">Other component to search.</typeparam>
    /// <returns>An enumerable of component tuple pairs.</returns>
    public static IEnumerable<(T1, T2)> QueryComponents<T1, T2>(this BaseWorld world)
        where T1 : ISKComponent
        where T2 : ISKComponent
    {
        foreach (SKEntity entity in world.ECS.EntityManager.AllEntities)
            if (entity.TryGetComponent(out T1? comp1) &&
                entity.TryGetComponent(out T2? comp2) &&
                comp1 != null && comp2 != null)
                yield return (comp1, comp2);
    }

    /// <summary>
    /// Yields all active instances of components that are paired inside an entity.
    /// </summary>
    /// <typeparam name="T1">Component to search.</typeparam>
    /// <typeparam name="T2">Other component to search.</typeparam>
    /// <typeparam name="T2">Yet another component to search.</typeparam>
    /// <returns>An enumerable of component tuple pairs.</returns>
    public static IEnumerable<(T1, T2, T3)> QueryComponents<T1, T2, T3>(this BaseWorld world)
        where T1 : ISKComponent
        where T2 : ISKComponent
        where T3 : ISKComponent
    {
        foreach (SKEntity entity in world.ECS.EntityManager.AllEntities)
            if (entity.TryGetComponent(out T1? comp1) &&
                entity.TryGetComponent(out T2? comp2) &&
                entity.TryGetComponent(out T3? comp3) &&
                comp1 != null && comp2 != null && comp3 != null)
                yield return (comp1, comp2, comp3);
    }

    /// <summary>
    /// Yields all active instances of components that are paired inside an entity.
    /// </summary>
    /// <typeparam name="T1">Component to search.</typeparam>
    /// <typeparam name="T2">Other component to search.</typeparam>
    /// <typeparam name="T2">Yet another component to search.</typeparam>
    /// <returns>An enumerable of component tuple pairs.</returns>
    public static IEnumerable<ISKComponent> QueryComponents(this BaseWorld world, params Type[] componentTypes)
    {
        foreach (SKEntity entity in world.ECS.EntityManager.AllEntities)
        {
            if (!componentTypes.All(d => entity.HasComponent(d))) continue;
            // Does entity have all components provided?
            foreach (Type type in componentTypes)
            {
                ISKComponent? comp = entity.GetComponent(type);
                if (comp == null)
                    yield break;
                yield return comp;
            }
        }
    }

    #endregion
}