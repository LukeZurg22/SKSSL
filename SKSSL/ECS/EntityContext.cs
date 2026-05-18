using SKSSL.Scenes;
using static SKSSL.DustLogger;

namespace SKSSL.ECS;

/// <summary>
/// Intermediate struct designed to provide interface-like methods to call instanced <see cref="EntityManager"/> methods
/// likewise in addition to other systems.
/// </summary>
public readonly struct EntityContext
{
    /// <inheritdoc cref="SKSSL.ECS.EntityManager"/>
    public readonly EntityManager EntityManager = null!;

    /// <inheritdoc cref="SKSSL.ECS.ComponentRegistry"/>
    public readonly ComponentRegistry Components = null!;

    /// <inheritdoc cref="BaseWorld"/>
    public readonly BaseWorld World = null!;
    
    public EntityContext(EntityManager entityManager, ComponentRegistry componentRegistry)
    {
        EntityManager = entityManager;
        Components = componentRegistry;
    }

    /// <summary>
    /// Blank constructor that calls method in <see cref="SSLGame"/> to statically get ECS instance.
    /// </summary>
    /// <exception cref="NullReferenceException">Thrown when ECS acquired is null.</exception>
    public EntityContext()
    {
        EntityContext ecs = SSLGame.ECS();
        World = ecs.World;
        EntityManager = ecs.EntityManager;
        Components = ecs.Components;
    }

    /// Wrapper Constructor for a <see cref="ECSController"/>.
    public EntityContext(BaseWorld world)
    {
        ECSController ecs = world.ECS;
        World = world;
        EntityManager = ecs.EntityManager;
        Components = ecs.ComponentRegistry;
    }

    /*
     * Below are Proxy-Methods, that is to say functions designed to be called remotely that which call internal
     * methods inside the Entity Manager, and Component Registry.
     *
     * Documentation is inherited from the functions-called.
     * \\TODO: Add more intermediate calls for further convenient interactions w. manager.
     *      All of the vital methods are still accessible, just from the "long way" and without the safety that an
     *      "upper-layer" try-catch would offer.
     */

    #region Proxy-Methods

    /// <inheritdoc cref="SKSSL.ECS.EntityManager.AllEntities"/>
    public List<Entity> ActiveEntities => EntityManager.AllEntities.ToList();

    /// <seealso cref="EntityManager"/>
    /// <seealso cref="EntityManager.Spawn"/>
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

    #endregion
}