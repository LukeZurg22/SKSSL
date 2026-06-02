using System.Text.Json.Serialization;
using YamlDotNet.Serialization;


// ReSharper disable VirtualMemberCallInConstructor

// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable UnusedType.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable NullableWarningSuppressionIsUsed
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SKSSL.ECS;

/// <summary>
/// Common abstraction for <see cref="Entity"/> and <see cref="Prototype"/> objects.
/// Allows ECS to store one of either type in its definitions, depending on use-case.
/// (De)Serializable data type read from YAML files. Further entries that inherit this may have optional parameters
/// implemented either through Nullable&lt;T&gt; variables, or variables with default provided values.
/// <code>
/// Yaml Entry Example:
/// - type: (string)
///   id: (string)
///   name: (string)
///   description (string)
/// </code>
/// </summary>
//[YamlObjectUnion("!Entity", typeof(Entity))]
public partial record Prototype
{
    /// Game content directory key to reverse-trace where this yaml prototype originated.
    [YamlIgnore]
    public virtual string Source { get; set; }

    [YamlMember(Alias = "parent"), JsonInclude, JsonPropertyName("Parent")]
    public string? Parent;
    
    /// Explicit type definition for this entry. For direct raw-serialization of entities.
    /// Completely unused if prioritizing yaml templates.
    [YamlMember(Alias = "type"), JsonInclude]
    public virtual string Type { get; set; } = "Prototype";

    /// Definition's Reference ID to later refer-to when making copies.
    /// Searchable, indexable ID. Virtual for possible nullability change in child classes.
    [YamlMember(Alias = "id")]
    public virtual string Handle { get; set; }
    
    /// <summary>
    /// Internal categorization of this yaml entry. Split into parts:<br/>
    /// 1. Directory (dictated by the folder where this was found)<br/>
    /// 2. Reference ID (also provided in-file)
    /// </summary>
    /// <returns>"<see cref="Source"/>:<see cref="Handle"/>"</returns>
    /// <returns>Fully-justified handle combining source, and short handle.</returns>
    public string GetUniqueInternalRef(string? key = null)
        => $"{key ?? Source}:{Handle}";

    /// Blank constructor for Common Entity root. Avoid using this unless absolutely necessary.
    /// Used for creating active <see cref="Entity"/> instances in the ECS, where properties are set elsewhere.
    [JsonConstructor]
    public Prototype()
    {
        // Auto-generate fallback source if not provided.
        if (string.IsNullOrEmpty(Source)) Source = "game";
    }

    /// Constructor for Entity Yaml basic fields and default components. This is for definitions.
    protected Prototype(Prototype yaml)
    {
        // ReSharper disable VirtualMemberCallInConstructor
        Handle = yaml.Handle;

        // Name and description may be absent / null, so handle them here.
        Type = yaml.Type;
    }
}
