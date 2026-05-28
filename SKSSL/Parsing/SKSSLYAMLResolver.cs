using System;
using SKSSL.ECS;
using SKSSL.YAML;
using VYaml.Serialization;

namespace SKSSL;

public class SKSSLYAMLResolver : IYamlFormatterResolver
{
    private static readonly EntityFormatter entityFormatterInstance = new();
    private static readonly ComponentFormatter componentFormatterInstance = new();
    private static readonly YamlComponentFormatter yamlComponentFormatterInstance = new();

    public IYamlFormatter<T>? GetFormatter<T>()
    {
        Type type = typeof(T);

        if (type == typeof(YamlComponent))
            return yamlComponentFormatterInstance as IYamlFormatter<T>;
        
        if (type == typeof(Component))
            return componentFormatterInstance as IYamlFormatter<T>;
        
        return null;
    }
}