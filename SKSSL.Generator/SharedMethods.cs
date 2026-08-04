using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SKSSL.Generator;

public static class SharedMethods
{
    internal static CommandInfo? GetCommandInfo(GeneratorAttributeSyntaxContext ctx)
    {
        // Filter-away non-abstract symbols.
        if (ctx.TargetSymbol is not INamedTypeSymbol symbol ||
            symbol.IsAbstract ||
            symbol.InstanceConstructors.All(c => c.Parameters.Length > 0))
        {
            // Skip abstract classes or classes without parameterless constructor.
            return null;
        }

        AttributeData attr = ctx.Attributes[0];

        var name = attr.ConstructorArguments.Length > 0
            ? attr.ConstructorArguments[0].Value as string
            : symbol.Name;

        return new CommandInfo(
            FullTypeName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Name: name ?? symbol.Name);
    }

    internal static SystemInfo? GetSystemInfo(GeneratorAttributeSyntaxContext ctx)
    {
        // Filter-away non-abstract symbols.
        if (ctx.TargetSymbol is not INamedTypeSymbol symbol ||
            symbol.IsAbstract ||
            symbol.InstanceConstructors.All(c => c.Parameters.Length > 0))
        {
            // Skip abstract classes or classes without parameterless constructor.
            return null;
        }

        AttributeData attr = ctx.Attributes[0];

        var name = attr.ConstructorArguments.Length > 0
            ? attr.ConstructorArguments[0].Value as string
            : symbol.Name;

        var priority = 0;

        // TODO: Add support for named arguments (Priority, etc.) if needed

        return new SystemInfo(
            FullTypeName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Name: name ?? symbol.Name,
            Priority: priority
        );
    }

    /// <summary>
    /// Filters game assemblies. Includes hard-coded assemblies that use SKSSL, KBSL, or Kuiperbilt.
    /// </summary>
    internal static bool IsRelevantAssembly(IAssemblySymbol assembly)
    {
        string name = assembly.Name;

        // Skip problematic/necessary assemblies
        return !name.StartsWith("MonoGame.", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("mscorlib") &&
               !name.StartsWith("netstandard");
    }

    /// <summary>
    /// Searches for members in compilation global namespace.
    /// </summary>
    /// <returns>Requested symbol name from namespace, or null if not found.</returns>
    internal static INamedTypeSymbol? FindInCompilation(Compilation compilation, string name)
    {
        // Fallback: search all types
        return compilation.GlobalNamespace
            .GetNamespaceMembers()
            .SelectMany(GetAllTypes)
            .FirstOrDefault(t => t.Name == name && !t.IsGenericType);
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (INamedTypeSymbol? type in ns.GetTypeMembers())
            yield return type;

        foreach (INamespaceSymbol? nestedNs in ns.GetNamespaceMembers())
        foreach (INamedTypeSymbol? type in GetAllTypes(nestedNs))
            yield return type;
    }

    internal static bool IsDerivedFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        INamedTypeSymbol? current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }

        return false;
    }

    internal static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
    {
        // Current assembly
        foreach (INamedTypeSymbol? type in GetAllTypes(compilation.Assembly))
            yield return type;

        // Referenced assemblies (source projects + binaries)
        foreach (MetadataReference? reference in compilation.References)
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asm)
                foreach (INamedTypeSymbol? type in GetAllTypes(asm))
                    yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(IAssemblySymbol assembly)
    {
        var stack = new Stack<INamespaceSymbol>();
        stack.Push(assembly.GlobalNamespace);

        while (stack.Count > 0)
        {
            INamespaceSymbol? ns = stack.Pop();
            foreach (INamespaceOrTypeSymbol? member in ns.GetMembers())
            {
                switch (member)
                {
                    case INamespaceSymbol childNs:
                        stack.Push(childNs);
                        break;
                    case INamedTypeSymbol type:
                    {
                        yield return type;

                        // nested types
                        foreach (INamedTypeSymbol? nested in GetNestedTypes(type))
                            yield return nested;
                        break;
                    }
                }
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (INamedTypeSymbol? nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (INamedTypeSymbol? deeper in GetNestedTypes(nested))
                yield return deeper;
        }
    }

    internal static INamedTypeSymbol? FindTypeInCompilation(Compilation compilation, string name)
    {
        return GetAllTypes(compilation)
            .FirstOrDefault(t => t.Name == name || t.Name == name + "Attribute");
    }
}