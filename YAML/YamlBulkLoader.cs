using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static SKSSL.DustLogger;

namespace SKSSL.YAML;

/// <summary>
/// Load all entries in YAML files based on provided types in BULK. Caches data, and is less efficient than
/// using the standard <see cref="YamlLoader"/> in return for more convenient loading. Use the <see cref="YamlLoader"/>
/// if you are loading specific folders dedicated to a set of YAML files that all share a data type homogeneously.
/// <example><code>
/// var types = new[] { typeof(YamlTypeA), typeof(YamlTypeB), typeof(YamlTypeC) };
/// var allData = YamlLoader.LoadAllTypes(types, path); // Supports ".../**/*.yaml"
/// var typeAs = allData[typeof(YamlTypeA)].Cast&lt;YamlTypeA&gt;();
/// var typeBs = allData[typeof(YamlTypeB)].Cast&lt;YamlTypeB&gt;();
/// // Files read only ONCE
/// </code></example>
/// <example><code>
/// var typeAs = YamlLoader.LoadAll&lt;YamlTypeA&gt;(path); // Supports ".../**/*.yaml"
/// var typeBs = YamlLoader.LoadAll&lt;YamlTypeB&gt;(path); // Uses cache
/// // Files read once per type, cached afterward
/// </code></example>
/// </summary>
public static partial class YamlBulkLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly Regex TypeRegex = RegexSpaceTypeBaseYaml();

    // Cache deserialized entries per type (optional, for repeated queries)
    private static readonly Dictionary<Type, object> _cache = new();

    /// <summary>
    /// Loads all entries of type T from the given file patterns. Files are read once.
    /// </summary>
    public static List<T> LoadAll<T>(params string[] filePatterns) where T : class
    {
        var result = GetAllMatching<T>(filePatterns);
        return result.ToList(); // Return copy if you want immutability
    }

    /// <summary>
    /// Reads files once and dispatches entries to correct types.
    /// </summary>
    public static Dictionary<Type, List<object>> LoadAllTypes(
        IEnumerable<Type> knownTypes, string? specificPath = null, params string[] filePatterns)
    {
        var types = knownTypes as Type[] ?? knownTypes.ToArray();
        var results = types.ToDictionary(t => t, _ => new List<object>());

        var files = GetFiles(filePatterns, specificPath);

        foreach (var file in files)
        {
            string[] lines = File.ReadAllLines(file);
            var entries = SplitIntoYamlEntries(lines);

            foreach (var entryLines in entries)
            {
                // WARN: The ExtractTypeTag below limits the parser to only one type per file.
                //  A file CANNOT have mixed types, despite that being the initial intention. This isn't super game-breaking,
                //  But it IS an issue.
                string? typeTag = ExtractTypeTag(entryLines);
                if (typeTag == null) continue;

                string coreName = StripBaseAndYaml(typeTag);

                // Find which known type matches this core name
                Type? targetType = types.FirstOrDefault(t =>
                    string.Equals(StripBaseAndYaml(t.Name), coreName, StringComparison.OrdinalIgnoreCase));

                if (targetType == null) continue;
                string yamlBlock = string.Join("\n", entryLines);
                try
                {
                    // Always deserialize as a list – handles single or multiple entries, with or without '-'
                    Type listType = typeof(List<>).MakeGenericType(targetType);
                    var list = _deserializer.Deserialize(yamlBlock, listType);

                    if (list is IEnumerable<object> items)
                        foreach (var item in items)
                            results[targetType].Add(item);
                }
                catch (Exception ex)
                {
                    Log($"Failed to deserialize {typeTag} in {file}: {ex.Message}", LOG.FILE_ERROR);
                }
            }
        }

        return results;
    }

    #region YAML Data Parsing Helpers
    
    // Helper used by LoadAll<T> for efficiency when calling multiple times
    private static IEnumerable<T> GetAllMatching<T>(string[] filePatterns) where T : class
    {
        Type targetType = typeof(T);
        if (_cache.TryGetValue(targetType, out var cached))
            return (List<T>)cached;

        var files = GetFiles(filePatterns);
        var list = new List<T>();
        string expectedCore = StripBaseAndYaml(targetType.Name);

        foreach (var file in files)
        {
            string[] lines = File.ReadAllLines(file);
            var entries = SplitIntoYamlEntries(lines);

            foreach (var entryLines in entries)
            {
                string? typeTag = ExtractTypeTag(entryLines);
                if (typeTag == null || !string.Equals(StripBaseAndYaml(typeTag), expectedCore,
                        StringComparison.OrdinalIgnoreCase))
                    continue; // Short-circuit.
                string yamlBlock = string.Join("\n", entryLines);
                var obj = _deserializer.Deserialize<T>(yamlBlock);
                list.Add(obj);
            }
        }

        _cache[targetType] = list; // Cache for future calls
        return list;
    }

    private static string StripBaseAndYaml(string name)
    {
        if (name.StartsWith("Base", StringComparison.OrdinalIgnoreCase))
            name = name[4..];
        if (name.EndsWith("Yaml", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }

    private static string? ExtractTypeTag(string[] entryLines)
    {
        foreach (var line in entryLines)
        {
            Match match = TypeRegex.Match(line);
            if (match.Success)
                return match.Groups[2].Value; // The core name part
        }

        return null;
    }

    private static List<string[]> SplitIntoYamlEntries(string[] lines)
    {
        var entries = new List<string[]>();
        var current = new List<string>();

        foreach (var line in lines)
        {
            if (IsTopLevelEntryStart(line) && current.Count > 0)
            {
                entries.Add(current.ToArray());
                current.Clear();
            }
            current.Add(line);
        }

        if (current.Count > 0)
            entries.Add(current.ToArray());

        return entries;
    }

    private static bool IsTopLevelEntryStart(string line)
    {
        // The line must start with '-' at column 0 (only whitespace before is OK, but typically none)
        // Skip leading whitespace
        int i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i]))
            i++;

        // Must be exactly at the start (i == 0) and begin with '-', followed by space or end
        if (i >= line.Length || i != 0) return false;  // Ensures that any indentation = not top-level

        if (line[i] != '-') return false;

        // Optional: require space after '-' (most common style)
        // Remove this check if you want to allow "-type: recipe" (no space)
        return i >= line.Length || char.IsWhiteSpace(line[i]);
    }

    #endregion

    /// <summary>
    /// Returns a distinct set of file paths matching the given patterns, optionally restricted to a base directory.
    /// </summary>
    /// <param name="patterns">File patterns (e.g., "*.cs", "src/**/*.txt", "logs/error.log")</param>
    /// <param name="baseDirectory">Optional base directory to resolve relative patterns against. If null, uses current directory.</param>
    /// <returns>Distinct file paths (case-insensitive comparison on Windows)</returns>
    private static IEnumerable<string> GetFiles(IEnumerable<string> patterns, string? baseDirectory = null)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        baseDirectory ??= Directory.GetCurrentDirectory();

        foreach (var pattern in patterns)
        {
            // If the pattern is an absolute path, use it directly
            string dir;
            string searchPattern;

            if (Path.IsPathRooted(pattern))
            {
                dir = Path.GetDirectoryName(pattern) ?? baseDirectory;
                searchPattern = Path.GetFileName(pattern);
            }
            else
            {
                // Relative pattern: resolve against baseDirectory
                dir = Path.Combine(baseDirectory, Path.GetDirectoryName(pattern) ?? "");
                searchPattern = Path.GetFileName(pattern);
            }

            // Ensure the directory is normalized and exists
            if (Directory.Exists(dir))
                files.UnionWith(Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories));
        }

        return files;
    }

    [GeneratedRegex(@"\btype\s*:\s*(Base)?([A-Za-z0-9_]+)(Yaml)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled,
        "en-US")]
    private static partial Regex RegexSpaceTypeBaseYaml();
}