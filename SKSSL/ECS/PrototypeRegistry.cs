using System;
using System.Collections.Generic;
using System.Reflection;
using SKSSL.YAML;
using VYaml.Emitter;
using static SKSSL.DustLogger;

// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

/// <summary>
/// Storing all prototype definitions.
/// </summary>
public abstract class PrototypeRegistry
{
    #region System Type Definitions

    /// Raw class type-definitions in development environment -only-. These are used in inheritance rules and are
    /// project-specific.
    public static readonly Dictionary<string, Type> Definitions = new();


    /// Outputs a string handle based on provided type linked to class-type definition.
    public static bool TryGetTypeHandle(Type type, out string typeHandle)
    {
        typeHandle = "";
        foreach (var definition in Definitions)
        {
            if (definition.Value != type)
                continue;
            typeHandle = definition.Key;
            return true;
        }

        return false;
    }

    /// Clears all definitions in registry. Called by Source Generator.
    // ReSharper disable once UnusedMember.Global
    public static void Clear()
    {
        Definitions.Clear();
        _loadedGamePrototypes.Clear();
    }
    
    #endregion

    public static bool ContainsDefinition(string name) => Definitions.Count != 0 && Definitions.ContainsKey(name);
    public static bool ContainsDefinition(Type type) => Definitions.Count != 0 && Definitions.ContainsValue(type);

    public static readonly Dictionary<string, Prototype> _loadedGamePrototypes = [];

    /// Individual definitions belonging to all prototype instances loaded from yaml.
    public static IReadOnlyDictionary<string, Prototype> LoadedGamePrototypes => _loadedGamePrototypes;



    /// <inheritdoc cref="Register{T,Y}"/>
    // ReSharper disable once UnusedMember.Global
    public static void Register<T>(Prototype yaml) where T : Entity => Register<T, Prototype>(yaml);

    /// <summary>
    /// Handles registration of definition from yaml data.
    /// </summary>
    /// <param name="yaml">The yaml file of the template.</param>
    /// <typeparam name="T">Derived Type of entity intermediate type registered. Forces inheritance.</typeparam>
    /// <typeparam name="Y"></typeparam>
    public static void Register<T, Y>(Y yaml) where T : Entity where Y : Prototype
    {
        if (!SSLGame.UseECS)
        {
            Log($"Attempted to register {yaml.Type} Entity {yaml.Handle} without ECS enabled!", LOG.SYSTEM_WARNING);
            return;
        }

        Type derivedType = typeof(T);

        // WIP: Use type and get from proto list

        if (typeof(Prototype).IsAssignableFrom(derivedType))
        {
            // Assumes that the yaml.handle has been sanitized of spare "...Prototype" or "...Entity" naming.
            if (TryGetTypeHandle(derivedType, out string typeHandle) && yaml.Handle.Equals(typeHandle))
            {
                Register(yaml, derivedType);
            }
        }
        else
        {
            Log("Unknown type for registration", LOG.FILE_ERROR);
        }
    }

    /// <summary>
    /// Creates copyable entity template from a provided Yaml file, and Template type. Also handles raw entities
    /// via a boolean toggle. Assumes templating by default.
    /// </summary>
    /// <param name="yaml">Yaml instance to process.</param>
    /// <param name="derivedType">Assumed derived type from EntityTemplate</param>
    /// <typeparam name="TYaml">Yaml Class</typeparam>
    /// <exception cref="YamlEmitterException">Thrown when ReferenceId / Handle not provided in YAML.</exception>
    public static void Register<TYaml>(TYaml yaml, Type derivedType) where TYaml : Prototype
    {
        if (!SSLGame.UseECS)
        {
            Log($"Called Register for {yaml.Type} Entity {yaml.Handle} without initializing Entity Manager!",
                LOG.SYSTEM_WARNING);
            return;
        }

        // Build components. All entity registration carries forth the task of parsing component data from a yaml file.
        var components = BuildComponentsFromYaml(yaml);

        // Raw entities are instantiated and casted. Create instance dynamically
        object? instance = Activator.CreateInstance(derivedType, yaml, components /*constructor parameters*/);

        // Cast to the derived type
        var entityObject = Convert.ChangeType(instance, derivedType);
        if (entityObject is not Entity entity)
            throw new YamlEmitterException("Entity created was not of expected type!");

        // Tag 'em.
        entity.Source = yaml.Source;
        Prototype output = entity;

        RegisterPrototype(output);
    }

    /// <summary>
    /// Helper for extracting components from a yaml file. Should work with any kind that inherits <see cref="Prototype"/>.
    /// Does NOT support other yaml types that implement this. This is for the ECS ONLY
    /// </summary>
    private static Dictionary<Type, object> BuildComponentsFromYaml(Prototype yaml)
    {
        var components = new Dictionary<Type, object>();

        if (yaml.YamlComponents == null)
        {
            yaml.YamlComponents = [];
            return components;
        }

        foreach (ComponentYaml yamlComponent in yaml.YamlComponents)
        {
            if (!ComponentRegistry.RegisteredComponentTypesDictionary
                    .TryGetValue(yamlComponent.Type.Replace("Component", string.Empty), out Type? componentType))
            {
                Log($"Unknown component type: {yamlComponent.Type}", LOG.FILE_WARNING);
                continue;
            }

            object component = Activator.CreateInstance(componentType)
                               ?? throw new InvalidOperationException(
                                   $"Cannot create {componentType.Name} in {nameof(BuildComponentsFromYaml)}");

            // Handle component variables.
            foreach (var field in yamlComponent.Fields)
            {
                PropertyInfo? property = componentType.GetProperty(field.Key,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                // If the property can't be written to, then why bother.
                if (property?.CanWrite != true)
                    continue;

                try
                {
                    var converted = Convert.ChangeType(field.Value, property.PropertyType);
                    property.SetValue(component, converted);
                }
                catch
                {
                    Log($"Failed to change type {field.Key} on {componentType.Name}", LOG.FILE_WARNING);
                }
            }

            components[componentType] = component; // Override.
        }

        return components;
    }

    /// <summary>
    /// Register an entity Definition raw or template according to <see cref="Prototype"/>.
    /// </summary>
    /// <remarks>
    /// Automatically registers definitions as a "source:handle" arrangement.
    /// </remarks>
    internal static void RegisterPrototype(Prototype definition)
        => _loadedGamePrototypes[definition.GetUniqueInternalRef()] = definition;

    /// <summary>
    /// Safe[r] TryGet method to retrieve an Entity Definition *OR* Template using a reference id.
    /// </summary>
    /// <returns>True if a template was found. False if one was not. The output is also Null if one was not found.</returns>
    public static bool TryGetPrototype<T>(string handle, out T? definition) where T : Prototype
    {
        var gotValue = ContainsDefinition(handle);
        if (LoadedGamePrototypes[handle] is T found)
        {
            definition = found;
            return true;
        }

        definition = null;
        return gotValue;
    }

    /// <summary>
    /// Inquiry to the entity manager for a possible entity definition.
    /// </summary>
    /// <param name="handle">Full Source:Handle ID that the Entity Registry definitions should possess.</param>
    /// <returns>True if a template was found. False if one was not.</returns>
    public static bool ContainsPrototype(string handle) => LoadedGamePrototypes.ContainsKey(handle);
}