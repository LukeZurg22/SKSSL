using System.Reflection;
using SKSSL.YAML;
using YamlDotNet.Core;
using static SKSSL.DustLogger;

namespace SKSSL.ECS;

public static class EntityRegistry
{
    private static readonly Dictionary<string, EntityTemplate> _definitions = new();
    public static IReadOnlyDictionary<string, EntityTemplate> Definitions => _definitions;

    /// <summary>
    /// Calls <see cref="RegisterTemplate{TYaml, TTemplate}"/> with a default to the <see cref="EntityYaml"/> type.
    /// </summary>
    /// <param name="yaml">The yaml file of the template.</param>
    /// <typeparam name="T">Type of template being registered. Forces inheritance.</typeparam>
    /// <exception cref="ArgumentException"></exception>
    public static void RegisterTemplate<T>(EntityYaml yaml) where T : EntityTemplate
        => RegisterTemplate<EntityYaml, T>(yaml);

    /// <summary>
    /// Creates copyable entity template from a provided Yaml file, and Template type.
    /// </summary>
    /// <param name="yaml"></param>
    /// <typeparam name="TYaml"></typeparam>
    /// <typeparam name="TTemplate"></typeparam>
    /// <exception cref="YamlException"></exception>
    public static void RegisterTemplate<TYaml, TTemplate>(TYaml yaml)
        where TYaml : EntityYaml
        where TTemplate : EntityTemplate
    {
        // Get components.
        var components = BuildComponentsFromYaml(yaml);

        // Call dynamic constructors instead.
        var template = EntityTemplate.CreateFromYaml<TTemplate>(yaml, components);

        if (string.IsNullOrEmpty(template.ReferenceId))
            throw new YamlException("Template must have ReferenceId");

        RegisterTemplate(template);
    }

    /// <summary>
    /// Helper for extracting components from a yaml file. Should work with any kind that inherits <see cref="EntityYaml"/>.
    /// Does NOT support other yaml types that implement this. This is for the ECS ONLY
    /// </summary>
    private static Dictionary<Type, object> BuildComponentsFromYaml(EntityYaml yaml)
    {
        var components = new Dictionary<Type, object>();

        foreach (ComponentYaml yamlComponent in yaml.Components)
        {
            var cleanTypeId = yamlComponent.Type.Replace("Component", string.Empty);

            if (!ComponentRegistry._registeredComponents.TryGetValue(cleanTypeId, out Type? componentType))
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

                if (property?.CanWrite != true) continue;

                try
                {
                    var converted = Convert.ChangeType(field.Value, property.PropertyType);
                    property.SetValue(component, converted);
                }
                catch
                {
                    Log($"Failed to set {field.Key} on {componentType.Name}", LOG.FILE_WARNING);
                }
            }

            components[componentType] = component; // Override.
        }

        return components;
    }

    /// <summary>
    /// Register a template.
    /// </summary>
    private static void RegisterTemplate(EntityTemplate template) => _definitions[template.ReferenceId] = template;

    /// <summary>
    /// Retrieves a template from the defined templates list. Throws an exception.
    /// <remarks>I suggest using <see cref="TryGetTemplate"/> instead and add additional handling for safety.</remarks>
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when template not found using provided reference id.</exception>
    public static EntityTemplate GetTemplate(string referenceId)
    {
        if (!_definitions.TryGetValue(referenceId, out EntityTemplate? template))
            throw new KeyNotFoundException(
                $"Call on {nameof(GetTemplate)} found no template for reference id: {referenceId}");

        return template;
    }

    /// <summary>
    /// Safe[r] TryGet method to retrieve a template using a reference id.
    /// </summary>
    /// <returns>True if a template was found. False if one was not. The output is also Null if one was not found.</returns>
    public static bool TryGetTemplate(string referenceId, out EntityTemplate? template)
        => _definitions.TryGetValue(referenceId, out template);
}