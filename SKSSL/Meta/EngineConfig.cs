// ReSharper disable FieldCanBeMadeReadOnly.Global

using SKSSL.Serializing;
using SKSSL.Textures;

namespace SKSSL;

public class EngineConfig
{
    /// Ultimate toggle to use ECS service. Enable this at project initialization.
    /// To use, set UseECS = true.
    // ReSharper disable once ConvertToConstant.Global
    public bool UseECS = true;

    /// <summary>
    /// The Project Gum UI file that will dictate how UI is loaded.
    /// <code>
    /// Example: "SolKom.gumx"
    /// </code>
    /// </summary>
    public string GumFile = string.Empty;

    /// <summary>
    /// A configurable developer-provided content loader that handles the logic the game uses to search and
    /// handle its files, whether to Serialize or Deserialize data.
    /// </summary>
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public IGameLoader PrototypeLoader = new PrototypeLoader<SerializerDefaultYaml>(".yaml", ".yml");
    
    /// <summary>
    /// A replaceable developer-provided texture loader.
    /// </summary>
    public TextureLoader TextureLoader = new TextureLoader();
    
    /// <summary>
    /// The number of active objects that can be recycled. This is ignored when recycling more than the expected count.
    /// </summary>
    /// <remarks>Don't touch this unless you know what you're doing. This may affect performance.</remarks>
    public int DESTROY_CACHE_LIMIT = 1024;
    
    public override string ToString() => $"{UseECS};{GumFile};{PrototypeLoader.GetType().Name}";
}