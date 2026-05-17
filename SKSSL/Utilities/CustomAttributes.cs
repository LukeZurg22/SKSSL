using System.Reflection;
using System.Text;
// ReSharper disable All

namespace SolKom.Shared.Utilities;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class DisplayPropertyAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}


[AttributeUsage(AttributeTargets.Class)]
public class CustomCommandAttribute : Attribute
{
    public CustomCommandAttribute(string name = "", string description = "n/a")
    {
        Name = name;
        Description = description;
    }

    public string Name { get; }
    public string Description { get; }
}

public static class CustomAttributes
{
    /// <summary>
    /// Warning: The following code is AI Generated for the sake of extracting properties easier.
    /// It is not flawless, and does not work perfectly.
    /// </summary>
    public static StringBuilder ExtractAttributes<T>(T target, StringBuilder output)
    {
        Type targetType = typeof(T);

        // Get all properties and fields with the DisplayPropertyAttribute
        var displayProperties = targetType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<DisplayPropertyAttribute>() != null)
            .Select(p => new 
            {
                p.Name, 
                DisplayName = p.GetCustomAttribute<DisplayPropertyAttribute>()?.Name, 
                Value = p.GetValue(target) 
            });

        var displayFields = targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.GetCustomAttribute<DisplayPropertyAttribute>() != null)
            .Select(f => new 
            {
                f.Name, 
                DisplayName = f.GetCustomAttribute<DisplayPropertyAttribute>()?.Name, 
                Value = f.GetValue(target) 
            });

        // Combine properties and fields
        var displayMembers = displayProperties.Concat(displayFields);

        // Print the display name (or fallback to member name) and value
        foreach (var member in displayMembers)
        {
            string displayName = member.DisplayName ?? member.Name; // Use attribute name or fallback
            // Special handling for enum types
            if (member.Value != null && member.Value.GetType().IsEnum)
            {
                // Display enum as its name
                output.AppendLine($"{displayName}: {Enum.GetName(member.Value.GetType(), member.Value)}");
            }
            // Special handling for custom types with a "Name" property
            else if (member.Value != null && member.Value.GetType().GetProperty("Name") != null)
            {
                PropertyInfo? nameProperty = member.Value.GetType().GetProperty("Name");
                var nameValue = nameProperty?.GetValue(member.Value)?.ToString();
                output.AppendLine($"{displayName}: {nameValue}");
            }
            else
            {
                output.AppendLine($"{displayName}: {member.Value}");
            }
        }
        return output;
    }
}