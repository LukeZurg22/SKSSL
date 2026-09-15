using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Xna.Framework;
using SKSSL.ECS.Registry;
using SKSSL.Extensions;

namespace SKSSL.ECS;

/// <summary>
/// Instantiated Manager for all <see cref="Entity"/> instances contained within it. Fundamental ECS
/// infrastructure that currently softly requires a world to be contained-in.
/// </summary>
public partial class EntityManager
{
    /// Registry of entity definitions.
    private readonly IReadOnlyRegistry<Entity> _entityRegistry =
        MasterRegistryManager.GetRegistry<Entity, EntityRegistry>().AsReadOnly();

    /// Registry of all component instances and definitions per entity manager per world.
    public readonly ComponentRegistry ComponentRegistry;

    // All -Active- Entities contained in UidList.
    public readonly UidList<Entity> EntitiesList = [];

    /// Entities that need updating over a network.
    public readonly List<EntityUid> DirtyEntities = [];

    /// <inheritdoc cref="EntityManager"/>
    public EntityManager() => ComponentRegistry = new ComponentRegistry(this);

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
        if (!_entityRegistry.TryGet(handle, out Entity? definition))
        {
            Log($"Failed to get entity copy using {handle} handle.", LOG.SYSTEM_ERROR);
            return null;
        }

        // Assumes all definitions present here are entities. A bit ambiguous, it is.
        if (definition.Abstract)
        {
            Log($"Unable to spawn abstract Entity \'{handle}\'.", LOG.SYSTEM_ERROR);
            return null;
        }

        // Safety padding for handle.
        if (definition.Handle.IsNullOrEmpty())
        {
            Log(
                $"Entity definition \'{handle}\' exists, but it doesn't contain its handle within its Handle-field. " +
                $"This was corrected at-runtime.",
                LOG.SYSTEM_WARNING);
            definition.Handle = handle;
        }

        return Clone(definition);
    }

    /// <summary>
    /// Acquires an entity template using a provided reference id, and creates an entity instance using it.
    /// </summary>
    /// <param name="source">Entity template to copy from.</param>
    /// <returns>Spawned copy of entity from handle.</returns>
    /// <exception cref="Exception">Thrown if entity is abstract. Abstract entities should not be instantiated.</exception>
    public Entity? Clone(Entity source)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        if (source.Abstract)
        {
            Log(new EntityException($"Attempted to clone abstract entity {source.GetFullHandle()}"));
            return null;
        }

        var uid = EntitiesList.New().As<EntityUid>(); // Create unique ID.
        Entity entity = source.Clone(); // Create copy of source entity.
        entity.SetUid(uid);

        // Add to "All Entities" list.
        EntitiesList.Set(entity, uid.As<PackableUid>(), handle: source.Handle);

        // Register component indices for this ID.
        ComponentRegistry.PrepareEntityComponentStorage(uid);

        // Add default components if provided.
        if (entity.YamlComponents != null)
            foreach (ComponentYaml yamlComponent in entity.YamlComponents)
                if (ComponentRegistry.TryGetComponentType(yamlComponent.Type, out Type componentType))
                    entity.AddComponent(componentType);

        entity.Initialize();
        return entity;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public bool TryGet(EntityUid uid, [NotNullWhen(true)] out Entity? entity)
    {
        entity = null;
        return EntitiesList.TryGet(new PackableUid(uid), out entity);
    }

    public void Destroy(EntityUid uid)
    {
        // Wipe UID's entry in comp storage. UID presence still means reusable.
        ComponentRegistry.PrepareEntityComponentStorage(uid);
        EntitiesList.Destroy(new PackableUid(uid));
    }

    public void DestroyAll()
    {
        foreach (Entity entry in EntitiesList)
        {
            // Asserting that entities present in the super-list all have valid Uids. This assumption is as dangerous
            //  as it is necessary to avoid some tedious workarounds. -Z
            Destroy(entry.GetUid());
        }
    }

    /// Mark certain Entity as "Dirty"; in need of an update.
    public void DirtyEntity(EntityUid entity)
    {
        DirtyEntities.Add(entity);
    }

    public void CleanEntity(EntityUid entity)
    {
        DirtyEntities.Remove(entity);
    }

    public void Update(GameTime gameTime)
    {
        var dirtiedEntities = DirtyEntities.ToList();
        foreach (EntityUid entity in dirtiedEntities)
        {
            // Update the entity through a little update call. Only works if it has special update methods.
            EntitiesList.Get(new PackableUid(entity)).Update(gameTime);
            DirtyEntities.Remove(entity);
        }
    }
}