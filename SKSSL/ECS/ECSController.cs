using Microsoft.Xna.Framework;
using SKSSL.Scenes;

// ReSharper disable UnusedMember.Local

namespace SKSSL.ECS;

/// Primary Entity-Component system controller present in any given world. Contains static <see cref="EntityContext"/> 
public class ECSController
{
    /// Registry of all component instances and definitions.
    public readonly ComponentRegistry ComponentRegistry;

    /// Manager of all active entities in this ECS instance.
    public readonly EntityManager EntityManager;

    /// <summary>
    /// Constructor instantiating an ECS controller unto a world as reference parent.
    /// </summary>
    public ECSController(IWorld world)
    {
        ComponentRegistry = new ComponentRegistry();
        EntityManager = new EntityManager(world);
    }

    /// Calls system manager update calls.
    public void Update(GameTime gameTime)
    {
    }

    /// Calls system manager draw calls.
    public void Draw(GameTime gameTime)
    {
    }

    /// Ensures that this world instance is safely deleted before being replaced.
    public void Destroy() => EntityManager.DestroyAll();
}