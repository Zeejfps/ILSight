using System.Collections.Concurrent;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Analysis;

namespace ILSight.Core;

/// <summary>ILSpy's search for the types deriving from a type: those in its scope, among the assemblies
/// a list holds now, that name it among their direct base types by full name and arity. Each call
/// searches again, so assemblies opened since are searched too.</summary>
internal static class DerivedTypes
{
    /// <summary>Whether anything can derive from <paramref name="type"/>: a class that is not sealed, or
    /// an interface.</summary>
    public static bool CanHave(ITypeDefinition type)
    {
        return !type.IsSealed && type.Kind is TypeKind.Class or TypeKind.Interface;
    }

    /// <summary>The reads of the type systems of the modules the search for <paramref name="type"/>'s derived types,
    /// in <paramref name="module"/>, can reach, each awaited (<see cref="AnalyzerScope.ReadReachableAsync"/>).</summary>
    public static Task<ModuleTypeSystems> ReadReachableAsync(LoadedModule module, CancellationToken cancellationToken)
    {
        return AnalyzerScope.ReadReachableAsync(module.List, module, cancellationToken);
    }

    /// <summary>The types deriving from <paramref name="type"/> among <paramref name="typeSystems"/>, the reads of the
    /// modules the search can reach (<see cref="ReadReachableAsync"/>), beside each module the search reached that the
    /// library could not read, by file name.</summary>
    public static ScopeSearch<ITypeDefinition> Search(AssemblyList list, ModuleTypeSystems typeSystems, ITypeDefinition type,
        CancellationToken cancellationToken)
    {
        var found = new List<ITypeDefinition>();
        var unreadable = new ConcurrentDictionary<LoadedModule, UnreadableModule>();
        foreach (var candidate in AnalyzerScope.Create(list, typeSystems, unreadable, type).GetTypes(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (NamesAsBase(candidate, type))
            {
                found.Add(candidate);
            }
        }

        return new ScopeSearch<ITypeDefinition>
        {
            Found = found,
            Unreadable = unreadable.Values.OrderBy(static u => u.Module.FilePath.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    /// <summary>The types of <paramref name="type"/>'s own module that derive from it directly.</summary>
    public static List<ITypeDefinition> FindInModule(ITypeDefinition type)
    {
        return DecompilerFacts.GetModule(type).TypeDefinitions
            .Where(c => NamesAsBase(c, type))
            .ToList();
    }

    private static bool NamesAsBase(ITypeDefinition candidate, ITypeDefinition type)
    {
        foreach (var baseType in candidate.DirectBaseTypes)
        {
            if (IsNamedAs(baseType, type))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsNamedAs(IType baseType, ITypeDefinition type)
    {
        if (baseType.FullName != type.FullName)
        {
            return false;
        }

        return baseType.TypeParameterCount == type.TypeParameterCount;
    }
}
