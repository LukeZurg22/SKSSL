// ReSharper disable UnusedMember.Global

using SKSSL.Managers;

namespace SKSSL.Registry;

/// <summary>
/// Includes extension methods for <see cref="SKEntity"/> objects.
/// </summary>
public static partial class ComponentRegistry
{
    /// <summary>
    /// Attempts to safely retrieve a component from an entity.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="component">Component output for use.</param>
    /// <typeparam name="T">Expected Component Type within entity.</typeparam>
    /// <returns>False if a component wasn't found.</returns>
    public static bool TryGetComponent<T>(this SKEntity entity, out ISKComponent? component)
    {
        component = GetComponent(entity, typeof(T));
        return component != null;
    }
    
    public static ISKComponent? GetComponent(this SKEntity entity, Type componentType)
    {
        int typeId = GetComponentTypeId(componentType);
        int index = entity.ComponentIndices[typeId];

        if (index == -1)
            throw new Exception($"Entity {entity.ReferenceId} missing {componentType.Name}");

        var array = _activeComponentLists[componentType];
        return GetComponentAt(array, index);
    }

    public static void AddComponent<T>(this SKEntity entity) => EntityManager.AddComponent(entity, typeof(T));

}