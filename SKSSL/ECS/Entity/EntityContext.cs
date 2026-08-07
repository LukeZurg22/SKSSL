using System;
using System.Collections.Generic;
using System.Linq;
using SKSSL.Scenes;
// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

/// <summary>
/// Intermediate struct designed to provide interface-like methods to call instanced <see cref="EntityManager"/> methods
/// likewise in addition to other systems.
/// </summary>
public readonly struct EntityContext
{
    /// <inheritdoc cref="SKSSL.ECS.EntityManager"/>
    public readonly EntityManager EntityManager = null!;

    /// <inheritdoc cref="Scenes.World"/>
    public readonly World World = null!;
    
    /// <summary>
    /// Blank constructor that calls method in <see cref="SSLGame"/> to statically get ECS instance.
    /// </summary>
    /// <exception cref="NullReferenceException">Thrown when ECS acquired is null.</exception>
    public EntityContext()
    {
        EntityContext ecs = SSLGame.Instance.SceneManager.ECS();
        World = ecs.World;
    }

    /// Wrapper for a <see cref="World"/>.
    public EntityContext(World world)
    {
        World = world;
        EntityManager = World.EntityManager;
    }
    
    /// Create context surrounding an entity.
    public EntityContext(Entity entity)
    {
        World = entity.ParentWorld;
        EntityManager = entity.SourceManager;
    }

    /*
     * Below are Proxy-Methods, that is to say functions designed to be called remotely that which call internal
     * methods inside the Entity Manager, and Component Registry.
     *
     * Documentation is inherited from the functions-called.
     */

    /// <inheritdoc cref="ECS.EntityManager.EntitiesList"/>
    public List<Entity> ActiveEntities => EntityManager.EntitiesList.Entries.ToList();

    /// <seealso cref="EntityManager"/>
    public Entity? SpawnEntity(string handle)
    {
        Entity? spawnedEntity = null;
        try
        {
            spawnedEntity = EntityManager.Spawn(handle);
        }
        catch (Exception e)
        {
            Log($"{nameof(EntityContext)}.{nameof(SpawnEntity)} call failed to spawn {handle}: {e.Message}");
        }

        return spawnedEntity;
    }
}