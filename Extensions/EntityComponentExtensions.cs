// ReSharper disable UnusedMember.Global

using System.Reflection;

namespace SKSSL.ECS;

/// <summary>
/// Includes extension and overload methods for handling <see cref="SKEntity"/> objects and their components.
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

        var array = _activeComponentArrays[componentType];
        return GetComponentAt(array, index);
    }

    public static bool HasComponent<T>(this SKEntity entity) where T : struct, ISKComponent
        => entity.ComponentIndices[GetComponentTypeId<T>()] != -1;
    
    /// <summary>
    /// Adds a component to an entity and returns it by reference.
    /// </summary>
    /// <param name="entity"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static ref T AddComponent<T>(this SKEntity entity) where T : struct, ISKComponent
    {
        var array = GetOrCreateComponentArray<T>();

        // This calls a custom Add() -> returns ref to the new slot
        ref T component = ref array.Add();

        // Default-initialize (zero-init for struct — fast and safe)
        component = default; // or = new T();

        int typeId = GetComponentTypeId<T>();
        entity.ComponentIndices[typeId] = array.Count - 1; // Count already incremented

        return ref component;
    }
    
    /// <summary>
    /// Adds a component of the specified runtime type and returns the new component instance (by value).
    /// </summary>
    public static object AddComponent(this SKEntity entity, Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!typeof(ISKComponent).IsAssignableFrom(componentType))
            throw new ArgumentException($"Type {componentType.Name} does not implement ISKComponent");

        // Get or create the component array
        object arrayObj = GetOrCreateComponentArray(componentType);

        // Call custom Add() via reflection to allocate slot
        MethodInfo addMethod = arrayObj.GetType()
                                   .GetMethod("Add", Type.EmptyTypes)
                               ?? throw new InvalidOperationException($"Missing Add() on ComponentArray<{componentType.Name}>");

        // Increments count and returns discarded [ref]erence.
        addMethod.Invoke(arrayObj, null);

        // Get the new index
        int newIndex = (int)arrayObj.GetType().GetProperty("Count")!.GetValue(arrayObj)! - 1;

        // Store index in entity
        int typeId = GetComponentTypeId(componentType);
        entity.ComponentIndices[typeId] = newIndex;

        // Return the actual component instance (by value) — perfect for initialization
        MethodInfo getAtMethod = arrayObj.GetType().GetMethod("GetAt")
                                 ?? throw new InvalidOperationException($"Missing GetAt(int) on ComponentArray<{componentType.Name}>");

        // Returns ref T, but boxed to object
        object component = getAtMethod.Invoke(arrayObj, [newIndex])
                           ?? throw new InvalidOperationException("GetAt returned null");

        return component;
    }

    
    /// <summary>
    /// Adds a provided component type to an entity. <see cref="SKEntity"/> calls this, using itself as the
    /// entity referenced.
    /// </summary>
    /// <param name="entity">Entity that which is receiving a new component.</param>
    /// <returns>Component added.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    // Type-safe Get
    public static ref T GetComponent<T>(this SKEntity entity) where T : struct, ISKComponent
    {
        int typeId = GetComponentTypeId<T>();
        int index = entity.ComponentIndices[typeId];

        if (index == -1)
            throw new InvalidOperationException($"Entity {entity.RuntimeId} missing {typeof(T).Name}");

        return ref GetOrCreateComponentArray<T>().GetAt(index);
    }
}