using YamlDotNet.Serialization;

namespace SKSSL;

public interface ISettingsModule // TODO: Dynamize settings for custom user-added settings.
{
    void Load(IDeserializer deserializer);
    void Save(ISerializer serializer);
}