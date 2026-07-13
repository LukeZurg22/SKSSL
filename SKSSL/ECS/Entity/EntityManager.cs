using System;
using System.Collections.Generic;
using System.Linq;
using SKSSL.ECS.Registry;
using SKSSL.Extensions;

namespace SKSSL.ECS;

/// <summary>
/// Instantiated Manager for all <see cref="Entity"/> instances contained within it. Fundamental ECS
/// infrastructure that currently softly requires a world to be contained-in.
/// </summary>
public partial class EntityManager
{
    /// Registry of all component instances and definitions per entity manager per world.
    public readonly ComponentRegistry ComponentRegistry = new();

    // Struct of Arrays Layout for all -Active- Entities contained in UidList.
    public readonly UidList<Entity> EntitiesList = [];

    /// <inheritdoc cref="EntityManager"/>
    public EntityManager()
    {
    }

    /// <summary>
    /// Get-Method for all Entities of desired type. Does not handle components.
    /// </summary>
    /// <typeparam name="T">
    /// Type of entities queried. <see cref="Entity"/> will return all entities, as
    /// all entities inherit that type.
    /// </typeparam>
    /// <returns>Readonly enumerable list of entities that inherit from type T</returns>
    // ReSharper disable once UnusedMember.Global
    public IEnumerable<Entity> GetAllEntities<T>() where T : Entity => EntitiesList.Entries.OfType<T>();

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
        EntityUid uid = EntityUid.FromPackableUid(EntitiesList.New());

        // Create entity copy.
        Entity entity = new Entity(uid).CopyFrom(source);
        
        // Add to "All Entities" list.
        EntitiesList.Set(entity, handle: source.Handle);
        
        // Register component indices for this ID.
        ComponentRegistry.PrepareEntityComponentStorage(uid);

        // Add default components if provided.
        if (entity.YamlComponents != null)
            foreach (ComponentYaml yamlComponent in entity.YamlComponents)
                if (ComponentRegistry.TryGetComponentType(yamlComponent.Type, out Type componentType))
                    entity.AddComponent(componentType);

        entity.Initialize();
        return entity;
    }

    public bool TryGet(EntityUid uid, out Entity entity)
    {
        entity = null!;
        if (EntitiesList.TryGet(uid, out entity!))
        {
            return true;
        }

        return false;
    }

    public void Destroy(EntityUid uid)
    {
        // Wipe UID's entry in comp storage. UID presence still means reusable.
        ComponentRegistry.PrepareEntityComponentStorage(uid);
        EntitiesList.Destroy(uid);
    }

    public void DestroyAll()
    {
        foreach (var entry in EntitiesList)
        {
            // Asserting that entities present in the super-list all have valid Uids. This assumption is as dangerous
            //  is it is necessary to avoid some tedious workarounds. -Z
            Destroy((EntityUid)entry.Uid);
        }
    }
}