using System.Drawing;
using RenderingLibrary.Graphics;
using YamlDotNet.Serialization;
using Color = Microsoft.Xna.Framework.Color;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SKSSL.YAML;

/// <summary>
/// (De)Serializable data type read from YAML files.
/// <code>
/// Yaml Entry Example:
/// - type: (string)
///   id: (localization)
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
    public string Id { get; set; }

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