using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using SKSSL.ECS;
using SKSSL.Utilities;
using VYaml.Serialization;
using static SKSSL.DustLogger;

// ReSharper disable UnusedMember.Global
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
    public static readonly YamlSerializerOptions SerializerOptions;

    static YamlLoader()
    {
        SerializerOptions = YamlSerializerOptions.Standard;

        // Omit properties that are null.
        SerializerOptions.DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull;

        SerializerOptions.Resolver = CompositeResolver.Create(
            formatters:
            [
                new YamlComponentFormatter()
            ],
            resolvers:
            [
                StandardResolver.Instance,
                new SKSSLYAMLResolver()
            ]
        );

        // Set naming convention to UpperCamelCase.
        //SerializerOptions.NamingConvention = NamingConvention.UpperCamelCase;
    }

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
    /// Serializes an object as either itself, or a list of its provided type.
    /// </summary>
    /// <returns>Serialized form of Object for YAML file save.</returns>
    /// <remarks>Forces object to list of itself for serialization.</remarks>
    public static string Serialize<T>(T obj) where T : class
    {
        // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (obj == null)
            return "";

        // Special handling for collections
        if (IsCollection(obj) && obj is not string)
        {
            return YamlSerializer.SerializeToString(obj, SerializerOptions);
        }
        else
        {
            return YamlSerializer.SerializeToString(new List<T> { obj }, SerializerOptions);
        }

        // Helper method - Clean way to detect collections
        bool IsCollection(object? ding) => ding switch
        {
            null or string => false,
            Array _ => true, // Catches T[]
            IList _ => true, // Catches List<T>, IList<T>, etc.
            IEnumerable _ => true,
            _ => false
        };
    } // TEMP: I am worried this will not handle multiple types very well!

    #endregion

    #region Loading

    /// <summary>
    /// Searches a directory using provided type definitions and file patterns. Directory defaults to application's if
    /// not provided.
    /// </summary>
    public static Dictionary<Type, List<object>> LoadDirectory(string directory, params string[] patterns)
    {
        // Get all yaml files.
        var files = GetFiles(patterns, directory);

        // Forces-load in bulk from all prototype definitions.
        var types = PrototypeRegistry.Definitions.Values.ToArray();

        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.
        var conglomerate = types.ToDictionary(type => type, _ => new List<object>());

        // Process every file with expected types.
        foreach (var file in files)
        {
            // Merging the file's conglomerate with our super conglomerate.
            var output = LoadFile(file, types);
            foreach ((Type type, var yamlData) in output)
            {
                // Tag each yaml entry with source.
                yamlData.ForEach(yamlEntry => ((Prototype)yamlEntry).Source = Path.GetFileName(directory));
                conglomerate[type] = conglomerate[type].Concat(yamlData).ToList();
            }
        }

        return conglomerate;
    }

    /// <summary>
    /// Overload for LoadFile call, feeding only one parameter in available types.
    /// </summary>
    /// <param name="file"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static Dictionary<Type, List<object>> LoadFile<T>(string file) => LoadFile(file, typeof(T));

    /// <summary>
    /// Searches a directory using provided type definitions and file patterns. Directory defaults to application's if
    /// not provided.
    /// </summary>
    public static Dictionary<Type, List<object>> LoadFile(string file, params Type[] types)
    {
        // Supported types are gotten from Source Generators' output to PrototypeManager, now!
        if (types.Length == 0)
            types = PrototypeRegistry.Definitions.Values.ToArray();
        if (File.Exists(file)) return ExtractYamlData(File.ReadAllLines(file), file, types);
        Log($"File not found from file path {file}, it's being skipped entirely!");
        return [];
    }

    /// <summary>
    /// Conglomerate and extract yaml data from text.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="fileTrace"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    public static Dictionary<Type, List<object>> ExtractYamlData(string[] text, string fileTrace = "",
        Type[]? types = null)
    {
        if (types is null || types.Length == 0) types = PrototypeRegistry.Definitions.Values.ToArray();

        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.
        var conglomerate = types.ToDictionary(type => type, _ => new List<object>());

        try
        {
            // Read all lines, divide into blocks in accordance to expected types.
            var yamlBlocks = ConvertLinesToYamlBlocks(text, types, fileTrace);

            // Creating intermediate dictionary where yaml blocks are amalgamated together.
            var combined = types.ToDictionary(type => type, _ => Array.Empty<byte>());
            CombineYamlBlockBytes(yamlBlocks, combined);
            var output = DeserializeFillData(types, combined, fileTrace);

            foreach ((Type type, var entries) in output)
                conglomerate[type].AddRange(entries); // Additive
        }
        catch (Exception ex)
        {
            Log($"{ex.Message} :: {ex.InnerException?.Message}", LOG.FILE_ERROR);
        }

        return conglomerate;
    }

    #endregion

    #region Helpers

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


    /// Reads all lines in a string, and parses them into yaml-blocks.
    private static List<IYamlBlock> ConvertLinesToYamlBlocks(string[] lines, Type[] expectedTypes, string? file = null)
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
            Match match = CommonRegex.BaseYaml.Match(line);
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

    /// Deserialize a set of bytes per type, and fill the data into a dictionary for use elsewhere.
    private static Dictionary<Type, List<object>> DeserializeFillData(
        Type[] expectedTypes, Dictionary<Type, byte[]> combined, string fileTrace)
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
                var deserializedTypeList = DeserializeBytesAsListOfType(combinedKVP.Value, combinedKVP.Key, fileTrace);
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
                    $"Failed to deserialize {combinedKVP.Key.Name} type from \"{Path.GetFileName(fileTrace)}\".", ex);
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
    public static object? DeserializeBytesAsListOfType(byte[] bytes, Type genericType, string file = "")
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
                $"Failed to create Deserializer method in SKSSL Yaml Loader. Likely library issue in file {file}");

        MethodInfo closed = method.MakeGenericMethod(listType);

        try
        {
            return closed.Invoke(null, [new ReadOnlyMemory<byte>(bytes), null]);
        }
        catch (Exception ex)
        {
            Log($"Fatal error in {nameof(DeserializeBytesAsListOfType)} call!\n" +
                $"Check class-type changes, invalid spacing, and values. Casted to list of {genericType} in file {file}.\n" +
                $"{ex.InnerException?.Message}", LOG.SYSTEM_ERROR);
        }
        // TODO: Add fallback attempt.

        return null;
    }

    #endregion
}