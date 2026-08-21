using System.Collections.Generic;
using System.IO;
using SKSSL.Serializing;

namespace SKSSL;

public class SoundManager
{
    private readonly Dictionary<Handle, FileInfo> _handleToFile = new();

    public void RegisterSound(Handle handle, FileInfo info)
    {
        
    }
}