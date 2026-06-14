using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SKSSL.ECS;
using SKSSL.ECS.Registry;

namespace SKSSL;

public abstract class PrototypeLoader : IGameLoader
{
    /// Using prototype data collected from a Game Directory's provided Prototypes folder, ten load into registries.
    /// Load multiple files containing identical types in a directory.
    /// Generally expects all files in the directory to be the same type. Can be overwritten for custom logic.
    public override void Load(string directory)
    {
        if (!SSLGame.Config.UseECS)
        {
            Log($"Cannot load prototypes from {directory} folder. ECS is not Enabled!", LOG.SYSTEM_WARNING);
            return;
        }

        // Get all yaml files.
        var files = GetFiles(directory);

        // Forces-load in bulk from all prototype definitions.
        var types = MasterRegistryManager.RegisteredGameProtoTypes;

        // "You can tell its conglomerate- because it's everywhere!"
        // All yaml entries sharing types between files are stored here. All supported types are instantiated wholesale.
        // Files should -not- have a type defined within them outside the ones passed through here. If one somehow
        //  gets passed, it's probably because of a test.
        var conglomerate = types.ToDictionary(type => type, _ => new List<Prototype>());

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
                if (MasterRegistryManager.TryGetRegisteredTypeDefinition(prototype.Type, out Type type))
                    conglomerate[type].Add(prototype);
                else // Logging for tracing.
                    Log($"Unsupported type {prototype.Type} found in {file}.", LOG.FILE_ERROR);
            }
        }


        foreach (var protoDict in conglomerate)
        foreach (Prototype prototype in protoDict.Value)
        {
            MasterRegistryManager.RegisterLoadedPrototype(protoDict.Key, prototype);
        }
    }

    /// Serialize provided object and save to specific file path. Overrides existing file if present.
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
            ? SerializeLogicImplement(obj)
            : SerializeLogicImplement(new List<T> { obj });

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

    /// <summary>
    /// The developer-implemented interim layer that handles the [de]serialization logic.
    /// </summary>
    /// <param name="obj">Some object which may or may not be a list.</param>
    /// <typeparam name="T">The type the object is supposed to represent, or be a list of represented.</typeparam>
    /// <returns>A serialized string of the object.</returns>
    protected abstract string SerializeLogicImplement<T>(T obj) where T : class;

    public List<Prototype> Deserialize(string text, string fileTrace = "", params Type[] types)
    {
        // Additional fallback. Typically for testing.
        if (types.Length == 0)
            types = MasterRegistryManager.RegisteredGameProtoTypes.ToArray();
        return DeserializeLogicImplement(text, fileTrace, types);
    }

    /// <summary>
    /// User-Developer implementation for deserialization logic for game content.
    /// </summary>
    /// <param name="text">Text to deserialize.</param>
    /// <param name="trace">Filepath passthrough for tracing.</param>
    /// <param name="types">Explicit types for conversions. (Optional)</param>
    /// <returns></returns>
    protected abstract List<Prototype> DeserializeLogicImplement(string text, string trace = "", params Type[] types);
}