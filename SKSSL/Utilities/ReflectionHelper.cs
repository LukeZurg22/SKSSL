using System.Reflection;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities;

public static class ReflectionHelper
{
    public static Type[] GetStaticClassesImplementingInterface<TInterface>()
    {
        Type interfaceType = typeof(TInterface);
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass
                        && t.IsAbstract
                        && t.IsSealed // static class
                        && interfaceType.IsAssignableFrom(t))
            .ToArray();
    }
}