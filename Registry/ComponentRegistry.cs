using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Type = System.Type;

namespace SKSSL.Registry;

/// Central registry that assigns unique IDs to component types
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
            // Cache this delegate too so we don't check again
            newCreator = () => Activator.CreateInstance(type)
                               ?? throw new InvalidOperationException(
                                   $"Cannot instantiate {type.Name}: no parameterless constructor and Activator failed.");
        }

        // Cache for next time (thread-safe enough for startup)
        _creators[type] = newCreator;

        return newCreator();
    }

    #endregion

    private static readonly Dictionary<Type, int> _typeToId = new();
    private static readonly Dictionary<int, Type> _idToType = new();
    private static readonly Dictionary<Type, object> _componentArrays = new(); // Type -> ComponentArray<T>
    private static readonly List<Type> _registeredTypes = [];

    private static int _nextTypeId = 0;
    private static bool _isInitialized = false;

    #region Component Registration and Assembly Checks

    /// <summary>
    /// Registers ALL components derived from BaseComponent using safe reflection.
    /// Handles MonoGame.Framework failures, missing dependencies, and edge cases.
    /// Call once at startup (e.g., in Game1.Initialize()).
    /// </summary>
    public static void RegisterAllComponents()
    {
        if (_isInitialized) return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var myAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(IsRelevantAssembly)
            .ToArray();

        DustLogger.Log($"Scanning {myAssemblies.Length} assemblies for components...");

        int registeredCount = 0;

        foreach (Assembly assembly in myAssemblies)
        {
            try
            {
                var types = GetTypesSafe(assembly);
                foreach (Type? type in types)
                {
                    if (!IsValidComponentType(type) || type is null)
                        continue;
                    GetId(type); // Auto-registers via existing logic
                    registeredCount++;
                }
            }
            catch (Exception ex)
            {
                DustLogger.Log($"Skipped assembly {assembly.GetName().Name}: {ex.Message}",
                    DustLogger.LOG.SYSTEM_WARNING);
            }
        }

        _isInitialized = true;
        stopwatch.Stop();

        // Logging
        DustLogger.Log($"Registered {registeredCount} components in {stopwatch.ElapsedMilliseconds}ms");
        DustLogger.Log("Registered types:");
        foreach (Type type in _registeredTypes)
            DustLogger.Log($"  {type.Name} -> ID {GetId(type)}");
    }

    /// <summary>
    /// Filters to only your game assemblies (excludes MonoGame, system, etc.)
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

    /// <summary>
    /// Safely extracts types, handling ReflectionTypeLoadException from MonoGame
    /// </summary>
    [SuppressMessage("ReSharper", "ConditionalAccessQualifierIsNonNullableAccordingToAPIContract")]
    private static Type?[] GetTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Extract partially loaded types (most MonoGame issues still give some types)
            var loadedTypes = ex.Types.Where(t => t != null).ToArray();

            // Log MonoGame-specific issues only once
            if (assembly.GetName().Name?.StartsWith("MonoGame") == true)
                DustLogger.Log($"MonoGame assembly partial load: {loadedTypes.Length}/{ex.Types?.Length ?? 0} types");

            return loadedTypes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load types from {assembly.GetName().Name}: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Validates a type is a usable component
    /// </summary>
    private static bool IsValidComponentType(Type? type)
    {
        return type != null &&
               !type.IsAbstract &&
               !type.IsGenericTypeDefinition &&
               !type.IsInterface &&
               type.IsSubclassOf(typeof(SKComponent)) &&
               type.GetConstructor(Type.EmptyTypes) != null; // Must have parameterless ctor
    }

    #endregion

    internal static object GetOrCreateComponentArray(Type componentType)
    {
        if (_componentArrays.TryGetValue(componentType, out var existingArray))
            return existingArray;

        // Create the closed generic type: ComponentArray<componentType>
        Type arrayType = typeof(ComponentArray<>).MakeGenericType(componentType);

        // ComponentArray<T> always has a parameterless constructor
        // Use Activator — it's safe and only called once per component type
        object newArray = Activator.CreateInstance(arrayType)
                          ?? throw new InvalidOperationException(
                              $"Failed to create ComponentArray<{componentType.Name}>");

        _componentArrays[componentType] = newArray;
        return newArray;
    }

    internal static SKComponent? GetComponentAt(object array, int index)
        => ((IList<object>)array)[index] as SKComponent;

    private static int GetComponentTypeId(Type componentType)
    {
        if (!_typeToId.TryGetValue(componentType, out int id))
            throw new ArgumentException($"Component type {componentType.Name} not registered!");
        return id;
    }

    public static int GetId(Type type)
    {
        if (_typeToId.TryGetValue(type, out int id))
            return id;

        id = Interlocked.Increment(ref _nextTypeId) - 1;
        _typeToId[type] = id;
        _idToType[id] = type;
        _registeredTypes.Add(type);

        return id;
    }

    public static int Count => _nextTypeId;

    public static IReadOnlyList<Type> RegisteredTypes => _registeredTypes.AsReadOnly();

    public static Type? GetType(int id) => _idToType.GetValueOrDefault(id);
}

internal class ComponentArray<T> : List<T>
{
    /// <summary>
    /// Stores a certain capacity of various types of components.
    /// This is a list of all active component instances.
    /// </summary>
    /// <param name="capacity"></param>
    public ComponentArray(int capacity = 1024) : base(capacity)
    {
    }
}