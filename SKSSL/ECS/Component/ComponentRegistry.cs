using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Type = System.Type; // For reflection purposes.

namespace SKSSL.ECS;

/// Central registry that creates, handles, gets, an deletes components.
public class ComponentRegistry
{
    #region Fast Component Creation

    private static readonly Dictionary<Type, Func<Component>> _creators = new();

    [System.Diagnostics.Contracts.Pure]
    internal static Component FastCreate(Type type)
    {
        if (_creators.TryGetValue(type, out var creator))
            return creator();

        Func<Component> newCreator;

        // Try to find parameterless constructor.
        ConstructorInfo? ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor != null)
        {
            // Fast path: compile expression tree once
            NewExpression newExpr = Expression.New(ctor);
            var lambda = Expression.Lambda<Func<Component>>(newExpr);
            newCreator = lambda.Compile();
        }
        else
        {
            // Slow but safe fallback: use Activator.
            newCreator = () => (Component)Activator.CreateInstance(type)!
                               ?? throw new InvalidOperationException(
                                   $"Cannot instantiate {type.Name}: no parameterless constructor; Activator failed.");
        }

        // Cache for next time (thread-safe enough)
        _creators[type] = newCreator;

        return newCreator();
    }

    #endregion

    #region Storage

    private static readonly Dictionary<Type, int> _typeToId = new(); // Comp Type to ID
    private static readonly Dictionary<int, Type> _idToType = new(); // Comp ID to Type
    private static readonly Dictionary<string, Type> _registeredComponents = new(); // Name Handle to Type

    /// <summary>
    /// Array of component indices.<br/>
    /// Index = Entity UI, Value = Component Indices<br/>
    /// Internal Array Index = ComponentTypeId&lt;T&gt;.Id,<br/>
    /// Internal Array Value = slot in ComponentArray&lt;T&gt; (-1 if missing)
    /// <br/><br/>
    /// For every index, there is a unique component type.
    /// <seealso cref="IterArray{T}"/>
    /// </summary>
    private readonly Dictionary<uint, int[]> _entityUIDToComponentIndices = new();

    internal void PrepareEntityComponentStorage(uint entityUid)
    {
        // Make component indices storage.
        var arr = new int[Count];
        Array.Fill(arr, -1);
        _entityUIDToComponentIndices[entityUid] = arr;
    }
    
    /// Called by Source Generator.
    public static void Clear()
    {
        _typeToId.Clear();
        _idToType.Clear();
        _registeredComponents.Clear();
    }

    /// All registered component types-types contained in the system. Key = Type; Value = ID
    public static IReadOnlyDictionary<Type, int> RegisteredTypeIDDictionary => _typeToId;

    /// All registered component class-types contained in the system. Key = TypeName (short)
    public static IReadOnlyDictionary<string, Type> RegisteredHandleComponentTypesDictionary => _registeredComponents;

    /// <summary>
    /// Dictionary of all active components.
    /// </summary>
    private readonly ConcurrentDictionary<Type, object> _activeComponentArrays = new(); // Type -> ComponentArray<T>

    #endregion

    private static int _nextTypeId = 0;

    /// Number of Component Types in the registry. Gets next available Component ID.
    public static int Count => _nextTypeId;

    // ReSharper disable once UnusedMember.Global
    /// <param name="id">ID of Registered Component</param>
    /// <returns>Null or Type Definition based on provided ID.</returns>
    [System.Diagnostics.Contracts.Pure]
    public static Type? GetType(int id) => _idToType.GetValueOrDefault(id);

    /// <param name="type">Type of Registered Component</param>
    /// <returns>ID of Component Type based on provided type.</returns>
    [System.Diagnostics.Contracts.Pure]
    public static int GetId(Type type) => _typeToId.GetValueOrDefault(type);

    #region Get Methods

    /// <summary>
    /// Used for extensions that attempt to retrieve a defined component from an entity.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    private bool TryGetComponentIndex(EntityUid entity, Type componentType, out int index)
    {
        if (!_typeToId.TryGetValue(componentType, out var typeId))
        {
            index = -1;
            return false;
        }

        index = _entityUIDToComponentIndices[entity][typeId];
        return index != -1;
    }

    /// <summary>
    /// Convenient version of <see cref="GetOrCreateComponentArray"/> that which it calls.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private IterArray<T> GetOrCreateComponentArray<T>() where T : Component
        => (IterArray<T>)GetOrCreateComponentArray(typeof(T));

    /// <summary>
    /// Gets or creates the ComponentArray&lt;T&gt; for the given component type.
    /// Called only once per component type.
    /// </summary>
    private object GetOrCreateComponentArray(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);

        return _activeComponentArrays.GetOrAdd(componentType, CreateComponentArray);

        static object CreateComponentArray(Type t)
        {
            // Build ComponentArray<componentType>
            Type arrayType = typeof(IterArray<>).MakeGenericType(t);

            // Call the public parameterless constructor
            return Activator.CreateInstance(arrayType)
                   ?? throw new InvalidOperationException($"Failed to instantiate ComponentArray<{t.Name}>");
        }
    }

    /// <param name="array"><see cref="IterArray{T}"/> of Active components.</param>
    /// <param name="index">Index of component provided by an <see cref="Entity"/> up the chain.</param>
    /// <returns>
    /// Gets a component using a <see cref="IterArray{T}"/> and provided index of the component's position
    /// within the array.
    /// </returns>
    private static Component? GetComponentAt(object array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return ((IterArray)array)[index] as Component;
    }

    internal static ref T GetComponentAt<T>(IterArray<T> array, int index) where T : Component
        => ref array.GetRefAt<T>(index);

    /// <returns>ID of component defined in type dictionary, or -1.</returns>
    /// <exception cref="ArgumentException">Provided type not present in dictionary.</exception>
    private static int GetComponentTypeId(Type componentType)
    {
        if (!_typeToId.TryGetValue(componentType, out int id))
            throw new ArgumentException($"Component type {componentType.Name} not registered!");
        return id;
    }

    /// <inheritdoc cref="GetComponentTypeId"/>
    [System.Diagnostics.Contracts.Pure]
    private static int GetComponentTypeId<T>() => GetComponentTypeId(typeof(T));

    /// Safe-ish way to to obtain a registered type definition added here from Source Generator Registrar.
    [System.Diagnostics.Contracts.Pure]
    public static bool TryGetComponentType(string shortName, out Type type)
    {
        if (RegisteredHandleComponentTypesDictionary.TryGetValue(shortName, out Type? temp))
        {
            type = temp;
            return true;
        }

        type = null!;
        return false;
    }

    /// <summary>
    /// Multipurpose method used to retrieve an ID of a registered type, or additionally
    /// register said-type before returning.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="type">A class-type definition hopefully implementing <see cref="Component"/>.</param>
    /// <returns>Integer ID of (what should be) a Type implementing <see cref="Component"/>.</returns>
    // ReSharper disable once UnusedMember.Global // Used by Source Generator
    public static int GetOrRegister(string handle, Type type)
    {
        if (_typeToId.TryGetValue(type, out int id))
            return id;

        id = Interlocked.Increment(ref _nextTypeId) - 1;
        // For reverse-checking in entities.
        _typeToId[type] = id;
        // For entity ID lists to types.
        _idToType[id] = type;
        // For deserializing entities. Renames TestComponent -> Test for deserialization reasons.
        _registeredComponents[handle] = type;

        return id;
    }

    #endregion

    // ""Unsafe"" get methods.

    #region More Get Methods

    /// <summary>
    /// Acts like <see cref="GetComponent{T}"/> but expects a provided type directly.
    /// </summary>
    /// <param name="uid">Entity expected to contain component.</param>
    /// <param name="componentType">The runtime type of the component (must implement ISKComponent).</param>
    /// <returns>The component instance (boxed as ISKComponent), or null if not found (or throws based on preference).</returns>
    /// <exception cref="InvalidOperationException">Thrown if the entity does not have the component or type is invalid.</exception>
    /// <seealso cref="TryGetComponent{T}"/>
    [System.Diagnostics.Contracts.Pure]
    public Component? GetComponent(EntityUid uid, Type componentType)
    {
        if (!typeof(Component).IsAssignableFrom(componentType))
            throw new ArgumentException($"Type {componentType.Name} must implement ISKComponent.",
                nameof(componentType));

        if (!HasComponent(uid, componentType))
            return null;

        if (!TryGetComponentIndex(uid, componentType, out var index))
            return null;

        var array = _activeComponentArrays[componentType];
        return GetComponentAt(array, index);
    }

    /// <summary>
    /// Gets the component of the specified type from this entity.
    /// </summary>
    /// <typeparam name="T">The component type (must implement ISKComponent).</typeparam>
    /// <returns>A reference to the component if found; otherwise throws.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the entity does not have the component.</exception>
    [System.Diagnostics.Contracts.Pure]
    public ref T GetComponent<T>(EntityUid uid) where T : Component
    {
        if (!TryGetComponentIndex(uid, typeof(T), out var index))
            throw new InvalidOperationException($"Failed to find expected component type in Entity #{uid}");
        return ref GetOrCreateComponentArray<T>().GetRefAt<T>(index);
    }

    #endregion

    // ""Unsafe"" add methods.

    #region AddComponent

    /// <summary>
    /// Adds a component of the specified runtime type and returns the new component boxed instance.
    /// </summary>
    /// <param name="uid">Entity that a component is added to.</param>
    /// <param name="component">The runtime type of the component to add.</param>
    /// <returns>The newly added component instance (boxed as object).</returns>
    /// <exception cref="ArgumentException">If the type doesn't implement ISKComponent.</exception>
    /// <exception cref="InvalidOperationException">If reflection fails or array is missing.</exception>
    public Component AddComponent(EntityUid uid, Component component)
    {
        if (component is null)
        {
            throw new ArgumentException(
                $"Fed invalid component to Entity [{uid}]. " +
                $"It likely does not implement \"{nameof(Component)}\".");
        }

        Type componentType = component.GetType();
        // Get or create the component array
        if (GetOrCreateComponentArray(componentType) is not IterArray componentArray)
            throw new ArgumentException($"Cannot create IterArray of Component {componentType.Name}.");

        // Store index of component inside entity, using index of its type.
        var componentIndex = componentArray.Count;
        _entityUIDToComponentIndices[uid][GetComponentTypeId(componentType)] = componentIndex;

        // Assign reference back to parent.
        component.Entity = uid;

        // Set component index in its array to referenced component
        componentArray.Set(componentIndex, component);
        componentArray.Increment();
        return component; // Fin.
    }

    #endregion

    // AddComponent calls surrounded in Try-Catch.

    #region TryAddComponent

    public bool TryAddComponent<T>(EntityUid uid, out T? component) where T : Component, new()
    {
        bool output = TryAddComponent(uid, typeof(T), out var compObject);
        component = compObject as T;
        return output;
    }

    public bool TryAddComponent(EntityUid uid, Type componentType, out object? component)
    {
        try
        {
            component = AddComponent(uid, FastCreate(componentType));
            return true;
        }
        catch
        {
            component = null;
            return false;
        }
    }

    #endregion

    // No Try-Catch needed.

    #region TryGetComponent

    /// <summary>
    /// Attempts to safely retrieve a component from an entity.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component">Component output for use.</param>
    /// <typeparam name="T">Expected Component Type within entity.</typeparam>
    /// <returns>False if a component wasn't found.</returns>
    [System.Diagnostics.Contracts.Pure]
    public bool TryGetComponent<T>(EntityUid uid, out T component) where T : Component
    {
        component = null!;
        int typeId = GetComponentTypeId<T>();
        int componentIndex = _entityUIDToComponentIndices[uid][typeId];

        if (componentIndex == -1)
            return false;

        component = GetOrCreateComponentArray<T>().GetRefAt<T>(componentIndex);
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a component using explicit type.
    /// </summary>
    /// <returns>true if component was found, output is not null. false if not found, output will be null.</returns>
    [System.Diagnostics.Contracts.Pure]
    public bool TryGetComponent(EntityUid uid, Type componentType, out Component component)
    {
        component = null!;

        if (!typeof(Component).IsAssignableFrom(componentType))
            return false;

        int typeId = GetComponentTypeId(componentType);
        int componentIndex = _entityUIDToComponentIndices[uid][typeId];

        if (componentIndex == -1)
            return false;

        var array = _activeComponentArrays[componentType];
        component = GetComponentAt(array, componentIndex)!;
        return true;
    }

    #endregion

    // Best not to use this.

    #region GetAllComponents

    /// <summary>
    /// Gets a list of all components in an entity as a snapshot at the time of the call meaning changes to the entity
    /// won't affect the returned list. Will require casting. Assumes that all returned components are valid.
    /// Not suggested for use.
    /// </summary>
    /// <returns>A list of all components currently attached to this entity (boxed as object).</returns>
    /// <remarks>
    /// Components are returned boxed. Pattern-matching or casting will be needed to access specific types.
    /// This is intended for debugging, serialization, inspection, or rare runtime needs.
    /// For performance, use <see cref="GetComponent{T}"/> instead.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    public ref List<Component> GetAllComponents(EntityUid uid)
    {
        // Return a ref to a static thread-local list to avoid allocations in hot paths
        // Still safe since it's ref-local-scoped.
        ref var resultList = ref ThreadLocalList<Component>.GetOrCreate();

        resultList.Clear();
        var indices = _entityUIDToComponentIndices[uid];

        foreach ((int typeId, Type? componentType) in _idToType)
        {
            // Checking to make sure the thing has it.
            int indexOfComponentEntry = indices[typeId];
            if (indexOfComponentEntry == -1)
                continue; // Short-circuit

            var array = _activeComponentArrays[componentType];
            Component? component = GetComponentAt(array, indexOfComponentEntry);
            if (component is not null)
                resultList.Add(component);
        }

        return ref resultList;
    }

    private static class ThreadLocalList<T>
    {
        [ThreadStatic] private static List<T>? _list;

        [System.Diagnostics.Contracts.Pure]
        public static ref List<T> GetOrCreate()
        {
            _list ??= new List<T>(8);
            return ref _list;
        }
    }

    #endregion

    /// <returns>true if entity possess an instance of component type, false if not.</returns>
    public bool HasComponent(EntityUid uid, Type componentType)
        => _entityUIDToComponentIndices[uid][RegisteredTypeIDDictionary.GetValueOrDefault(componentType, -1)] != -1;
}