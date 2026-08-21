using System.Collections.Generic;
using System.IO;
using SKSSL.Serializing;

namespace SKSSL;

/// <summary>
/// Loader for sound files.
/// </summary>
/// <reference>
/// https://www.radio42.com/bass/help/
/// </reference>
public class SoundLoader() : IGameLoader(".wav", ".mp3", ".opus", ".ogg")
{

    // WARN: Sound handle storage is per-loader. This is the same for textures. There is no central registry!!
    //  Textures and Sounds need some kind of central registry for those handles, and for mods to be able to override them.
    /*
Short sounds wav
Opus requires a C library, but is great for music and for voice. .ogg is a good replacer.
Matroska (.mka) is a more modern container for Audio. Mkv is for video.
https://github.com/dmitrykolchev/OpusSharp
Use stream decoding for opus / ogg.
Assets are typically loaded into memory when they‘re first used and freed when the last handle is dropped.
“Core” assets that see constant use (like basic UI stuff) can be loses into memory.
Give it a path, it immediately returns a handle, it internally checks if it‘s already loaded and if not it will load the asset into memory.
SoA for assets: { Name, Handle, Data }
Void expects literally everything to be tapped into the ECS. Review Bevy Asset systems.
     */

    public override void Load(string directory)
    {
        // Process all folders inside textures folder. This accommodates nested material folders for whatever reason.
        var folders = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories);
        SoundManager manager = SSLGame.SoundManager;

        foreach (string folder in folders)
        {
            var folderName = Path.GetRelativePath(directory, folder).ToLowerInvariant();

            var files = GetFiles(folder, SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var handle = Handle.Create(folder, file);
                manager.RegisterSound(handle, fileInfo);
            }
        }

    }
}