using System.Reflection;
using SKSSL.ECS;

namespace SKSSL.YAML;

#pragma warning disable CS8618
public record EntityTemplate
{
    
    public string ReferenceId { get; init; }
    public string NameKey { get; init; }
    public string DescriptionKey { get; init; }
    public IReadOnlyDictionary<Type, object> DefaultComponents { get; init; }
    
    /// <summary>
    /// Can be overwritten to allow for safe type-casting.
    /// </summary>
    public virtual Type EntityType => typeof(SKEntity);
    protected EntityTemplate(EntityYaml yaml, IReadOnlyDictionary<Type, object> components)
    {
        ReferenceId = yaml.ReferenceId;
        NameKey = yaml.Name;
        DescriptionKey = yaml.Description;
        DefaultComponents = components;
    }

    /// <summary>
    /// Dynamic constructor factory — works with any depth of inheritance
    /// </summary>
    /// <param name="yaml"></param>
    /// <param name="components"></param>
    /// <typeparam name="TTemplate"></typeparam>
    /// <returns></returns>
    public static TTemplate CreateFromYaml<TTemplate>(
        EntityYaml yaml,
        Dictionary<Type, object> components)
        where TTemplate : EntityTemplate
    {

        if (Activator.CreateInstance(
                typeof(TTemplate),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                [yaml, components],
                null) is not TTemplate template)
        {
            throw new MissingMethodException(
                $"No suitable constructor found on {typeof(TTemplate).Name} " +
                $"for YAML type {yaml.GetType().Name}. " +
                "Ensure there is a protected/internal constructor accepting a compatible YAML type.");
        }

        return template;
    }
}