using VYaml.Annotations;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace SKSSL.YAML;

/// Structure for component information contained in YAML files.
[YamlObject]
public partial class ComponentYaml
{
    // e.g., "RenderableComponent" but named "Renderable"; it's stripped of the "Component" suffix.
    [YamlMember(name: "type")] public string Type { get; set; }

    // Dictionary for flexible fields (for varied components)
    /// <summary>
    /// Variable Fields contained in the record that defines the component.
    /// Private code will require provided component documentation for user-defined entities.
    /// </summary>
    /// <remarks>
    /// It's funky. Field names should be about as 1:1 to the actual component's fields.
    /// As far as I know, it's case sensitive.
    /// </remarks>
    public Dictionary<string, object> Fields { get; set; } = new();
}