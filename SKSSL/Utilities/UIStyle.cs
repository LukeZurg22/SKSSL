using System.Collections.Generic;
using System.Drawing;
using RenderingLibrary.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace SKSSL.Utilities;

using System.Xml.Serialization;

[XmlRoot("root", Namespace = "http://jetbrains.com/rider/schemas/resx")]
public class ResxRoot
{
    [XmlElement("UIStyle")] public List<UIStyle> Entries { get; set; }
}

[XmlRoot("UIStyle")]
public class UIStyle
{
    [XmlAttribute("Key", Namespace = "http://jetbrains.com/rider/schemas/resx")]
    public string Key { get; set; }

    [XmlAttribute("Foreground")] public string ColorHexStringForeground { get; set; }
    [XmlAttribute("Background")] public string ColorHexStringBackground { get; set; }

    [XmlIgnore]
    public Color Foreground
    {
        get => FromHex(ColorHexStringForeground);
        set => ColorHexStringForeground = value.ToString();
    }
    
    [XmlIgnore]
    public Color Background
    {
        get => FromHex(ColorHexStringBackground);
        set => ColorHexStringBackground = value.ToString();
    }

    public static UIStyle Default()
    {
        var style = new UIStyle
        {
            ColorHexStringBackground = "#FFFFFFFF",
            Background = Color.White,
            Foreground = Color.Black,
            Key = "default-background"
        };
        return style;
    }
    
    public static Color FromHex(string HEX) => ColorTranslator.FromHtml(HEX).ToXNA();
}