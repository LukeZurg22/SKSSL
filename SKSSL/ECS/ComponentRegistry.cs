using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using SKSSL.Extensions;
using static SKSSL.DustLogger; // I like my DustLogger. I will use it everywhere.
using Type = System.Type; // For reflection purposes.

// ReSharper disable UnusedMember.Global
// ReSharper disable InvalidXmlDocComment

namespace SKSSL.ECS;

/// Central registry that creates, handles, gets, an deletes components.
public class ComponentRegistry
{
    #region Fast Component Creation

    private static readonly Dictionary<Type, Func<Component>> _creators = new();

    internal static Component FastCreate(Type type)
    {
        if (_creators.TryGetValue(type, out var creator))
            return creator();

        Func<Component> newCreator;

        // Try to find parameterless constructor
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
            // Slow but safe fallback: use Activator
            newCreator = () => (Component)Activator.CreateInstance(type)!
                               ?? throw new InvalidOperationException(
                                   $"Cannot instantiate {type.Name}: no parameterless constructor and Activator failed.");
        }

        // Cache for next time (thread-safe enough)
        _creators[type] = newCreator;

        return newCreator();
    }

    #endregion

    private static readonly Dictionary<Type, int> _typeToId = new();
    private static readonly Dictionary<int, Type> _idToType = new();
    private static readonly Dictionary<string, Type> _registeredComponents = new();

    /// All registered component types-types contained in the system.
    public static IReadOnlyDictionary<Type, int> RegisteredTypesDictionary => _typeToId;

    /// All registered component class-types contained in the system.
    public static IReadOnlyDictionary<string, Type> RegisteredComponentTypesDictionary => _registeredComponents;

    /// <summary>
    /// Dictionary of all active components.
    /// </summary>
    private readonly ConcurrentDictionary<Type, object> _activeComponentArrays = new(); // Type -> ComponentArray<T>

    private static bool Initialized { get; set; } = false;

    private static int _nextTypeId = 0;

    /// Number of Component Types in the registry. Gets next available Component ID.
    public static int Count => _nextTypeId;

    /// <param name="id">ID of Registered Component</param>
    /// <returns>Null or Type Definition based on provided ID.</returns>
    public static Type? GetType(int id) => _idToType.GetValueOrDefault(id);

    #region Component Registration and Assembly Checks

    /// Uses reflection to get all defined components in the (relevant) assemblies, and initializes them.
    public static void Initialize()
    {
        if (Initialized) return;
        Initialized = true;

        // Keeping stopwatch timer for releases. It's nice to have.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(IsRelevantAssembly)
            .ToArray();

        Log($"...scanning {assemblies.Length} assemblies for components...");

        int componentCount = 0;
        foreach (Assembly assembly in assemblies)
        {
            var types = GetTypesSafe(assembly);
            foreach (Type type in types)
            {
                if (!IsValidComponent(type))
                    continue;
                GetOrRegister(type); // Registers
                componentCount++;
            }
        }

        stopwatch.Stop();
        Initialized = true;

        // Logging
        Log($"Registered {componentCount} components in {stopwatch.ElapsedMilliseconds}ms");
        Log("Registered component types:");
        // Print all registered components in a nice list. 
        StringBuilder consoleTypesOutput = new();
        foreach (Type type in _registeredComponents.Values)
            consoleTypesOutput.AppendLine($"\n  {type.Name} -> ID {GetOrRegister(type)}");
        Log(consoleTypesOutput.ToString());

        return;

        Type[] GetTypesSafe(Assembly asm)
        {
            try
            {
                return asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).ToArray()!;
            }
            catch
            {
                return [];
            }
        }

        bool IsValidComponent(Type type) =>
            typeof(Component).IsAssignableFrom(type) &&
            !type.IsAbstract &&
            !type.IsGenericTypeDefinition;
    }

    /// <summary>
    /// Filters game assemblies. Includes hard-coded assemblies that use SKSSL, KBSL, or Kuiperbilt.
    /// </summary>
    private static bool IsRelevantAssembly(Assembly assembly)
    {
        string name = assembly.GetName().Name ?? "";

        // Skip problematic/problematic assemblies
        return !name.StartsWith("MonoGame.", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("mscorlib") &&
               !name.StartsWith("netstandard") &&
               !assembly.IsDynamic &&
               !assembly.ReflectionOnly;
    }

    #endregion

    #region Get Methods

    /// <summary>
    /// Used for extensions that attempt to retrieve a defined component from an entity.
    /// </summary>
    private static bool TryGetComponentIndex(Entity entity, Type componentType, out int index)
    {
        if (!_typeToId.TryGetValue(componentType, out var typeId))
        {
            index = -1;
            return false;
        }

        index = entity.ComponentIndices[typeId];
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
    internal static Component? GetComponentAt(object array, int index)
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
    public static int GetComponentTypeId<T>() => GetComponentTypeId(typeof(T));

    /// <summary>
    /// Multipurpose method used to retrieve an ID of a registered type, or additionally
    /// register said-type before returning.
    /// </summary>
    /// <param name="type">A class-type definition hopefully implementing <see cref="Component"/>.</param>
    /// <returns>Integer ID of (what should be) a Type implementing <see cref="Component"/>.</returns>
    private static int GetOrRegister(Type type)
    {
        if (_typeToId.TryGetValue(type, out int id))
            return id;

        id = Interlocked.Increment(ref _nextTypeId) - 1;
        // For reverse-checking in entities.
        _typeToId[type] = id;
        // For entity ID lists to types.
        _idToType[id] = type;
        // For deserializing entities. Renames TestComponent -> Test for deserialization reasons.
        _registeredComponents[type.Name.Replace("Component", string.Empty)] = type;

        return id;
    }

    #endregion

    // ""Unsafe"" get methods.

    #region More Get Methods

    /// <summary>
    /// Acts like <see cref="GetComponent{T}"/> but directly expects a provided type.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="componentType">The runtime type of the component (must implement ISKComponent).</param>
    /// <returns>The component instance (boxed as ISKComponent), or null if not found (or throws based on preference).</returns>
    /// <exception cref="InvalidOperationException">Thrown if the entity does not have the component or type is invalid.</exception>
    public Component? GetComponent(Entity entity, Type componentType)
    {
        if (!typeof(Component).IsAssignableFrom(componentType))
            throw new ArgumentException($"Type {componentType.Name} must implement ISKComponent.",
                nameof(componentType));

        if (!entity.HasComponent(componentType))
            return null;

        if (!TryGetComponentIndex(entity, componentType, out var index))
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
    public ref T GetComponent<T>(Entity entity) where T : Component
    {
        if (!TryGetComponentIndex(entity, typeof(T), out var index))
            throw new InvalidOperationException($"Failed to find expected component type in Entity #{entity.Uid}");
        return ref GetOrCreateComponentArray<T>().GetRefAt<T>(index);
    }

    #endregion

    // ""Unsafe"" add methods.

    #region AddComponent

    /// <summary>
    /// Adds a component of the specified runtime type and returns the new component boxed instance.
    /// </summary>
    /// <param name="entity">Entity that a component is added to.</param>
    /// <param name="componentType">The runtime type of the component to add.</param>
    /// <returns>The newly added component instance (boxed as object).</returns>
    /// <exception cref="ArgumentException">If the type doesn't implement ISKComponent.</exception>
    /// <exception cref="InvalidOperationException">If reflection fails or array is missing.</exception>
    public Component AddComponent(Entity entity, Component component)
    {
        if (component is null)
            throw new ArgumentException(
                $"Fed null component to {entity.Handle} Entity [{entity.Uid}]. It likely does not implement \"{nameof(Component)}\".");

        Type componentType = component.GetType();
        // Get or create the component array
        if (GetOrCreateComponentArray(componentType) is not IterArray componentArray)
            throw new ArgumentException($"Cannot create IterArray of Component {componentType.Name}.");

        // Store index of component inside entity, using index of its type.
        var componentIndex = componentArray.Count;
        entity.ComponentIndices[GetComponentTypeId(componentType)] = componentIndex;

        // Assign reference back to parent.
        component.Entity = entity;

        // Set component index in its array to referenced component
        componentArray.Set(componentIndex, component);
        componentArray.Increment();
        return component; // Fin.
    }

    #endregion

    // AddComponent calls surrounded in Try-Catch.

    #region TryAddComponent

    public bool TryAddComponent<T>(Entity entity, out T? component) where T : Component, new()
    {
        bool output = TryAddComponent(entity, typeof(T), out var compObject);
        component = compObject as T;
        return output;
    }

    public bool TryAddComponent(Entity entity, Type componentType, out object? component)
    {
        try
        {
            component = AddComponent(entity, FastCreate(componentType));
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
    /// <param name="entity"></param>
    /// <param name="component">Component output for use.</param>
    /// <typeparam name="T">Expected Component Type within entity.</typeparam>
    /// <returns>False if a component wasn't found.</returns>
    public bool TryGetComponent<T>(Entity entity, out T? component) where T : Component
    {
        component = null;
        int typeId = GetComponentTypeId<T>();
        int index = entity.ComponentIndices[typeId];

        if (index == -1)
            return false;

        component = GetOrCreateComponentArray<T>().GetRefAt<T>(index);
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a component using explicit type, outputting null interface of <see cref="Component"/>
    /// if not found.
    /// </summary>
    public bool TryGetComponent(Entity entity, Type componentType, out Component? component)
    {
        component = null;

        if (!typeof(Component).IsAssignableFrom(componentType))
            return false;

        int typeId = GetComponentTypeId(componentType);
        int index = entity.ComponentIndices[typeId];

        if (index == -1)
            return false;

        var array = _activeComponentArrays[componentType];
        component = GetComponentAt(array, index);
        return true;
    }

    #endregion

    // Best not to use this.

    #region GetAllComponents

    /// <summary>
    /// Gets a list of all components in an entity as a snapshot at the time of the call meaning changes to the entity
    /// won't affect the returned list. Will require casting. Assumes that all returned components are valid.
    /// </summary>
    /// <returns>A list of all components currently attached to this entity (boxed as object).</returns>
    /// <remarks>
    /// Components are returned boxed. Pattern-matching or casting will be needed to access specific types.
    /// This is intended for debugging, serialization, inspection, or rare runtime needs.
    /// For performance, use <see cref="GetComponent{T}"/> instead.
    /// </remarks>
    public ref List<object> GetAllComponents(Entity entity)
    {
        // Return a ref to a static thread-local list to avoid allocations in hot paths
        // Still safe since it's ref-local-scoped.
        ref var resultList = ref ThreadLocalList<object>.GetOrCreate();

        resultList.Clear();
        var indices = entity.ComponentIndices;

        foreach ((int typeId, Type? componentType) in _idToType)
        {
            // Checking to make sure the thing has it.
            int indexOfComponentEntry = indices[typeId];
            if (indexOfComponentEntry == -1)
                continue; // Short-circuit

            var array = _activeComponentArrays[componentType];
            var component = GetComponentAt(array, indexOfComponentEntry);
            if (component is not null)
                resultList.Add(component);
        }

        return ref resultList;
    }

    private static class ThreadLocalList<T>
    {
        [ThreadStatic] private static List<T>? _list;

        public static ref List<T> GetOrCreate()
        {
            _list ??= new List<T>(8);
            return ref _list!;
        }
    }

    #endregion

    public static bool HasComponent(Entity entity, Type componentType)
        => entity.ComponentIndices[RegisteredTypesDictionary.GetValueOrDefault(componentType, -1)] != -1;
}