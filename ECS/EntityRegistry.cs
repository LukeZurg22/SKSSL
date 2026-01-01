using System.Reflection;
using SKSSL.YAML;
using static SKSSL.DustLogger;

namespace SKSSL.ECS;

public static class EntityRegistry
{
    private static readonly Dictionary<string, EntityTemplate> _definitions = new();
    public static IReadOnlyDictionary<string, EntityTemplate> Definitions => _definitions;

    public static void RegisterTemplate(EntityYaml yaml)
    {
        Dictionary<Type, object> components = new();
        foreach (ComponentYaml yamlComponent in yaml.Components)
        {
            var cleanTypeId = yamlComponent.Type.Replace("Component", string.Empty);
            // Get component from registry.
            if (!ComponentRegistry._registeredComponents.TryGetValue(cleanTypeId, out Type? componentType))
            {
                Log($"Unknown component type: {yamlComponent.Type}", LOG.FILE_WARNING);
                continue;
            }

            // Create default instance of the component.
            object component = Activator.CreateInstance(componentType)
                               ?? throw new InvalidOperationException($"Cannot create {componentType.Name}");

            // Apply fields from YAML using reflection (simple & flexible)
            foreach (var field in yamlComponent.Fields)
            {
                PropertyInfo? property = componentType.GetProperty(field.Key,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property == null || !property.CanWrite)
                    continue; // If null or can't write, then short-circuit.
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

            // Logging because yknow, this is bad but i don't want to crash the program.
            if (components.ContainsKey(componentType))
                Log($"Entity definition {yaml.ReferenceId} contains more than one instance of {componentType.Name}! Overriding previous definition!",
                    LOG.FILE_WARNING);

            // Override, for safety.
            components[componentType] = component;
        }

        var template = new EntityTemplate(yaml, defaultComponents: components);

        if (string.IsNullOrEmpty(template.ReferenceId))
            throw new ArgumentException("Template must have ReferenceId");

        RegisterTemplate(template);
    }

    /// <summary>
    /// Register a template.
    /// </summary>
    private static void RegisterTemplate(EntityTemplate template) => _definitions[template.ReferenceId] = template;

    /// <summary>
    /// Retrieves a template from the defined templates list.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when template not found using provided reference id.</exception>
    public static EntityTemplate GetTemplate(string referenceId)
    {
        if (!_definitions.TryGetValue(referenceId, out EntityTemplate template))
            throw new KeyNotFoundException($"No template registered for '{referenceId}'");

        return template;
    }

    /// <summary>
    /// Safe TryGet method to safely retrieve a template.
    /// </summary>
    public static bool TryGetTemplate(string referenceId, out EntityTemplate template)
        => _definitions.TryGetValue(referenceId, out template!);
}