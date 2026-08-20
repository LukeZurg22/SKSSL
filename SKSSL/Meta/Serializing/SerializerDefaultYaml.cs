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
using static YamlDotNet.Serialization.DefaultValuesHandling;

// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable MemberCanBeProtected.Global

namespace SKSSL.Serializing;

/// <summary>
/// Load all entries in YAML files based on provided types in BULK. Caches data when
/// loading specific folders dedicated to a set of YAML files that homogeneously share a data type.
/// Override the [De]Serializer fields when the Loader itself is enough, but types with unique parsing
/// exceptions must be inserted.
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
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class SerializerDefaultYaml : ISerializer
{
    // FROM THE SOL.KOM. PROJECT.

    private static readonly YamlDotNet.Serialization.ISerializer DefaultSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(OmitNull | OmitDefaults | OmitEmptyCollections)
        .WithTypeConverter(new LocIdYamlConverter())
        .WithTypeConverter(new HandleYamlConverter())
        .WithTypeConverter(new FileInfoYamlConverter(GameDirectory.RootDirectory))
        .Build();

    /// YAML Serializer.
    public virtual YamlDotNet.Serialization.ISerializer Serializer => DefaultSerializer;

    // ReSharper disable once RedundantNameQualifier
    private static readonly YamlDotNet.Serialization.IDeserializer DefaultDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new LocIdYamlConverter())
        .WithTypeConverter(new HandleYamlConverter())
        .WithTypeConverter(new FileInfoYamlConverter(GameDirectory.RootDirectory))
        .Build();

    /// YAML Deserializer.
    public virtual IDeserializer Deserializer => DefaultDeserializer;

    /// <summary>
    /// Serializes an object as either itself, or a list of its provided type.
    /// </summary>
    /// <returns>Serialized form of Object for YAML file save.</returns>
    /// <remarks>Forces object to list of itself for serialization.</remarks>
    // ReSharper disable once UnusedMember.Global
    public T Deserialize<T>(string serialized) => Deserializer.Deserialize<T>(serialized);

    /// <summary>
    /// Serializes an object as either itself, or a list of its provided type.
    /// </summary>
    /// <returns>Serialized form of Object for YAML file save.</returns>
    /// <remarks>Forces object to list of itself for serialization.</remarks>
    // TEMP: I am worried this will not handle multiple types very well!
    public string Serialize<T>(T obj) where T : class => Serializer.Serialize(obj);

    public List<Prototype> DeserializePrototypes(string text, string trace = "", params Type[] types)
    {
        // Split up the text into lines.
        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.

        // Read all lines, divide into blocks in accordance to expected types.
        var yamlBlocks = ConvertLinesToYamlBlocks(lines, types, trace);

        // Combine yaml blocks of shared declared-types.
        var combined = CombineYamlBlocks(yamlBlocks);

        // Deserialize each set of blocks as a list of their corresponding types.
        return DeserializeFillData(combined, trace);
    }

    #region Helpers

    /// Reads all lines in a string, and parses them into yaml-blocks.
    private static List<YamlBlock> ConvertLinesToYamlBlocks(string[] lines, Type[] expectedTypes, string? file = null)
    {
        int blockStartLine = 0;
        var entries = new List<YamlBlock>();
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
                    var block = new YamlBlock(
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
            entries.Add(new YamlBlock(previousType, previousTag, blockTextBuilder.ToString(), file, linesRead));
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
    private List<Prototype> DeserializeFillData(Dictionary<Type, StringBuilder> combined, string fileTrace)
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
        object? DeserializeAsListOfType(Type genericType, string text)
        {
            Type listType = typeof(List<>).MakeGenericType(genericType);
            var result = Deserializer.Deserialize(text, listType);
            return result;
        }
    }


    private static Dictionary<Type, StringBuilder> CombineYamlBlocks(List<YamlBlock> yamlBlocks)
    {
        var combined = new Dictionary<Type, StringBuilder>();

        foreach (YamlBlock block in yamlBlocks)
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