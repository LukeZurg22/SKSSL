using static System.StringComparison;

namespace SKSSL;

public static class ComponentTypeHelper
{
    /// <summary>
    /// Strips "Component" suffix for YAML tags (e.g. RenderableComponent → Renderable)
    /// </summary>
    public static string NormalizeTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return string.Empty;
        const string suffix = "Component";
        return typeName.EndsWith(suffix, OrdinalIgnoreCase) ? typeName[..^suffix.Length] : typeName;
    }

    /// <summary>
    /// Adds "Component" suffix if missing
    /// </summary>
    public static string GetFullComponentTypeName(string shortName)
    {
        if (string.IsNullOrEmpty(shortName))
            return string.Empty;

        if (shortName.EndsWith("Component", Ordinal))
            return shortName;

        return shortName + "Component";
    }
}