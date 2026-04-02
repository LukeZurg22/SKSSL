using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using VYaml.Serialization;
using static SKSSL.DustLogger;

// ReSharper disable UnusedType.Global
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace SKSSL.YAML;

/// <summary>
/// Load all entries in YAML files based on provided types in BULK. Caches data when
/// loading specific folders dedicated to a set of YAML files that homogeneously share a data type.
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
public static partial class YamlLoader
{
    #region Serialization

    /// Serialize provided object and save to specific file path. Overrides existing file if present.
    public static void SerializeAndSave<T>(string path, T obj, bool @override = true) where T : class
    {
        var data = Serialize(obj);

        // Create directory if needed.
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // If overridden, write over. Otherwise, will create one if it doesn't exist.
        if (@override || !File.Exists(path))
            File.WriteAllText(path, data);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string Serialize<T>(T obj) where T : class => YamlSerializer.SerializeToString(obj);

    #endregion

    #region Loading (Single)

    /// Loads a file containing more than one entry of type T. Entries are a consecutive list beginning with '-' each.
    [Obsolete("This method is deprecated. Use LoadFile() instead.")]
    public static async Task<List<T>> LoadFileAsync<T>(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
            throw new FileNotFoundException("File not found", path);

        try
        {
            await using FileStream stream = File.OpenRead(path);
            var sample = await YamlSerializer.DeserializeAsync<List<T>>(stream);
            return sample;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"YAML deserialization failed for {path}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempt to extract yaml data with limited defined types.
    /// Entries are expected to begin with "- type: example"
    /// </summary>
    /// <returns>A filled, partially filled, or empty dictionary of expected types with their corresponding yaml blocks.</returns>
    private static void ExtractYamlData(string file, Type[] expectedTypes, out Dictionary<Type, List<object>> output)
    {
        // Read all lines, divide into blocks in accordance to expected types.
        var lines = File.ReadAllLines(file);
        var yamlBlocks = ConvertLinesToYamlBlocks(lines, expectedTypes, file);

        // Creating intermediate dictionary where yaml blocks are amalgamated together.
        var combined = expectedTypes.ToDictionary(type => type, _ => Array.Empty<byte>());
        CombineYamlBlockBytes(yamlBlocks, combined);
        output = FillDeserializedData(file, expectedTypes, combined);
    }

    #endregion

    #region Loading (Bulk)

    /// <summary>
    /// Gets all files in directory, searches every file, searches every in said file for provided type.
    /// If the first entry contains it, then an attempt to parse the entire file as a list is made.
    /// </summary>
    /// <param name="directory">Directory path to load.</param>
    /// <typeparam name="T">Entry type in files</typeparam>
    [Obsolete("Use the alternative non-generic LoadDirectory() instead.")]
    public static IEnumerable<T> LoadDirectory<T>(GameContentDirectory directory)
    {
        var extensions = new[] { ".yaml", ".yml" };

        // Loop over every YAML file.
        var files = Directory
            .GetFiles(path: directory.ContentDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)));

        foreach (var file in files)
        {
            string expectedTypeName = typeof(T).Name;

            // Regex breakdown:
            // .*?                  -> any characters (non-greedy) before "type"
            // type\s*:\s*          -> the keyword "type" (case-insensitive), colon, optional whitespace
            // (Base)?              -> optional "Base" prefix
            // ([A-Za-z0-9_]+)      -> capture group 2: the core type name (alphanumeric + _)
            // (Yaml)?              -> optional "Yaml" suffix
            // .*                   -> any characters after (we don't care)
            var regex = new Regex(
                @".*?type\s*:\s*(Base)?([A-Za-z0-9_]+)(Yaml)?.*",
                options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            bool typeFound = false;
            using var reader = new StreamReader(file);
            while (reader.ReadLine() is { } line)
            {
                Match hasType = regex.Match(line);
                if (!hasType.Success)
                    continue;
                string extractedTypeName = hasType.Groups[1].Value;

                if (!string.Equals(extractedTypeName, expectedTypeName, StringComparison.OrdinalIgnoreCase)) continue;
                typeFound = true;
                break;
            }

            if (!typeFound)
                continue;

            var yaml = LoadFileAsync<T>(file).Result;
            foreach (T entry in yaml)
                yield return entry;
        }
    }

    /// <summary>
    /// Searches a directory using provided type definitions and file patterns. Directory defaults to application's if
    /// not provided.
    /// </summary>
    public static Dictionary<Type, List<object>> LoadDirectory(Type[] types, string directory, params string[] patterns)
    {
        // Get all yaml files.
        var files = GetFiles(patterns, directory);

        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.
        var conglomerate = types.ToDictionary(type => type, _ => new List<object>());

        // Process every file with expected types.
        foreach (var file in files)
        {
            // Merging the file's conglomerate with our super conglomerate.
            var output = LoadFile(types, file);
            foreach ((Type type, var yamlData) in output)
            {
                // Tag each yaml entry with source.
                yamlData.ForEach(yamlEntry => ((BaseYamlEntry)yamlEntry).Source = Path.GetFileName(directory));
                conglomerate[type] = conglomerate[type].Concat(yamlData).ToList();
            }
        }

        return conglomerate;
    }


    /// <summary>
    /// Searches a directory using provided type definitions and file patterns. Directory defaults to application's if
    /// not provided.
    /// </summary>
    public static Dictionary<Type, List<object>> LoadFile(Type[] types, string file)
    {
        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.
        var conglomerate = types.ToDictionary(type => type, _ => new List<object>());
        if (!File.Exists(file))
        {
            Log($"File not found from file path {file}, it's being skipped entirely!");
            return [];
        }

        try
        {
            // Get file output and put to dictionary.
            ExtractYamlData(file, types, out var output);
            foreach ((Type type, var entries) in output)
                conglomerate[type].AddRange(entries);
        }
        catch (Exception ex)
        {
            Log($"{ex.Message} :: {ex.InnerException?.Message}", LOG.FILE_ERROR);
        }

        return conglomerate;
    }

    #endregion

    #region Helpers

    [GeneratedRegex(@"\btype\s*:\s*(Base)?([A-Za-z0-9_]+)(Yaml)?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RegexSpaceTypeBaseYaml();

    /// <summary>
    /// Returns a distinct set of file paths matching the given patterns, optionally restricted to a base directory.
    /// </summary>
    /// <param name="patterns">File patterns (e.g., "*.cs", "src/**/*.txt", "logs/error.log")</param>
    /// <param name="directory">Optional base-directory to resolve relative patterns against. If null, uses current directory.</param>
    /// <returns>Distinct file paths (case-insensitive comparison on Windows)</returns>
    public static IEnumerable<string> GetFiles(IEnumerable<string> patterns, string directory = "")
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Force base directory to current directory of application if none is provided.
        if (string.IsNullOrWhiteSpace(directory))
            directory = Directory.GetCurrentDirectory();

        foreach (var pattern in patterns)
        {
            string dir;
            string searchPattern;

            // If the pattern is an absolute path, use it directly
            if (Path.IsPathRooted(pattern))
            {
                dir = Path.GetDirectoryName(pattern) ?? directory;
                searchPattern = Path.GetFileName(pattern);
            }
            else
            {
                // Relative pattern: resolve against baseDirectory
                dir = Path.Combine(directory, Path.GetDirectoryName(pattern) ?? "");
                searchPattern = Path.GetFileName(pattern);
            }

            // Ensure the directory is normalized and exists
            if (Directory.Exists(dir))
                files.UnionWith(Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories));
        }

        return files;
    }


    /// Reads all lines in a file, and parses them into blocks.
    public static List<IYamlBlock> ConvertLinesToYamlBlocks(string[] lines, Type[] expectedTypes, string? file = null)
    {
        var entries = new List<IYamlBlock>();
        file ??= ""; // For reverse-tracing files.

        // Text contained in the block, separated into individual lines for parsing.
        StringBuilder blockTextBuilder = new();
        int linesRead = 0;
        Type? previousType = null;
        string previousTag = "";

        // For every line, if it begins with a '-' starting marker, it is the sign of a new block.
        var index = 0;
        for (; index < lines.Length; index++)
        {
            if (string.IsNullOrEmpty(lines[index])) continue; // Skip empty lines.
            string line = lines[index].Replace("\r", "").Replace("\n", ""); // Clean of Environment new-line characters.

            // Every new '-' primary entry begins a "store and reset"
            if (IsTopLevelEntryStart(line))
            {
                string text = blockTextBuilder.ToString();

                // Add block. ">0" avoids an edge-case wherein it's the start of the file.
                if (linesRead > 0)
                    entries.Add(new IYamlBlock(previousType, previousTag, text, Path.GetFileName(file), index));

                // Conduct a reset.
                blockTextBuilder = new StringBuilder();
                previousTag = OutType(line, out Type? type);
                previousType = type;
                linesRead = 0;
            }

            // Add the current line
            blockTextBuilder.AppendLine(line);
            linesRead++;
        }

        // If there are no more lines, but lines have been read, output the remainder as a Yaml Block.
        if (linesRead > 0)
        {
            entries.Add(new IYamlBlock(previousType, previousTag, blockTextBuilder.ToString(), file, linesRead));
        }

        return entries;

        // Spits out a Type expected from a line containing it as a tag.
        string OutType(string line, out Type? type)
        {
            // Extract "- type:" tag
            var typeTag = ExtractTypeTag(line) ?? "";

            // Short-circuits
            if (string.IsNullOrEmpty(typeTag))
            {
                type = null;
                return typeTag;
            }

            // Strip any "Base...Yaml" [pre]/[suf]fixes.
            typeTag = StripBaseAndYaml(typeTag);

            // Find which known type matches the tag.
            Type? targetType = expectedTypes.FirstOrDefault
                (type => string.Equals(StripBaseAndYaml(type.Name), typeTag, StringComparison.OrdinalIgnoreCase));
            type = targetType;
            return typeTag;
        }

        // Remove "Base" from the beginning of a name, and "Yaml" from the end, if present.
        string StripBaseAndYaml(string name)
        {
            if (name.StartsWith("Base", StringComparison.OrdinalIgnoreCase))
                name = name[4..];
            if (name.EndsWith("Yaml", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            return name;
        }

        // Extracts type tag from line.
        string? ExtractTypeTag(string line)
        {
            Match match = RegexSpaceTypeBaseYaml().Match(line);
            return match.Success switch
            {
                false => null,
                true => match.Groups[2].Value // core name
            };
        }

        // Helper Method to check if this is top-level entry
        bool IsTopLevelEntryStart(string line)
        {
            // The line must start with '-' at column 0 (only whitespace before is OK, but typically none)
            //  Also skips leading whitespace.
            int i = 0;
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;

            // Must be exactly at the start (i == 0) and begin with '-', followed by space or end
            if (i >= line.Length || i != 0) return false; // Ensures that any indentation = not top-level

            // Start of the line must begin with '-'
            if (line[i] != '-') return false;

            // Require space after '-' (remove this to allow "-type: x")
            return 1 >= line.Length || char.IsWhiteSpace(line[1]);
        }
    }

    private static Dictionary<Type, List<object>> FillDeserializedData(
        string file, Type[] expectedTypes, Dictionary<Type, byte[]> combined)
    {
        // Assign proper types to a new dictionary to organize all the different flavors of files.
        // Because provided types are static, and that yaml blocks are later verified,
        //  this should guarantee that a list within the dictionary is available for all types.
        var yamlDict = expectedTypes.ToDictionary(type => type, _ => new List<object>());

        // For every combined pairing, deserialize.
        foreach (var combinedKVP in combined)
        {
            try
            {
                var deserializedTypeList = DeserializeBytesAsListOfType(combinedKVP.Value, combinedKVP.Key, file);
                if (deserializedTypeList == null)
                {
                    // Do NOT throw an error here, as this particular deserialized list may not have been found in the
                    //  file to begin with!
                    continue;
                }

                // Iterate through the list and fill the output.
                foreach (var yamlObject in (IEnumerable)deserializedTypeList)
                {
                    yamlDict[combinedKVP.Key].Add(Convert.ChangeType(yamlObject, combinedKVP.Key));
                }
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Failed to deserialize {combinedKVP.Key.Name} type from \"{Path.GetFileName(file)}\".", ex);
            }
        }

        return yamlDict;
    }

    private static void CombineYamlBlockBytes(List<IYamlBlock> yamlBlocks, Dictionary<Type, byte[]> combined)
    {
        // For every IYamlBlock that happens to have a valid type defined within it...
        foreach (IYamlBlock block in yamlBlocks)
        {
            // Skip blocks whose text begin with '#', because this is a comment. Comments are ignored!
            if (block.Text.StartsWith('#')) continue;
            if (block.Type == null)
            {
                // Short-circuit. Type is resolved during block parsing.
                // IYamlBlock contains the list of expected types.
                Log(
                    $"{(string.IsNullOrWhiteSpace(block.Tag) ? "missing" : block.Tag)} tag is invalid on line {block.Index} " +
                    $"in file {block.File}", LOG.FILE_ERROR);
                continue;
            }

            // Get the bytes of the block and using the type, combined the bytes with the existing ones to effectively
            //  merge the yaml entries into one.
            var bytes = block.ToBytes();
            combined[block.Type] = combined[block.Type].Concat(bytes).ToArray();
        }
    }

    /// Helper method used to deserialize bytes as a list of an element type.
    /// Requires the DeserializeListOf method to remain exactly as it is, as this converts a type parameter
    ///  into a generic one.
    private static object? DeserializeBytesAsListOfType(byte[] bytes, Type genericType, string file)
    {
        Type listType = typeof(List<>).MakeGenericType(genericType);
        MethodInfo? method = typeof(YamlSerializer).GetMethod(
            "Deserialize",
            1,
            BindingFlags.Static | BindingFlags.Public,
            null,
            [typeof(ReadOnlyMemory<byte>), typeof(YamlSerializerOptions)],
            null
        );
        if (method == null)
            throw new EntryPointNotFoundException(
                $"Failed to create Deserializer method in SKSSL Yaml Loader. Likely library issue. caused in {file}");

        MethodInfo closed = method.MakeGenericMethod(listType);

        try
        {
            return closed.Invoke(null, [new ReadOnlyMemory<byte>(bytes), null]);
        }
        catch (Exception ex)
        {
            Log($"Fatal error in {nameof(DeserializeBytesAsListOfType)} call!\n" +
                $"Check class-type changes, invalid spacing, and values. Casted to list of {genericType} in {file}.\n" +
                $"{ex.InnerException?.Message}", LOG.SYSTEM_ERROR);
        }
        // TODO: Add fallback attempt.

        return null;
    }

    #endregion
}

/// A block of YAML text data that represents a single entry in a file, which is assumed to be a list of blocks.
public readonly record struct IYamlBlock(Type? Type, string Tag, string Text, string File, int Index)
{
    /// Explicit representation of Type in Assembly that this block represents.
    public readonly Type? Type = Type;

    /// Type Tag that the block represents.
    public readonly string Tag = Tag;

    /// Text contained in the block.
    public readonly string Text = Text;

    /// Text contained in the block.
    public readonly string File = File;

    /// Index in the file that which this is defined .
    public readonly int Index = Index;

    /// Convert Text contained in this block to Bytes with [not] provided encoding.
    public byte[] ToBytes(Encoding? encoding = null)
        => encoding == null ? Encoding.UTF8.GetBytes(Text) : encoding.GetBytes(Text);

    /// Returns Block <see cref="Text"/>.
    public override string ToString() => Text;
}