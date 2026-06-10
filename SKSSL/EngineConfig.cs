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
    public GameContentLoader ContentLoader = new YamlLoader();

    public override string ToString()
    {
        return $"{UseECS};{GumFile};{ContentLoader.GetType().Name}";
    }
}