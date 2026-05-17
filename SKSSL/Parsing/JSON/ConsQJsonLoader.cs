using System.Text.Json;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.Parsing.JSON;

/// <summary>
/// Lifted from "Consequaintances", this is an incredibly simple loader for Json files
/// using system <see cref="System.Text.Json"/>.
/// </summary>
public class ConsQJsonLoader
{
    /// Save object as JSON.
    public static void Save<T>(string directory, string fileName, T obj)
    {
        // Ensure path exists.
        string fullDir = Path.GetFullPath(directory);
        Directory.CreateDirectory(fullDir);

        // Finally write to file.
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(fullDir, fileName), json);
    }


    /// Load multiple files containing identical types in a directory.
    /// Expects all files in the directory to be the same general type.
    public static List<T> LoadDirectory<T>(string directory)
    {
        var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
        List<T> result = [];
        foreach (var file in files)
        {
            var obj = LoadAmbiguous<T>(file);
            result.AddRange(obj);
        }

        return result;
    }

    /// <summary>
    /// Loads json file at path and returns a list of objects, even if there is only one entry.
    /// </summary>
    public static List<T> LoadAmbiguous<T>(string path)
    {
        var json = File.ReadAllText(path);
        using JsonDocument doc = JsonDocument.Parse(json);
        List<T> objects = [];
        switch (doc.RootElement.ValueKind)
        {
            case JsonValueKind.Array:
            {
                var list = Load<List<T>>(json);
                if (list != null)
                    objects = list;
                break;
            }
            case JsonValueKind.Object:
            {
                var entry = Load<T>(json);
                if (entry != null)
                    objects.Add(entry);
                break;
            }
        }

        return objects;
    }

    private static T? Load<T>(string json) => JsonSerializer.Deserialize<T>(json);
}