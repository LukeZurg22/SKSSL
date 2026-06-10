// ReSharper disable FieldCanBeMadeReadOnly.Global
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
    public PrototypeLoader ContentLoader = new YamlLoader();

    /* // WIP: More dynamic directory loading.
     * Dictionary<string, GameLoader>() {
     *  { "textures", new TextureLoader() },
     *  { "prototypes", new YamlLoader() },
     *  { "localization", new LocaleLoader() },
     * }
     */
    
    /// <summary>
    /// The number of entities that can be recycled. This is ignored when recycling more than the expected entity count.
    /// </summary>
    /// <remarks>Don't touch this unless you know what you're doing. This may affect performance.</remarks>
    public int DESTROY_ENTITY_CACHE_LIMIT = 256;

    public override string ToString()
    {
        return $"{UseECS};{GumFile};{ContentLoader.GetType().Name}";
    }
}