using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities;

public static class StyleSheet
{
    public static readonly List<UIStyle> UIColorSheet = [];

    public static readonly string DefaultFilePath = Path.Combine(Environment.CurrentDirectory, "styles.xml");

    private static readonly XmlSerializer Serializer = new(typeof(ResxRoot));

    /// <summary>
    /// Add Color style to stored inner dictionary for safekeeping.
    /// </summary>
    /// <example>AddStyle("ui-button-green")</example>
    public static void AddStyle(string name, UIStyle style)
    {
        switch (UIColorSheet.Any(d => d.Key.Equals(name)))
        {
            case false:
                UIColorSheet.Add(style);
                break;
            default:
                Log($"Attempted to add existing UI Style {name} to style dictionary.", LOG.SYSTEM_WARNING);
                break;
        }
    }

    public static UIStyle GetStyle(string name) =>
        UIColorSheet.Any(d => d.Key.Equals(name))
            ? UIColorSheet.First(d => d.Key.Equals(name))
            : UIStyle.Default();

    public static void SaveStyles()
    {
        using var writer = new StreamWriter(DefaultFilePath);
        Serializer.Serialize(writer, UIColorSheet);
    }

    public static void LoadStyles()
    {
        if (!File.Exists(DefaultFilePath))
        {
            File.Create(DefaultFilePath).Close();
            return;
        }

        UIColorSheet.Clear();
        using var reader = new StreamReader(DefaultFilePath);
        var root = Serializer.Deserialize(reader) as ResxRoot;
        if (root?.Entries == null) return;
        foreach (UIStyle entry in root.Entries)
            AddStyle(entry.Key, entry);
    }
}