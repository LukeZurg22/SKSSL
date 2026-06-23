using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using SKSSL.ECS;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.Extensions;

/// Extensions to the ECS that allow direct interaction with its parts without making tedious manual Context-Request calls.
public static partial class EntityExtensions
{
    #region Get Components

    /// <inheritdoc cref="ComponentRegistry.GetComponent"/> (Generic Variant)
    [Pure]
    public static ref T GetComponent<T>(this Entity entity) where T : Component
        => ref ComponentRegistry.GetComponent<T>(entity);

    /// <inheritdoc cref="ComponentRegistry.GetComponent"/>
    [Pure]
    public static Component? GetComponent(this Entity entity, Type componentType)
        => ComponentRegistry.GetComponent(entity, componentType);

    /// <inheritdoc cref="ComponentRegistry.TryGetComponent"/> (Generic Variant)
    [Pure]
    public static bool TryGetComponent<T>(this Entity entity, out T component) where T : Component
        => ComponentRegistry.TryGetComponent(entity, out component!);

    /// <inheritdoc cref="ComponentRegistry.TryGetComponent"/>
    public static bool TryGetComponent(this Entity entity, Type type, out Component component)
        => ComponentRegistry.TryGetComponent(entity, type, out component!);

    /// <inheritdoc cref="ComponentRegistry.GetAllComponents"/>
    [Pure]
    public static List<Component> GetAllComponents(this Entity entity)
        => ComponentRegistry.GetAllComponents(entity);

    #endregion

    #region Add Components

    public static T AddComponent<T>(this Entity entity) where T : Component, new()
        => (T)ComponentRegistry.AddComponent(entity, ComponentRegistry.FastCreate(typeof(T)));

    /// Use AddComponent(component instance) or the generic method instead! This is more dangerous!
    public static Component AddComponent(this Entity? entity, Type type)
        => ComponentRegistry.AddComponent(entity, ComponentRegistry.FastCreate(type));

    public static Component AddComponent(this Entity entity, Component comp)
        => ComponentRegistry.AddComponent(entity, comp);

    #endregion

    #region Has Components

    /// <inheritdoc cref="ComponentRegistry.HasComponent"/>
    [Pure]
    public static bool HasComponent<T>(this Entity entity) where T : Component
        => ComponentRegistry.HasComponent(entity, typeof(T));

    /// <inheritdoc cref="ComponentRegistry.HasComponent"/>
    [Pure]
    public static bool HasComponent(this Entity entity, Type componentType)
        => ComponentRegistry.HasComponent(entity, componentType);

    #endregion
}