using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using SKSSL.ECS;
using SKSSL.Scenes;

#pragma warning disable CS8631 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match constraint type.

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
    [Pure]
    public static IEnumerable<Entity> QueryEntitiesWith<T1>(this World world)
        => QueryEntitiesWith(world, typeof(T1));

    ///<inheritdoc cref="QueryEntitiesWith{T1}"/>
    /// <typeparam name="T2">Another component to search.</typeparam>
    [Pure]
    public static IEnumerable<Entity> QueryEntitiesWith<T1, T2>(this World world)
        => QueryEntitiesWith(world, typeof(T1), typeof(T2));

    ///<inheritdoc cref="QueryEntitiesWith{T1,T2}"/>
    /// <typeparam name="T3">Yet another component to search.</typeparam>
    [Pure]
    public static IEnumerable<Entity> QueryEntitiesWith<T1, T2, T3>(this World world)
        => QueryEntitiesWith(world, typeof(T1), typeof(T2), typeof(T3));

    ///<inheritdoc cref="QueryEntitiesWith{T1,T2,T3}"/>
    /// <typeparam name="T4">A fourth component to also search for.</typeparam>
    [Pure]
    public static IEnumerable<Entity> QueryEntitiesWith<T1, T2, T3, T4>(this World world)
        => QueryEntitiesWith(world, typeof(T1), typeof(T2), typeof(T3), typeof(T4));

    /// Core implementation (supports any number of components)
    /// Get all entities that have all of the specified component types.
    [Pure]
    public static IEnumerable<Entity> QueryEntitiesWith(this World world, params Type[] componentTypes)
    {
        foreach (Entity entity in world.EntityManager.EntitiesList)
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
    [Pure]
    public static IEnumerable<(Entity entity, T1 comp1)> QueryEntitiesComponents<T1>(this World world)
        where T1 : Component
    {
        var entities = new EntityContext(world).ActiveEntities;
        foreach (Entity entity in entities)
            if (entity.TryGetComponent(out T1? comp1) && comp1 != null)
                yield return (entity, comp1);
    }

    ///<inheritdoc cref="QueryEntitiesComponents{T1}"/>
    /// <typeparam name="T2">Another component to search.</typeparam>
    [Pure]
    public static IEnumerable<(Entity entity, T1 comp1, T2 comp2)> QueryEntitiesComponents<T1, T2>(
        this World world)
        where T1 : Component where T2 : Component
    {
        var entities = new EntityContext(world).ActiveEntities;
        foreach (Entity entity in entities)
            if (entity.TryGetComponent(out T1? comp1) &&
                entity.TryGetComponent(out T2? comp2) &&
                comp1 != null && comp2 != null)
                yield return (entity, comp1, comp2);
    }

    ///<inheritdoc cref="QueryEntitiesComponents{T1,T2}"/>
    /// <typeparam name="T3">Yet one more component to search.</typeparam>
    [Pure]
    public static IEnumerable<(Entity entity, T1 c1, T2 c2, T3 c3)> QueryEntitiesComponents<T1, T2, T3>(
        this World world)
        where T1 : Component where T2 : Component where T3 : Component
    {
        var entities = new EntityContext(world).ActiveEntities;
        foreach (Entity entity in entities)
            if (entity.TryGetComponent(out T1? c1) &&
                entity.TryGetComponent(out T2? c2) &&
                entity.TryGetComponent(out T3? c3) &&
                c1 != null && c2 != null && c3 != null)
                yield return (entity, c1, c2, c3);
    }

    #endregion

    #region Query Components

    /// <inheritdoc cref="QueryComponents{T1}"/>
    /// <remarks>
    /// This is a quick and dirty re-route to the World.QueryComponents() call.
    /// However this is mainly to Extends EntitySystem instances with a "Global World Context".
    /// </remarks>
    [Pure]
    public static IEnumerable<T1> Query<T1>(this EntitySystem _) where T1 : Component
        => QueryComponents<T1>(EntitySystem.World);

    /// <summary>
    /// Yields all active instances of this component type.
    /// </summary>
    /// <typeparam name="T1">Component to search.</typeparam>
    /// <returns>An enumerable of components.</returns>
    [Pure]
    public static IEnumerable<T1> QueryComponents<T1>(this World world)
        where T1 : Component
    {
        foreach (Entity entity in world.EntityManager.EntitiesList)
            if (entity.TryGetComponent(out T1? comp1) && comp1 != null)
                yield return comp1;
    }

    /// <summary>
    /// Yields all active instances of components that are paired inside an entity.
    /// </summary>
    /// <typeparam name="T1">Component to search.</typeparam>
    /// <typeparam name="T2">Other component to search.</typeparam>
    /// <returns>An enumerable of component tuple pairs.</returns>
    [Pure]
    public static IEnumerable<(T1, T2)> QueryComponents<T1, T2>(this World world)
        where T1 : Component
        where T2 : Component
    {
        foreach (Entity entity in world.EntityManager.EntitiesList)
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
    [Pure]
    public static IEnumerable<(T1, T2, T3)> QueryComponents<T1, T2, T3>(this World world)
        where T1 : Component
        where T2 : Component
        where T3 : Component
    {
        foreach (Entity entity in world.EntityManager.EntitiesList)
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
    [Pure]
    public static IEnumerable<Component> QueryComponents(this World world, params Type[] componentTypes)
    {
        foreach (Entity entity in world.EntityManager.EntitiesList)
        {
            if (!componentTypes.All(d => entity.HasComponent(d))) continue;
            // Does entity have all components provided?
            foreach (Type type in componentTypes)
            {
                Component? comp = entity.GetComponent(type);
                if (comp == null)
                    yield break;
                yield return comp;
            }
        }
    }

    #endregion
}