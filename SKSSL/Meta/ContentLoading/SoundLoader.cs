using System.Collections.Generic;

namespace SKSSL;

public class SoundLoader() : IGameLoader(".wav", ".mp3", ".opus", ".ogg")
{
    private Dictionary<string, string> _handleToPath  = new(); 
    
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
        throw new System.NotImplementedException();
        var files = GetFiles(directory);
    }
}