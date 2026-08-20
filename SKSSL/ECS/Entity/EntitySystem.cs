using System;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Scenes;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedTypeParameter
// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable PublicConstructorInAbstractClass

namespace SKSSL.ECS;

/// <summary>
/// Base class for entity system registration. Append <see cref="RegisterSystemAttribute"/> for game to auto-register
/// this system on Initialize.
/// </summary>
public abstract class EntitySystem
{
    #region Shortcut Functions

    /// <summary>
    /// Quick Context route to SSLGame.ECS(); This is the game's current world's context, rather than per-entity.
    /// </summary>
    /// <remarks>
    /// Will cause a crash if ECS isn't enabled, and Entity Systems are in use.
    /// </remarks>
    public static EntityContext GameContext => new();

    /// <summary>
    /// Quick route to SSLGame.ECS().EntityManager.
    /// </summary>
    protected internal static EntityManager EntityManager => GameContext.EntityManager;

    /// <summary>
    /// Quick route to SSLGame.ECS().World for extension methods.
    /// </summary>
    public static World World => GameContext.World;

    /// <summary>
    /// Quick route to SSLGame.ECS().World.Events
    /// </summary>
    public static EventHandler Events => GameContext.World.Events;

    /// <summary>
    /// Quick route to SSLGame.ECS().World.Graphics handling.
    /// </summary>
    public static GraphicsDevice Graphics => GameContext.World.Graphics.GraphicsDevice;


    #endregion

    /// <summary>
    /// Public constructor so the system stops complaining.
    /// </summary>
    public EntitySystem()
    {
    }

    /// <summary>
    /// Overridable initialize method that allows local events to be subscript to the Event Handler.
    /// </summary>
    public virtual void Initialize()
    {
    }

    protected static void SubscribeEvent<E>(Action<EntityUid, E> handler) where E : struct, IEntityEvent
    {
        Events.Subscribe(handler);
    }

    protected static void RaiseEvent<E>(EntityUid uid, E @event) where E : struct, IEntityEvent =>
        Events.Raise(uid, @event);
}