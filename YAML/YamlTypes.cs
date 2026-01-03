using System.Drawing;
using RenderingLibrary.Graphics;
using YamlDotNet.Serialization;
using Color = Microsoft.Xna.Framework.Color;
// ReSharper disable PropertyCanBeMadeInitOnly.Global

// ReSharper disable NullableWarningSuppressionIsUsed

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SKSSL.YAML;

/// <summary>
/// (De)Serializable data type read from YAML files. Further entries that inherit this may have optional parameters
/// implemented either through Nullable&lt;T&gt; variables, or variables with default provided values.
/// <code>
/// Yaml Entry Example:
/// - type: (string)
///   id: (string)
/// </code>
/// </summary>
public record BaseYamlEntry
{
    /// <summary>
    /// Explicit type definition for this entry.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Searchable, indexable ID.
    /// </summary>
    [YamlMember(Alias = "id")]
    public string ReferenceId { get; set; }
}

/// <summary>
/// <inheritdoc cref="BaseYamlEntry"/>
/// <code>
/// In Addition To:
///   name: (localization)
///   description: (localization)
/// </code>
/// </summary>
public record BaseLocalizedYamlEntry : BaseYamlEntry
{
    /// <summary>
    /// UNLOCALIZED name. Localization should be implemented elsewhere.
    /// </summary>
    public string Name { get; set; }

    public string Description { get; set; }
}

/// <summary>
/// <inheritdoc cref="BaseLocalizedYamlEntry"/>
/// <code>
/// In Addition To:
///   color: "#RRGGBB"
/// </code>
/// </summary>
public record BaseLocalizedColorableYamlEntry : BaseLocalizedYamlEntry
{
    /// <summary>
    /// Raw HTML (#RRGGBB) color when viewed on the map or in graphs.
    /// </summary>
    [YamlMember(Alias = "color")]
    public string YamlColor { get; set; }

    private Color? _color;

    [YamlIgnore]
    public Color Color
    {
        get
        {
            _color ??= ColorTranslator.FromHtml(YamlColor).ToXNA();
            return _color.Value;
        }
        set
        {
            YamlColor = value.ToString();
            _color = value;
        }
    }
}

public class ComponentYaml
{
    // e.g., "RenderableComponent" but named "Renderable"; it's stripped of the "Component" suffix.
    [YamlMember(Alias = "type")] public string Type { get; set; }

    // Dictionary for flexible fields (for varied components)
    public Dictionary<string, object> Fields { get; set; } = new();
}

/// <summary>
/// <inheritdoc cref="BaseLocalizedYamlEntry"/>
/// <code>
/// In Addition To:
///   components: (Component Yaml Entries)
///     - type: (string)
///       field_1: (varies)
///       field_2: (varies)
///       field_3: (varies)
/// Component fields vary between component type.
/// </code>
/// </summary>
public record EntityYaml : BaseLocalizedYamlEntry
{
    /// <summary>
    /// Optional field for <see cref="EntityYaml"/> which is exclusively for entities.
    /// </summary>
    [YamlMember(Alias = "components")]
    public List<ComponentYaml> Components { get; set; } = [];
}