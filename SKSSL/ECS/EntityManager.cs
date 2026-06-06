using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using SKSSL.Extensions;
using SKSSL.Scenes;
using SKSSL.YAML;


namespace SKSSL.ECS;

/// <summary>
/// Instantiated Manager for all <see cref="Entity"/> instances contained within it. Fundamental ECS
/// infrastructure that currently softly requires a world to be contained-in.
/// </summary>
public partial class EntityManager
{
    private readonly List<Entity> _allEntities = [];
    private readonly IWorld _world;

    /// <inheritdoc cref="EntityManager"/>
    public EntityManager(IWorld world) => _world = world;

    /// Get all Active entities present in the game.
    internal IReadOnlyList<Entity> AllEntities => _allEntities;

    #region Get Methods

    /// <param name="handle">Full handle ID of entity definition.</param>
    /// <returns>Null or first entity found within active <see cref="AllEntities"/> list.</returns>
    /// <remarks>Acts like <see cref="GetEntity(int)"/>, but uses a string reference handle instead.</remarks>
    public Entity? GetEntity(string handle)
        => _allEntities.FirstOrDefault(e => e?.GetUniqueInternalRef() == handle, null);

    /// <param name="id">Numeric ID of requested entity.</param>
    /// <returns>Null or instance of entity with provided ID.</returns>
    /// <remarks>Requires the user to know the ID of the entity.</remarks>
    [Pure]
    public Entity? GetEntity(int id)
    {
        if (_allEntities.Any(e => e.Uid == id))
            return _allEntities[id];
        Log($"Attempted to retrieve nonexistent entity with ID {id}");
        return null;
    }

    /// <summary>
    /// Get-Method for all Entities of desired type. Does not handle component contents.
    /// </summary>
    /// <typeparam name="T">
    /// Type of entities queried. <see cref="Entity"/> will return all entities, as
    /// all entities inherit that type.
    /// </typeparam>
    /// <returns>Readonly enumerable list of entities that inherit from type T</returns>
    public IEnumerable<Entity> GetEntities<T>() where T : Entity => AllEntities.OfType<T>();

    /// <summary>
    /// TryGet wrapper for <see cref="GetEntity(string)"/>
    /// </summary>
    public bool TryGetEntity(string handle, [MaybeNullWhen(false)] out Entity entity)
    {
        entity = GetEntity(handle);
        return entity != null;
    }

    #endregion

    #region Spawn Entity

    /// <summary>
    /// Public generic of <see cref="Spawn(Entity)"/> that creates a new entity w. blank constructor.
    /// </summary>
    /// <typeparam name="T">Entity of type Entity</typeparam>
    /// <returns>Entity instance, which is considered active.</returns>
    public Entity Spawn<T>() where T : Entity, new() => Spawn(new T());

    /// <summary>
    /// Creates a copy of an entity instance in its parameter.
    /// </summary>
    /// <param name="type">Entity instance to be copied and finalized.</param>
    /// <returns>New Entity Instance.</returns>
    public Entity Spawn(Entity type)
    {
        // Create entity and hope and pray it's fine.
        Entity entity = CreateEntity(type);
        Finalize(ref entity);
        return entity;
    }

    /// <summary>
    /// Acquires an entity template using a provided reference id, and creates an entity instance using it.
    /// </summary>
    /// <param name="handle">Reference id to template stored in registry.</param>
    /// <returns>Spawned copy of entity from handle.</returns>
    public Entity? Spawn(string handle)
    {
        if (!GameECSMasterRegistry.TryGetPrototype(handle, out var definition))
        {
            Log($"Failed to get entity copy using {handle} handle. Try full handle instead.",
                LOG.SYSTEM_ERROR);
            return null;
        }

        // Assumes all definitions present here are entities. A bit ambiguous, it is.
        if (definition.GetType() != typeof(Entity))
        {
            Log($"Invalid Entity handle \'{handle}\'. Are you attempting to spawn some other non-entity prototype?",
                LOG.SYSTEM_ERROR);
            return null;
        }

        // TODO: Nullability fallbacks may be needed from here and "up the chain" of calls.
        // Create entity regardless of how it's stored.
        Entity entity = Spawn((definition as Entity)!);

        // Assign world to entity. Will cause some funk if the world is null.
        return entity;
    }

    #endregion

    #region CreateEntity

    /// <summary>
    /// Create entity using existing raw <see cref="Entity"/> definition. Assumes definition is valid.
    /// </summary>
    /// <returns>New cloned entity.</returns>
    private static Entity CreateEntity(Entity definition)
    {
        // Create a copy of this entity.
        if (definition.CloneEntityAs<Entity>() is not { } entity)
            throw new Exception("Attempted to create entity from definition, but the definition was not an Entity!");
        return entity;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Final steps to conduct against an entity before spawning / creating.
    /// </summary>
    private void Finalize(ref Entity entity)
    {
        // TODO: Last-preemptive registration if this entity's full handle is not present in the registry.

        // Assign world.
        entity.World = _world;

        // Add default components if provided.
        foreach (YamlComponent yamlComponent in entity.YamlComponents!)
        {
            if (ComponentRegistry.TryGetComponentType(yamlComponent.Type, out Type componentType))
            {
                entity.AddComponent(componentType);
            }
        }

        // Initialize the entity.
        entity.Initialize();

        _allEntities.Add(entity);
    }

    /// <summary>
    /// Remove all entities contained in Entity Manager.
    /// </summary>
    public void MassacreAllEntities()
    {
        // TODO: MIGHT require additional unloading? The list just clears references for the GC. Components these
        //  entities had aren't clear, and created IDs aren't reset back to start from 0.
        _allEntities.Clear();
    }

    #endregion

    /// <summary>
    /// Dynamic constructor factory — works with any depth of inheritance
    /// </summary>
    /// <param name="yaml"></param>
    /// <param name="components"></param>
    /// <returns></returns>
    public static Entity CreateFromYaml(Prototype yaml, Dictionary<Type, object> components)
    {
        if (Activator.CreateInstance(
                typeof(Entity),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                [yaml, components],
                null) is not Entity template)
        {
            throw new MissingMethodException(
                $"No suitable constructor found on {nameof(Entity)} " +
                $"for YAML type {yaml.GetType().Name}. " +
                "Ensure there is a protected/internal constructor accepting a compatible YAML type.");
        }

        return template;
    }
}