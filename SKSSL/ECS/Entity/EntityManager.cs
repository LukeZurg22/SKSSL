using System;
using System.Collections.Generic;
using System.Linq;
using SKSSL.ECS.Registry;
using SKSSL.Extensions;
using static SKSSL.SSLGame;

namespace SKSSL.ECS;

/// <summary>
/// Instantiated Manager for all <see cref="Entity"/> instances contained within it. Fundamental ECS
/// infrastructure that currently softly requires a world to be contained-in.
/// </summary>
public partial class EntityManager
{
    /// Registry of all component instances and definitions per entity manager per world.
    public readonly ComponentRegistry ComponentRegistry = new();

    // Struct of Arrays Layout for Entities.
    private Entity?[] _entities;
    private int[] _generations;
    private int[] _freeList = new int[Config.DESTROY_ENTITY_CACHE_LIMIT];
    private int _freeCount = 0;

    /// <inheritdoc cref="EntityManager"/>
    public EntityManager()
    {
    }

    /// Get all Active entities present in the game.
    internal IReadOnlyList<Entity> AllEntities => _entities;

    /// <summary>
    /// Get-Method for all Entities of desired type. Does not handle components.
    /// </summary>
    /// <typeparam name="T">
    /// Type of entities queried. <see cref="Entity"/> will return all entities, as
    /// all entities inherit that type.
    /// </typeparam>
    /// <returns>Readonly enumerable list of entities that inherit from type T</returns>
    // ReSharper disable once UnusedMember.Global
    public IEnumerable<Entity> GetAllEntities<T>() where T : Entity => AllEntities.OfType<T>();

    public Entity? Spawn(string handle)
    {
        if (!MasterRegistryManager.TryGetPrototype(handle, out Prototype definition))
        {
            Log($"Failed to get entity copy using {handle} handle. Try full handle instead.",
                LOG.SYSTEM_ERROR);
            return null;
        }

        // Assumes all definitions present here are entities. A bit ambiguous, it is.
        if (definition is not Entity source || source.Abstract)
        {
            Log($"Invalid Entity handle \'{handle}\'. Are you attempting to spawn some other non-entity prototype?",
                LOG.SYSTEM_ERROR);
            return null;
        }

        return Clone(source);
    }

    /// <summary>
    /// Acquires an entity template using a provided reference id, and creates an entity instance using it.
    /// </summary>
    /// <param name="source">Entity template to copy from.</param>
    /// <returns>Spawned copy of entity from handle.</returns>
    /// <exception cref="Exception">Thrown if entity is abstract. Abstract entities should not be instantiated.</exception>
    public Entity Clone(Entity source)
    {
        if (source.Abstract)
            throw new Exception($"Attempted to spawn abstract entity {source.GetFullHandle()}");

        // Create unique ID.
        EntityUid entityUid = CreateUID();
        
        // Create entity copy.
        Entity entity = new Entity(entityUid).CopyFrom(source);
        
        // Assign to "All Entities" list.
        _entities[entityUid.Index] = entity;

        // Register component indices for this ID.
        ComponentRegistry.PrepareEntityComponentStorage(entityUid);

        // Add default components if provided.
        if (entity.YamlComponents != null)
            foreach (ComponentYaml yamlComponent in entity.YamlComponents)
                if (ComponentRegistry.TryGetComponentType(yamlComponent.Type, out Type componentType))
                    entity.AddComponent(componentType);

        entity.Initialize();
        return entity;
    }

    /// <summary>
    /// Dangerous "Get" method to retrieve an entity stored in active entities list.
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    // ReSharper disable once UnusedMember.Global
    public Entity Get(EntityUid uid)
    {
        int index = (int)(uid.Value & 0xFFFF);
        int generation = (int)(uid.Value >> 16);

        if ((uint)index >= (uint)_entities.Length)
            throw new Exception("Invalid entity index");

        if (_generations[index] != generation)
            throw new Exception("Stale EntityUid (entity was destroyed or reused)");

        Entity? entity = _entities[index];

        if (entity is null)
            throw new Exception("Entity slot is empty");

        return entity;
    }

    /// <summary>
    /// Safer way to obtain an entity definition using its ID.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public bool TryGet(EntityUid uid, out Entity entity)
    {
        int index = (int)(uid.Value & 0xFFFF);
        int generation = (int)(uid.Value >> 16);

        if ((uint)index < (uint)_entities.Length
            && _generations[index] == generation
            && (entity = _entities[index]!) is not null)
            return true;

        entity = null!;
        return false;
    }

    /// Create unique ID for entity.
    private EntityUid CreateUID()
    {
        int index;

        if (_freeCount > 0)
        {
            // Reuse old indices.
            index = _freeList[--_freeCount];
        }
        else
        {
            index = _entities.Length;

            // Ensure free list has plenty of space.
            if (_freeCount >= _freeList.Length) Array.Resize(ref _freeList, _freeList.Length * 2);

            Array.Resize(ref _entities, index + 1);
            Array.Resize(ref _generations, index + 1);
        }

        int generation = _generations[index];
        return new EntityUid(index, generation);
    }

    public void Destroy(EntityUid uid)
    {
        int index = (int)(uid.Value & 0xFFFF);
        int generation = (int)(uid.Value >> 16);

        if ((uint)index >= (uint)_entities.Length)
            return;

        // Validate generation (prevents double-destroy bugs)
        if (_generations[index] != generation)
            return;

        if (_entities[index] is null)
            return;

        // Remove entry.
        _entities[index] = null;

        // Wipe UID's entry in comp storage. UID presence still means reusable.
        ComponentRegistry.PrepareEntityComponentStorage(uid);

        // Invalidate old IDs.
        _generations[index]++;

        // Ensure free list has plenty of space.
        if (_freeCount >= _freeList.Length) Array.Resize(ref _freeList, _freeList.Length * 2);

        // Add slot back to free list.
        _freeList[_freeCount++] = index;
    }

    public bool IsValid(EntityUid uid)
    {
        int index = uid.Index;

        if ((uint)index >= (uint)_entities.Length)
            return false;

        Entity? entity = _entities[index];
        if (entity is null)
            return false;

        return _generations[index] == uid.Generation;
    }

    /// <summary>
    /// Remove all entities contained in Entity Manager.
    /// </summary>
    public void DestroyAll()
    {
        Array.Clear(_entities);
        Array.Clear(_generations);
        _freeCount = 0;
        for (int i = 0; i < _entities.Length; i++)
        {
            _generations[i]++; // Invalidate all old ID's.
            _freeList[_freeCount++] = i;
        }
    }
}