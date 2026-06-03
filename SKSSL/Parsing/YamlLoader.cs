using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SKSSL.ECS;
using SKSSL.Utilities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
    private static readonly ISerializer SKSSLDefaultSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .JsonCompatible()
        .Build();

    private static readonly IDeserializer SKSSLDefaultDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    #region Loading

    /// Using prototype data collected from elsewhere, load into registry.
    public static void LoadGameDirectory(GameDirectory gameDirectory)
    {
        if (gameDirectory.PrototypesFolder == null)
            return;
        var prototypes = DeserializeDirectory(gameDirectory.PrototypesFolder);
        
        // TODO: add some safety padding here for overrides?
        
        foreach (var prototypeList in prototypes.Values)
        foreach (Prototype prototype in prototypeList)
        {
            PrototypeRegistry.RegisterPrototype(prototype);
        }
    }

    #endregion

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
        //return IsCollection(obj) && obj is not string
        //    ? YamlSerializer.SerializeToString(obj, SerializerOptions)
        //    : YamlSerializer.SerializeToString(new List<T> { obj }, SerializerOptions);

        // WARN: THIS SUFFICIENT. THIS IS TEMPORARY WHILST SERIALIZING TO STRING IS BEING RESORTED
        return "";

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

    #region Deserialization

    /// <summary>
    /// Searches a directory using provided type definitions and file patterns. Directory defaults to application's if
    /// not provided.
    /// </summary>
    public static Dictionary<Type, List<Prototype>> DeserializeDirectory(string directory, params string[] patterns)
    {
        // Get all yaml files.
        var files = GetFiles(patterns, directory);

        // Forces-load in bulk from all prototype definitions.
        var types = PrototypeRegistry.Definitions.Values.ToArray();

        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.
        var conglomerate = types.ToDictionary(type => type, _ => new List<Prototype>());

        // Process every file with expected types.
        foreach (var file in files)
        {
            // Merging the file's conglomerate with our super conglomerate.
            var prototypes = DeserializeFile(file, types);
            foreach (var prototype in prototypes)
            {
                var type = PrototypeRegistry.Definitions[prototype.Type];
                conglomerate[type].Add(prototype);
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
    public static List<Prototype> DeserializeFile<T>(string file) => DeserializeFile(file, typeof(T));

    /// <summary>
    /// Searches a directory using provided type definitions and file patterns. Directory defaults to application's if
    /// not provided.
    /// </summary>
    public static List<Prototype> DeserializeFile(string file, params Type[] types)
    {
        // Supported types are gotten from Source Generators' output to PrototypeManager, now!
        if (types.Length == 0)
            types = PrototypeRegistry.Definitions.Values.ToArray();
        if (File.Exists(file))
            return DeserializePrototypesFrom(File.ReadAllLines(file), file, types);
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
    public static List<Prototype> DeserializePrototypesFrom(
        string[] text, string fileTrace = "",
        Type[]? types = null)
    {
        // Using source generators to their fullest effectiveness, here!
        if (types is null || types.Length == 0) types = PrototypeRegistry.Definitions.Values.ToArray();

        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.

        // Read all lines, divide into blocks in accordance to expected types.
        var yamlBlocks = ConvertLinesToYamlBlocks(text, types, fileTrace);

        // Combine yaml blocks of shared declared-types.
        var combined = CombineYamlBlocks(yamlBlocks);

        // Deserialize each set of blocks as a list of their corresponding types.
        var result = DeserializeFillData(combined, fileTrace);
        
        return result;
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
        int blockStartLine = 0;
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
            string line = lines[index].TrimEnd('\r', '\n');

            // Every new '-' primary entry begins a "store and reset"
            if (IsTopLevelEntryStart(line))
            {
                if (linesRead > 0)
                {
                    var block = new IYamlBlock(
                        previousType,
                        previousTag,
                        blockTextBuilder.ToString(),
                        Path.GetFileName(file),
                        blockStartLine);

                    entries.Add(block);
                }

                blockTextBuilder.Clear();
                blockStartLine = index;

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
    private static List<Prototype> DeserializeFillData(Dictionary<Type, StringBuilder> combined, string fileTrace)
    {
        // Assign proper types to a new dictionary to organize all the different flavors of files.
        // Because provided types are static, and that yaml blocks are later verified,
        //  this should guarantee that a list within the dictionary is available for all types.
        var yamlDict = new List<Prototype>();

        // For every combined pairing, deserialize.
        foreach ((Type type, StringBuilder text) in combined)
        {
            object? deserializedTypeList = null;
            try
            {
                deserializedTypeList = DeserializeAsListOfType(type, text.ToString());
            }
            catch (Exception ex)
            {
                Log($"Failed to deserialize {type.Name} type from \"{Path.GetFileName(fileTrace)}\". {ex.Message}",
                    LOG.FILE_ERROR);
            }

            if (deserializedTypeList == null)
            {
                // Do NOT throw an error here, as this particular deserialized list may not have been found in the
                //  file to begin with!
                continue;
            }

            // Iterate through the list and fill the output.
            foreach (var yamlObject in (IEnumerable)deserializedTypeList)
            {
                yamlDict.Add((Prototype)Convert.ChangeType(yamlObject, type));
            }
        }

        return yamlDict;

        // Helper method used to deserialize bytes as a list of an element type.
        // Requires the DeserializeListOf method to remain exactly as it is, as this converts a type parameter
        //  into a generic one.
        static object? DeserializeAsListOfType(Type genericType, string text)
        {
            Type listType = typeof(List<>).MakeGenericType(genericType);
            var result = SKSSLDefaultDeserializer.Deserialize(text, listType);
            return result;
        }
    }


    private static Dictionary<Type, StringBuilder> CombineYamlBlocks(List<IYamlBlock> yamlBlocks)
    {
        var combined = new Dictionary<Type, StringBuilder>();

        foreach (IYamlBlock block in yamlBlocks)
        {
            if (block.Text.StartsWith('#')) continue;
            if (block.Type == null)
            {
                Log(
                    $"{(string.IsNullOrWhiteSpace(block.Tag) ? "missing" : block.Tag)} tag is invalid on line {block.Index} " +
                    $"in file {block.File}",
                    LOG.FILE_ERROR);
                continue;
            }

            if (!combined.TryGetValue(block.Type, out StringBuilder? sb))
            {
                sb = new StringBuilder();
                combined[block.Type] = sb;
            }

            sb.AppendLine(block.Text);
        }

        return combined;
    }

    #endregion
}