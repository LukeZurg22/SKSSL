using System.Reflection;
using SKSSL.YAML;
using VYaml.Emitter;
using static SKSSL.DustLogger;

namespace SKSSL.ECS;

///<summary>
/// Registry of all Entity Definitions. Called by <see cref="EntityManager"/>.
/// <seealso cref="ComponentRegistry"/>
/// </summary>
public abstract class EntityRegistry
{
    internal static readonly Dictionary<string, Prototype> Definitions = new();

    /// <inheritdoc cref="RegisterEntity{T, EntityYaml}"/>
    public static void RegisterEntity<T>(Prototype yaml) where T : Entity => RegisterEntity<T, Prototype>(yaml);

    /// <summary>
    /// Handles registration of entity ambiguously between Entity and EntityTemplate derivations.
    /// </summary>
    /// <remarks>
    /// Provided that the Derived Type T is an EntityTemplate or Entity, will either call a direct  
    /// Calls <see cref="RegisterEntity{TYaml}(TYaml,System.Type,bool)"/>.
    /// When registering specialized templates, use <see cref="RegisterEntity{TYaml}(TYaml,System.Type,bool)"/> instead.
    /// </remarks>
    /// <param name="yaml">The yaml file of the template.</param>
    /// <typeparam name="T">Derived Type of entity intermediate type registered. Forces inheritance.</typeparam>
    /// <typeparam name="Y"></typeparam>
    public static void RegisterEntity<T, Y>(Y yaml) where T : Entity where Y : Prototype
    {
        if (!SSLGame.UseECS)
        {
            Log($"Attempted to register {yaml.Type} Entity {yaml.Handle} without initializing Entity Manager!",
                LOG.SYSTEM_WARNING);
            return;
        }

        Type derivedType = typeof(T);

        // Register raw entity
        if (typeof(Entity).IsAssignableFrom(derivedType))
        {
            RegisterEntity(yaml, derivedType, true);
        }
        // Attempt register of template
        else if (typeof(Prototype).IsAssignableFrom(derivedType))
        {
            RegisterEntity(yaml, derivedType, false);
        }
        else
        {
            throw new InvalidOperationException("Unknown type for registration");
        }
    }

    /// <summary>
    /// Creates copyable entity template from a provided Yaml file, and Template type. Also handles raw entities
    /// via a boolean toggle. Assumes templating by default.
    /// </summary>
    /// <param name="yaml">Yaml instance to process.</param>
    /// <param name="derivedType">Assumed derived type from EntityTemplate</param>
    /// <param name="isRawEntity">Assumed false by default. Toggles alternative handling for raw entity definitions.</param>
    /// <typeparam name="TYaml">Yaml Class</typeparam>
    /// <exception cref="YamlEmitterException">Thrown when ReferenceId / Handle not provided in YAML.</exception>
    public static void RegisterEntity<TYaml>(TYaml yaml, Type derivedType, bool isRawEntity) where TYaml : Prototype
    {
        if (!SSLGame.UseECS)
        {
            Log($"Called Register for {yaml.Type} Entity {yaml.Handle} without initializing Entity Manager!",
                LOG.SYSTEM_WARNING);
            return;
        }

        // Build components. All entity registration carries forth the task of parsing component data from a yaml file.
        var components = BuildComponentsFromEntityYaml(yaml);

        Prototype output;
        // Raw entities are instantiated and casted.
        if (isRawEntity)
        {
            // Create instance dynamically
            object? instance = Activator.CreateInstance(derivedType, yaml, components /*constructor parameters*/);

            // Cast to the derived type
            var entityObject = Convert.ChangeType(instance, derivedType);
            if (entityObject is not Entity entity)
                throw new YamlEmitterException("Entity created was not of expected type!");

            // Tag 'em.
            entity.Source = yaml.Source;
            output = entity;
        }
        // Templates are constructed and the yaml is passed-through.
        else
        {
            // Get dynamic template constructor.
            ConstructorInfo? ctor = derivedType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: [yaml.GetType(), components.GetType()],
                modifiers: null);
            if (ctor == null)
                throw new InvalidOperationException($"Type {derivedType} has no matching constructor.");

            // Call constructor
            var templateObj = ctor.Invoke([yaml, components]);

            if (templateObj is not Prototype template)
                throw new YamlEmitterException("Created template is not an EntityTemplate!");

            if (string.IsNullOrEmpty(template.Handle))
                throw new YamlEmitterException("Invalid template!");

            // Tag 'em.
            template.Source = yaml.Source;
            output = template;
        }

        RegisterDefinition(output);
    }

    /// <summary>
    /// Helper for extracting components from a yaml file. Should work with any kind that inherits <see cref="Prototype"/>.
    /// Does NOT support other yaml types that implement this. This is for the ECS ONLY
    /// </summary>
    private static Dictionary<Type, object> BuildComponentsFromEntityYaml(Prototype yaml)
    {
        var components = new Dictionary<Type, object>();

        if (yaml.Components == null)
        {
            yaml.Components = [];
            return components;
        }

        foreach (ComponentYaml yamlComponent in yaml.Components)
        {
            if (!ComponentRegistry.RegisteredComponentTypesDictionary
                    .TryGetValue(yamlComponent.Type.Replace("Component", string.Empty), out Type? componentType))
            {
                Log($"Unknown component type: {yamlComponent.Type}", LOG.FILE_WARNING);
                continue;
            }

            object component = Activator.CreateInstance(componentType)
                               ?? throw new InvalidOperationException(
                                   $"Cannot create {componentType.Name} in {nameof(BuildComponentsFromEntityYaml)}");

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
    internal static void RegisterDefinition(Prototype definition)
        => Definitions[definition.GetUniqueInternalRef()] = definition;

    /// <summary>
    /// Safe[r] TryGet method to retrieve an Entity Definition *OR* Template using a reference id.
    /// </summary>
    /// <returns>True if a template was found. False if one was not. The output is also Null if one was not found.</returns>
    public static bool TryGetDefinition<T>(string handle, out T? definition) where T : Prototype
    {
        var gotValue = ContainsDefinition(handle);
        if (EntityManager.Definitions[handle] is T found)
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
    public static bool ContainsDefinition(string handle) => EntityManager.Definitions.ContainsKey(handle);
}