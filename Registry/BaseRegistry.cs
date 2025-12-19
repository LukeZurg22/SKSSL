using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SKSSL.Utilities;

// ReSharper disable StaticMemberInGenericType

namespace SKSSL.Registry;

public abstract class
    BaseRegistry<Registry, T>
    where Registry : BaseRegistry<Registry, T>
    where T : BaseComponent
{
    /// <summary>
    /// Mutable suffix that can be adjusted per-registry to accomodate whatever type of tile naming scheme it uses.
    /// </summary>
    private static string Suffix { get; set; } = "Prototype";

    /// <summary>
    /// Global list of all active mechanics in the game, which is looped-through and called upon by <see cref="UpdateComponents"/>
    /// </summary>
    private static readonly List<T> AllActive = [];

    /// <summary>
    /// Global dictionary of all mechanic types which are instantiated elsewhere. This list is ambiguous between ALL
    /// types of mechanics, regardless of specific application.
    /// </summary>
    internal static readonly Dictionary<string, BaseComponent> Registered = [];

    /// <summary>
    /// Allows programmer to set reflection Suffix for Registry system. It is up to them to maintain it.
    /// </summary>
    protected static void SetSuffix(string suffix) => Suffix = suffix;

    /// <summary>
    /// Attempts to retrieve a component from the system.
    /// </summary>
    /// <param name="id">ID of the mechanic in-registry.</param>
    /// <param name="component">Output instanced variable of mechanic for use elsewhere.</param>
    /// <typeparam name="M">Generic type to indicate what kind of component.</typeparam>
    /// <returns>True if object / mechanic was found. False if returned null.</returns>
    public static bool TryGetComponent<M>(string id, out M? component) where M : BaseComponent
    {
        if (Registered.TryGetValue(id, out BaseComponent? proto) &&
            Activator.CreateInstance(proto.GetType()) is M instance)
        {
            foreach (PropertyInfo prop in proto.GetType().GetProperties())
            {
                if (prop.CanWrite)
                    prop.SetValue(instance, prop.GetValue(proto));
            }

            component = instance;
            return true;
        }

        component = default;
        return false;
    }

    /// <summary>
    /// Wrapper for the dictionary's add method that foments reporting overlaps.
    /// </summary>
    protected static void RegisterWithOverride(string key, BaseComponent component)
    {
        if (Registered.ContainsKey(key)) Console.WriteLine($"[Warning] Detected registry override: {key}");
        // Override previous entry
        Registered[key] = component;
    }

    protected static void RegisterWithOverride(BaseComponent component) => RegisterWithOverride(component.Id, component);

    protected static BaseComponent? Create(string yamlId)
    {
        // Convert to PascalCase + suffix
        var className = yamlId.ToPascalCase() + Suffix;

        Type? type = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == className && typeof(BaseComponent).IsAssignableFrom(t));

        if (type != null)
            return Activator.CreateInstance(type) as BaseComponent;

        Console.WriteLine($"Failed to create mechanic. \"{className}Mechanic.cs\" not found.");
        return null;
    }


    // IMPL: See below queue methods. Used for thread-work stuff. Make thread-safe.
    /// <summary>
    /// Enqueues activation of a mechanic.
    /// </summary>
    public static void QueueActivate(BaseComponent component)
    {
    }

    /// <summary>
    /// Enqueues deactivation of a mechanic.
    /// </summary>
    public static void QueueDeactivate(BaseComponent component)
    {
    }

    public static Dictionary<string, BaseComponent> GetRegistered() => Registered;

    /// <summary>
    /// Adds mechanic to active mechanics list, which is updated with <see cref="UpdateComponents"/>
    /// </summary>
    public static void Activate(T mech) => AllActive.Add(mech);

    /// <summary>
    /// Removes mechanic from active mechanics list.
    /// </summary>
    public static void Deactivate(T mech) => AllActive.Remove(mech);

    // TODO: Update all components on a thread
    /// <summary>
    /// Updates all components in this registry.
    /// </summary>
    public static void UpdateComponents(float deltaTime)
    {
        // Update all active components in this registry.
        foreach (T thing in AllActive) thing.Update(deltaTime);
    }

    /// <summary>
    /// Update all registries. Separate from UpdateComponents.
    /// </summary>
    /// <param name="deltaTime"></param>
    public static void UpdateRegistries(float deltaTime)
    {
        // Get all registry entries with IRegistryUpdatable
        var updaterTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IRegisteryUpdatable).IsAssignableFrom(t) && t.IsClass);

        foreach (Type type in updaterTypes) // Attempt to call "Update" on all Registries.
            type.GetMethod("Update")?.Invoke(null, [deltaTime]);
    }

    /// <summary>
    /// Clears all active and registered components. 
    /// </summary>
    protected static void Clean()
    {
        AllActive.Clear();
        Registered.Clear();
    }
}