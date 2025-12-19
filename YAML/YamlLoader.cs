using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SKSSL.YAML;

/// <summary>
/// Solely handles the deserialization and loading of Y[A]ML file data. Processing is up to whatever calls load.
/// According to the code, this expects 100% peak perfect YAML matchups with the provided type. Modding will be great,
/// except when there is a change to the structure.
/// </summary>
public static class YamlLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
    
    /// <summary>
    /// Loads YAML files from a folder or a single file and returns a list of deserialized objects.
    /// </summary>
    public static IEnumerable<T> Load<T>(string folderOrFile, Action<T>? postProcess = null)
    {
        var files = Directory.Exists(folderOrFile)
            ? Directory.GetFiles(folderOrFile, "*.*", SearchOption.AllDirectories)
            : [folderOrFile];

        foreach (var file in files)
        {
            using var reader = new StreamReader(file);
            var items = Deserializer.Deserialize<List<T>>(reader);
            foreach (T item in items)
            {
                postProcess?.Invoke(item);
                yield return item;
            }
        }
    }

    /// <summary>
    /// Loads YAML into a dictionary keyed by a provided ID selector.
    /// </summary>
    public static Dictionary<TKey, TValue> LoadDictionary<TKey, TValue>(
        string folderOrFile,
        Func<TValue, TKey> keySelector,
        Action<TValue>? postProcess = null) where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>();
        foreach (TValue item in Load(folderOrFile, postProcess))
        {
            TKey key = keySelector(item);
            dict[key] = item; // overwrite duplicates silently
        }
        return dict;
    }

    /// <summary>
    /// Handles the dynamic loading of Type1 and Type2 types, which the latter may reference back to Type1.
    /// Both instances must begin with the "type" keyword, annotated by "typeAnnoX".
    /// </summary>
    /// <param name="handleFunction"/>
    /// <param name="yamlText">Filepath to the text being parsed.</param>
    /// <param name="typeAnno1">Type annotation for group entry. (Ex: racial_group)</param>
    /// <param name="typeAnno2">Type annotation for subversive entry. (Ex: race)</param>
    /// <param name="typeAnno2Plural">Plural of type2 annotation subversive entry. (Ex: raceS)</param>
    /// <typeparam name="Type1">Contains a list of Type2.</typeparam>
    /// <typeparam name="Type2">Has a pointer to Type1, but is isolated in its own instances.</typeparam>
    public static void LoadMixedContainers<Type1, Type2>(
        string yamlText, string typeAnno1, string typeAnno2, string typeAnno2Plural,
        Action<Type2, Type1?> handleFunction)
        where Type1 : class
        where Type2 : class
    {
        var entries = Deserializer.Deserialize<List<Dictionary<string, object>>>(yamlText);
        foreach (var entry in entries)
        {
            if (!entry.TryGetValue("type", out var typeObj))
                continue;

            var type = typeObj.ToString();

            switch (type)
            {
                // (Example: if racial group)
                case var _ when type == typeAnno1:
                {
                    // Serialize dictionary to YAML string first
                    string yamlFragment = Serializer.Serialize(entry);
                    var group = Deserializer.Deserialize<Type1>(yamlFragment);

                    // Get contained instances
                    PropertyInfo? prop =typeof(Type1).GetProperty(typeAnno2Plural, 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                    // Handling property without concern for case.
                    if (prop != null && prop.GetValue(group) is IEnumerable<Type2> type2InstancesProp)
                        foreach (Type2 type2Instance in type2InstancesProp)
                            handleFunction(type2Instance, group);

                    break;
                }
                // (Example: if race entry)
                case var _ when type == typeAnno2:
                {
                    string yamlFragment = Serializer.Serialize(entry);
                    var type2Instance = Deserializer.Deserialize<Type2>(yamlFragment);
                    
                    // Run function but exclaim that the thing meant to contain it, is null.
                    handleFunction(type2Instance, null);
                    break;
                }
            }
        }
    }
}