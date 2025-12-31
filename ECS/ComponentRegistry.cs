using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Type = System.Type;
using System.Collections.Concurrent;

// ReSharper disable InvalidXmlDocComment

namespace SKSSL.Registry;

/// Central registry that creates, handles, gets, an deletes components.
public static partial class ComponentRegistry
{
    #region Fast Component Creation

    private static readonly Dictionary<Type, Func<object>> _creators = new();

    internal static object FastCreate(Type type)
    {
        if (_creators.TryGetValue(type, out var creator))
            return creator();

        Func<object> newCreator;

        // Try to find parameterless constructor
        ConstructorInfo? ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor != null)
        {
            // Fast path: compile expression tree once
            NewExpression newExpr = Expression.New(ctor);
            var lambda = Expression.Lambda<Func<object>>(newExpr);
            newCreator = lambda.Compile();
        }
        else
        {
            // Slow but safe fallback: use Activator
            newCreator = () => Activator.CreateInstance(type)
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
    public static Dictionary<string, Type> _registeredComponents { get; } = new();

    /// <summary>
    /// Dictionary of all active components.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, object> _activeComponentArrays = new(); // Type -> ComponentArray<T>

    private static int _nextTypeId = 0;
    private static bool _initialized = false;

    #region Component Registration and Assembly Checks

    public static void RegisterAllComponents()
    {
        if (_initialized) return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(IsRelevantAssembly)
            .ToArray();

        DustLogger.Log($"Scanning {assemblies.Length} assemblies for components...");

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
        _initialized = true;

        // Logging
        DustLogger.Log($"Registered {componentCount} components in {stopwatch.ElapsedMilliseconds}ms");
        DustLogger.Log("Registered types:");
        foreach (Type type in _registeredComponents.Values)
            DustLogger.Log($"  {type.Name} -> ID {GetOrRegister(type)}");
    }

    /// <summary>
    /// Filters game assemblies. Includes hard-coded assemblies that use SKSSL, KBSL, or Kuiperbilt.
    /// </summary>
    private static bool IsRelevantAssembly(Assembly assembly)
    {
        string name = assembly.GetName().Name ?? "";

        // Skip problematic/problematic assemblies
        if (name.StartsWith("MonoGame.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("mscorlib") ||
            name.StartsWith("netstandard") ||
            assembly.IsDynamic ||
            assembly.ReflectionOnly)
            return false;

        // Hard-coding our supported assemblies.
        return name.Contains("SKSSL") ||
               name.Contains("KBSL") ||
               name.Contains("Kuiperbilt");
    }

    private static Type[] GetTypesSafe(Assembly asm)
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

    private static bool IsValidComponent(Type t) =>
        typeof(ISKComponent).IsAssignableFrom(t) &&
        !t.IsAbstract &&
        !t.IsInterface &&
        !t.IsGenericTypeDefinition;

    #endregion

    // I just couldn't choose which to implement. Theres multiple ways to do this and i am picky about performance.
    //  The compiler shall handle the rest of this, consequences be damned.
    #region ComponentArray Activators
#pragma warning disable CS0162 // Unreachable code detected
#pragma warning disable SYSLIB0050

    public static object GetOrCreateComponentArrayActivator(Type componentType)
    {
        return _activeComponentArrays.GetOrAdd(componentType, CreateArray);

        static object CreateArray(Type t)
        {
            Type arrayType = typeof(ComponentArray<>).MakeGenericType(t);
            return Activator.CreateInstance(arrayType)!;
        }
    }
    
    internal static object GetOrCreateComponentArrayRuntime(Type componentType)
    {
        return _activeComponentArrays.GetOrAdd(componentType, t =>
        {
            Type arrayType = typeof(ComponentArray<>).MakeGenericType(t);
            return RuntimeHelpers.GetUninitializedObject(arrayType);
            // Requires manual call .Initialize() or just let List<T> default init
        });
    }
#pragma warning restore SYSLIB0050
#pragma warning restore CS0162 // Unreachable code detected
    #endregion

    /// <summary>
    /// Convenient version of <see cref="GetOrCreateComponentArray"/> that which it calls.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static ComponentArray<T> GetOrCreateComponentArray<T>() where T : struct, ISKComponent
        => (ComponentArray<T>)GetOrCreateComponentArray(typeof(T));

    /// <summary>
    /// Gets or creates the ComponentArray<T> for the given component type.
    /// Called only once per component type.
    /// </summary>
    public static object GetOrCreateComponentArray(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);

        return _activeComponentArrays.GetOrAdd(componentType, CreateComponentArray);

        static object CreateComponentArray(Type t)
        {
            // Build ComponentArray<componentType>
            Type arrayType = typeof(ComponentArray<>).MakeGenericType(t);

            // Call the public parameterless constructor
            return Activator.CreateInstance(arrayType)
                   ?? throw new InvalidOperationException($"Failed to instantiate ComponentArray<{t.Name}>");
        }
    }
   
    internal static ISKComponent? GetComponentAt(object array, int index)
        => ((IList<object>)array)[index] as ISKComponent;

    private static int GetComponentTypeId(Type componentType)
    {
        if (!_typeToId.TryGetValue(componentType, out int id))
            throw new ArgumentException($"Component type {componentType.Name} not registered!");
        return id;
    }

    public static int GetComponentTypeId<T>() => GetComponentTypeId(typeof(T));

    public static int GetOrRegister(Type type)
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

    public static int Count => _nextTypeId;

    public static IReadOnlyList<Type> RegisteredTypes => _registeredComponents.Values.ToList().AsReadOnly();

    public static Type? GetType(int id) => _idToType.GetValueOrDefault(id);
}

/// <summary>
/// Contains the component instances for each registered entity. This list is instantiated; it gets pretty complicated.
/// </summary>
/// <typeparam name="T">Type of components being stored in this particular list.</typeparam>
/// <seealso cref="List"/>
public sealed class ComponentArray<T> where T : struct
{
    public ComponentArray(int capacity) => _items = new T[capacity];
    public ComponentArray() : this(1024) {}
    
    private T[] _items;
    private int _count = 0;

    public int Count => _count;

    public ref T Add()
    {
        if (_count >= _items.Length)
            Array.Resize(ref _items, _items.Length * 2);

        return ref _items[_count++];
    }

    public ref T GetAt(int index)
    {
        if (index < 0 || index >= _count)
            throw new IndexOutOfRangeException();

        return ref _items[index];
    }
}