// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMemberInSuper.Global
namespace SKSSL
{
    /// <summary>
    /// For generic parsers or loaders, typically with YAML files that permit the programme's expansion into loading
    /// serialized game content.
    /// </summary>
    public interface IGameFileLoader
    {
        /// <summary>
        /// Loads game content using the provided folder / file path.
        /// </summary>
        public static abstract void Load(string path);

        /// <summary>
        /// Clean method used for handling the deletion of any leftover data.
        /// </summary>
        public static abstract void HandleClean();
    }
}