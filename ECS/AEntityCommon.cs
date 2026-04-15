using System.Text.Json.Serialization;
using SKSSL.Localization;
using SKSSL.YAML;
using VYaml.Annotations;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace SKSSL.ECS;

/// <summary>
/// Common abstraction for <see cref="SKEntity"/> and <see cref="EntityTemplate"/> objects.
/// Allows ECS to store one of either type in its definitions, depending on use-case.
/// </summary>
[YamlObject]
[YamlObjectUnion("!SKEntity", typeof(SKEntity))]
public abstract partial record AEntityCommon
{
    [YamlIgnore]
    public string Source { get; set; }
    
    /// <summary>
    /// For direct raw-serialization of entities. Completely unused if prioritizing yaml templates.
    /// </summary>
    [YamlMember(name: "type"), JsonInclude]
    public virtual string RawType { get; set; }
    
    /// <summary>
    /// Definition's Reference ID to later refer-to when making copies.
    /// </summary>
    [YamlMember(name: "id"), JsonInclude]
    public abstract string Handle { get; init; }

    /// <summary>
    /// Localization for name.
    /// </summary>
    [YamlMember(name: "name"), JsonInclude]
    public abstract string NameKey { get; set; }

    /// <summary>
    /// Localization for description.
    /// </summary>
    [YamlMember(name: "description"), JsonInclude]
    public abstract string DescriptionKey { get; set; }

    /// <returns>Localized name from Name Key.</returns>
    public void GetName() => Loc.Get(NameKey);

    /// <returns>Localized Description from Description Key.</returns>
    public void GetDescription() => Loc.Get(DescriptionKey);
    
    /// <returns>Fully-justified handle combining source, and short handle.</returns>
    public string GetFullHandle() => $"{Source}:{Handle}";

    /// Predefined class-specific dictionary of components.
    public abstract IReadOnlyDictionary<Type, object> DefaultComponents { get; init; }

    /// Constructor for Entity Yaml basic fields and default components. This is for definitions.
    protected AEntityCommon(EntityYaml yaml, IReadOnlyDictionary<Type, object> components)
    {
        // ReSharper disable VirtualMemberCallInConstructor
        Handle = yaml.Handle;
        
        // Name and description may be absent / null, so handle them here.
        NameKey = yaml.Name ?? "unknown";
        DescriptionKey = yaml.Description ?? "unknown";
        
        DefaultComponents = components;
    }

    /// Blank constructor for Common Entity root. Avoid using this unless absolutely necessary.
    /// Used for creating active <see cref="SKEntity"/> instances in the ECS, where properties are set elsewhere.
    [JsonConstructor]
    protected AEntityCommon()
    {
        var type = GetType().Name.Replace("Entity", "");
        
        // Auto-generate fallback handle if not provided.
        if (string.IsNullOrEmpty(Handle)) Handle = type.ToLower().ToLower();
        
        // Auto-generate fallback source if not provided.
        if (string.IsNullOrEmpty(Source)) Source = "game";
        
    }
}