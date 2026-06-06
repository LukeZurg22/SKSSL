using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Extensions;
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
    /// Quick Context route to SSLGame.ECS();
    /// </summary>
    /// <remarks>
    /// Will cause a crash if ECS isn't enabled, and Entity Systems are in use.
    /// </remarks>
    private static EntityContext Context => SSLGame.Instance.SceneManager.ECS();

    /// <summary>
    /// Quick route to SSLGame.ECS().EntityManager.
    /// </summary>
    protected static EntityManager EntityManager => Context.EntityManager;

    /// <summary>
    /// Quick route to SSLGame.ECS().World for extension methods.
    /// </summary>
    public static World World => Context.World;

    /// <summary>
    /// Get all active entities in the game.
    /// </summary>
    /// <remarks>WARNING! THIS IS VERY DANGEROUS!</remarks>
    public static List<Entity> ActiveEntities => Context.ActiveEntities;

    /// <summary>
    /// Quick route to SSLGame.ECS().World.Events
    /// </summary>
    public static EventHandler Events => Context.World.Events;

    /// <summary>
    /// Quick route to SSLGame.ECS().World.Graphics handling.
    /// </summary>
    public static GraphicsDevice Graphics => Context.World.Graphics.GraphicsDevice;

    /// <summary>
    /// Quick route to World.QueryComponents Query.
    /// </summary>
    public static IEnumerable<T> Query<T>() where T : Component => World.QueryComponents<T>();

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

    protected static void SubscribeEvent<E>(Action<EntityUid, E> handler) where E : EntityEvent =>
        Events.Subscribe(handler);

    protected static void RaiseEvent<E>(EntityUid uid, E @event) where E : EntityEvent =>
        Events.Raise(uid, @event);

    protected static void RaiseEvent<E>(Entity ent, E @event) where E : EntityEvent =>
        Events.Raise(ent.Uid, @event);
}