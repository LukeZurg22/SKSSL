using SKSSL.ECS;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.Extensions;

/// Extensions to the ECS that allow direct interaction with its parts without making tedious manual Context-Request calls.
public static partial class EntityExtensions
{
    #region Get Components

    public static ref T GetComponent<T>(this SKEntity entity) where T : ISKComponent
        => ref ComponentRegistry.GetComponent<T>(entity);

    public static ISKComponent? GetComponent(this SKEntity entity, Type componentType)
        => ComponentRegistry.GetComponent(entity, componentType);

    public static bool TryGetComponent<T>(this SKEntity entity, out T? component) where T : ISKComponent
        => ComponentRegistry.TryGetComponent(entity, out component);

    public static bool TryGetComponent(this SKEntity entity, Type type, out ISKComponent? component)
        => ComponentRegistry.TryGetComponent(entity, type, out component);

    public static List<object> GetAllComponents(this SKEntity entity)
        => ComponentRegistry.GetAllComponents(entity);

    #endregion

    #region Add Components

    public static T AddComponent<T>(this SKEntity entity) where T : ISKComponent, new()
        => (T)ComponentRegistry.AddComponent(entity, new T());

    /// Use AddComponent(component instance) or the generic method instead! This is more dangerous!
    public static ISKComponent AddComponent(this SKEntity entity, Type type)
        => ComponentRegistry.AddComponent(entity, ComponentRegistry.CreateComponentFromType(type));

    public static ISKComponent AddComponent(this SKEntity entity, ISKComponent comp)
        => ComponentRegistry.AddComponent(entity, comp);

    #endregion

    #region Has Components

    public static bool HasComponent<T>(this SKEntity entity) where T : ISKComponent
        => HasComponent(entity, typeof(T));

    public static bool HasComponent(this SKEntity entity, Type componentType)
        => ComponentRegistry.HasComponent(entity, componentType);

    #endregion
}