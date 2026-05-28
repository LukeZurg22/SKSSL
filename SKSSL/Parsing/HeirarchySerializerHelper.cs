using System;
using System.Collections.Generic;
using System.Reflection;
using SKSSL.ECS;
using VYaml.Annotations;
using VYaml.Emitter;
using VYaml.Serialization;

namespace SKSSL;

public static class HierarchySerializerHelper
{
    internal static void SerializeHierarchy(
        ref Utf8YamlEmitter emitter,
        object obj,
        Type type,
        YamlSerializationContext context)
    {
        var stack = new Stack<Type>();

        // Build inheritance chain (bottom-up)
        while (type != null && type != typeof(object))
        {
            if (!typeof(Prototype).IsAssignableFrom(type))
                break;

            stack.Push(type);
            type = type.BaseType!;
        }

        // Serialize base → derived order
        while (stack.Count > 0)
        {
            Type stackType = stack.Pop();
            foreach (var p in FormatterFieldHelper.IterateMembers(stackType, obj))
            {
               // FieldInfo field = enumerator.Current;
               // emitter.WriteString(field.Name.TrimStart('_'));
               // var value = field.GetValue(obj);
               // YamlSerializer.Serialize(ref emitter, value, context.Options);
            }
        }
    }
}