using System;
using System.Drawing;
using RenderingLibrary.Graphics;
using VYaml.Annotations;
using Color = Microsoft.Xna.Framework.Color;

// ReSharper disable UnusedType.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable NullableWarningSuppressionIsUsed
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace SKSSL.YAML;

#pragma warning disable CS8618
/// <summary>
/// <inheritdoc cref="Prototype"/>
/// <code>
/// In Addition To:
///   color: "#RRGGBB"
/// </code>
/// </summary>
[YamlObject, Obsolete("Will be removed. Replace w. custom prototype definition!")]
public partial record ColorablePrototype : Prototype
{
    /// <summary>
    /// Raw HTML (#RRGGBB) color when viewed on the map or in graphs.
    /// </summary>
    [YamlMember(name: "color")]
    public string YamlColor { get; set; }

    private Color? _color;

    [YamlIgnore]
    public Color Color // TODO: Make this an optional thing.
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