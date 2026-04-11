using SKSSL.ECS;

namespace SKSSL.Extensions;

/// Extensions to the ECS that allow direct interaction with its parts without making tedious manual Context-Request calls.
public static partial class EntityExtensions
{
    public static ref T GetComponent<T>(this SKEntity entity) where T : class, ISKComponent => ref ComponentRegistry.GetComponent<T>(entity);

    public static bool TryGetComponent<T>(this SKEntity entity, out T? component) where T : class, ISKComponent
        => ComponentRegistry.TryGetComponent(entity, out component);
    
    public static T AddComponent<T>(this SKEntity entity) where T : struct, ISKComponent
        => (T)AddComponent(entity, typeof(T));

    public static object AddComponent(this SKEntity entity, Type type)
        => ComponentRegistry.AddComponent(entity, type);

    public static List<object> GetAllComponents(this SKEntity entity)
        => ComponentRegistry.GetAllComponents(entity);

    public static bool HasComponent<T>(this SKEntity entity) where T : ISKComponent
        => HasComponent(entity, typeof(T));

    public static bool HasComponent(this SKEntity entity, Type componentType)
        =>  ComponentRegistry.HasComponent(entity, componentType);
}