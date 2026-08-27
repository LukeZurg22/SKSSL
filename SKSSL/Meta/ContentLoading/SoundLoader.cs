using System.Collections.Generic;
using System.IO;
using SKSSL.Extensions;
using SKSSL.Serializing;
using SKSSL.Sound;

namespace SKSSL;

// https://github.com/videolan/libvlcsharp

/// <summary>
/// Loader for sound files.
/// </summary>
public class SoundLoader() : IGameLoader(".wav", ".mp3", ".opus", ".ogg")
{
    /*
     NOTES:
        Short sounds wav
        Opus requires a C library, but is great for music and for voice. .ogg is a good replacer.
        Matroska (.mka) is a more modern container for Audio. Mkv is for video.
        https://github.com/dmitrykolchev/OpusSharp
        Use stream decoding for opus / ogg.
        Assets are typically loaded into memory when they‘re first used and freed when the last handle is dropped.
        “Core” assets that see constant use (like basic UI stuff) can be lost into memory.
        Give it a path, it immediately returns a handle, it internally checks if it is already loaded,
            and if not it will load the asset into memory.
     */

    /// <summary>
    /// Load all sounds in a directory where it belongs; the <see cref="SoundManager"/>.
    /// </summary>
    /// <param name="directory"></param>
    public override void Load(string directory)
    {
        SoundManager manager = SSLGame.SoundManager;

        // Include the root sound directory itself.
        HandleFolder(directory, directory, manager);

        // This accommodates nested material folders for whatever reason the user has for nesting them.
        var subfolders = Directory.EnumerateDirectories
            (directory, "*", SearchOption.AllDirectories);

        // Process all folders inside sounds folder. Each folder may have its own meta file.
        foreach (string folder in subfolders)
            HandleFolder(folder, directory, manager);
    }

    private void HandleFolder(string folder, string root, SoundManager manager)
    {
        // Get the meta file.
        string metaPath = Path.Combine(folder, "meta.yml");
        Dictionary<Handle, string> handleToFile = [];

        // Loop over sounds and process them to then re-iterate over later.
        foreach (string file in GetFiles(folder, SearchOption.TopDirectoryOnly))
        {
            var handle = Handle.CreateFromRelativeRoot(root, file);
            handleToFile.Add(handle, file);
        }

        // The metadata entries are given priority. This reduces the dictionary once registrations are done.
        foreach (SoundMetadata metadataEntry in SoundMetadata.Load(metaPath))
        {
            // Get sound handle.
            var sound = new Handle(metadataEntry.Sound.NormalizePath());
            string category = metadataEntry.Category;
            ProcessMetaEntry(sound, category, parent: null, @explicit: null);

            // If no children... who cares?! Onto the next entry.
            if (metadataEntry.Variants == null || metadataEntry.Variants.Count == 0)
                continue;

            // Process each of the children. By the time this is called, there is ALWAYS a parent!
            // Huzzah for no race conditions!
            foreach (string variant in metadataEntry.Variants)
                ProcessMetaEntry(new Handle(variant.NormalizePath()), category, parent: sound, @explicit: null);
        }

        // Iterate over the remaining entries of the dictionary. 
        // These are files without any categorization or special handling annotated in a meta file.
        // Lesser programs would throw an exception here. Behold: the power of making terrible assumptions!
        foreach ((Handle sound, string? file) in handleToFile)
        {
            // Use the relative folder path as a category!
            ProcessMetaEntry(
                sound,
                category: Path.GetRelativePath(root, folder),
                parent: null,
                file
            );
        }

        return;

        void ProcessMetaEntry(string sound, string? category, Handle? parent, string? @explicit)
        {
            var handle = new Handle(sound.NormalizePath());
            if (string.IsNullOrEmpty(@explicit))
            {
                if (!handleToFile.TryGetValue(handle, out var file))
                {
                    Log($"Invalid meta-data file reference sound field value \'{sound}\'.",
                        LOG.FILE_ERROR);
                    return;
                }

                // Register sound. Child sounds are handled dynamically by the provision of a parent.
                manager.RegisterSound(
                    handle: handle,
                    info: new FileInfo(file),
                    parent: parent,
                    category: category
                );

                // Remove from the list. There may still be entries.
                handleToFile.Remove(handle);
                return;
            }

            // From here, its assumed the file explicitly provided.
            manager.RegisterSound(
                handle: handle,
                info: new FileInfo(@explicit),
                parent: parent,
                category: category
            );
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global // Used instantiated by Deserializer.
    public class SoundMetadata
    {
        // ReSharper disable once UnassignedField.Global
        public string Sound;

        // ReSharper disable once UnassignedField.Global
        /// Assign handles as dedicated child variants to this sound.
        public List<string>? Variants;

        // ReSharper disable once UnassignedField.Global
        public string Category;

        public static List<SoundMetadata> Load(string metadataPath)
        {
            if (!File.Exists(metadataPath))
                return [];

            var text = File.ReadAllText(metadataPath);
            // Attempt to deserialize a list of Sound MetaData entries, avoiding a crash entirely.
            try
            {
                return new SerializerDefaultYaml().Deserialize<List<SoundMetadata>>(text);
            }
            catch
            {
                Log($"Failed to deserialize sound metadata file \'{metadataPath}\'. You did it wrong.", LOG.FILE_ERROR);
                return [];
            }
        }
    }
    // -
}