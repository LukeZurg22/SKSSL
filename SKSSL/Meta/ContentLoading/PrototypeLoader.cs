using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SKSSL.ECS;
using SKSSL.ECS.Registry;
using SKSSL.Serializing;

namespace SKSSL;

//@formatter:off
public class PrototypeLoader<TSerializer>(params string[] extensions) : IGameLoader(extensions) where TSerializer : class, ISerializer, new()
{
    //@formatter:on
    private readonly TSerializer Serializer = new();

    /// Using prototype data collected from a Game Directory's provided Prototypes folder, ten load into registries.
    /// Load multiple files containing identical types in a directory.
    /// Generally expects all files in the directory to be the same type. Can be overwritten for custom logic.
    public override void Load(string directory)
    {
        // Get all yaml files.
        var files = GetFiles(directory);

        // Forces-load in bulk from all prototype definitions.
        var types = MasterRegistryManager.RegisteredGameRegistryTypes;

        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.
        // --> Update: The Conglomerate list was removed. This note is left here for nostalgic reasons only.

        // Process every file with expected types.
        foreach (var file in files)
        {
            // Merging the file's conglomerate with our super conglomerate.
            var text = File.ReadAllText(file);
            var prototypes = Deserialize(text, file, types.ToArray());
            foreach (Prototype prototype in prototypes)
            {
                // Forces only the support of defined types within the system. Since all types have a handle, and that
                //  every prototype has an explicitly-referenced handle, that handle is used as a reference.
                if (!MasterRegistryManager.TryRegisterPrototype(prototype))
                    Log($"Unsupported type {prototype.Type} found in {file}.", LOG.FILE_ERROR);
            }
        }
    }

    /// Serialize provided object and save to specific file path. Overrides existing file if present.
    // ReSharper disable once UnusedMember.Global
    public void SerializeAndSave<T>(string path, T obj, bool @override = true) where T : class
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

    public string Serialize<T>(T obj) where T : class
    {
        // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (obj == null)
            return "";

        return IsCollection(obj) && obj is not string
            ? Serializer.Serialize(obj)
            : Serializer.Serialize(new List<T> { obj });

        // Helper method - Clean way to detect collections
        bool IsCollection(object? ding) => ding switch
        {
            null or string => false,
            Array _ => true, // Catches T[]
            IList _ => true, // Catches List<T>, IList<T>, etc.
            IEnumerable _ => true,
            _ => false
        };
    }

    public List<Prototype> Deserialize(string text, string fileTrace = "", params Type[] types)
    {
        // Additional fallback. Typically for testing.
        if (types.Length == 0)
            types = MasterRegistryManager.RegisteredGameRegistryTypes.ToArray();

        return Serializer.DeserializePrototypes(text, fileTrace, types);
    }
}