using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>
/// How the real ILSpy names the nodes of its assembly tree, so bookmark files can be exchanged with
/// it: the assembly by its file, a namespace by its name, a type by its reflection name, and a
/// member by its kind and IL signature. Each name is what ILSpy's tree node returns from ToString.
/// </summary>
public static class ILSpyTree
{
    private static readonly ILAmbience Signatures = new()
    {
        ConversionFlags = ConversionFlags.ShowTypeParameterList
            | ConversionFlags.PlaceReturnTypeAfterParameterList
            | ConversionFlags.ShowReturnType
            | ConversionFlags.ShowParameterList
            | ConversionFlags.ShowParameterModifiers,
    };

    /// <summary>The name ILSpy's tree gives the node standing for <paramref name="node"/>, or null
    /// for a node it has no named counterpart of: the one kept when the node was made.</summary>
    public static string? NameOf(BrowseNode node)
    {
        return node.ILSpyName;
    }

    /// <summary>The name ILSpy's tree gives a node of <paramref name="subject"/>, read from what it stands for, as
    /// <see cref="BrowseNodes"/> makes the node.</summary>
    internal static string? NameOf(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.TypeDefinition type => NodeDefinitions.GetSymbol(type.Definition).ReflectionName,
            NodeSubject.MemberDefinition member => NodeDefinitions.GetSymbol(member.Definition) switch
            {
                IMethod method => "Method " + Signatures.ConvertSymbol(method),
                IProperty property => "Property " + Signatures.ConvertSymbol(property),
                IField field => "Field " + field.Name,
                IEvent @event => "Event " + @event.Name,
                _ => null,
            },
            NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
                or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType or NodeSubject.Resources
                or NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables or NodeSubject.MetadataTable
                or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.FailedAssembly => null,
            NodeSubject.Module module => module.Value.FilePath.Text,
            NodeSubject.Namespace ns => ns.Value.Name,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The names of the nodes down from the assembly to <paramref name="node"/>, as ILSpy's
    /// tree finds a node by; null when one of them has no name there.</summary>
    public static IReadOnlyList<string>? PathOf(BrowseNode node)
    {
        var path = new List<string>();
        for (var n = node; n is not null; n = n.Parent)
        {
            var name = NameOf(n);
            if (name is null)
            {
                return null;
            }

            path.Add(name);
        }
        path.Reverse();
        return path;
    }

    /// <summary>The node a bookmark ILSpy made in the assembly at <paramref name="filePath"/> sits
    /// in, as the bookmarks describe it for ILSpy (<see cref="ILSpyBookmarkInfo"/>): the node <paramref name="treePath"/> names, else the
    /// node of the entity with metadata <paramref name="token"/>, when its full name is
    /// <paramref name="memberName"/>, or its nearest ancestor. Loads the assembly without listing it; null
    /// when it cannot be opened or has neither.</summary>
    public static async Task<BrowseNode?> LocateAsync(AssemblyList list, BrowseSession session, AssemblyPath filePath, IReadOnlyList<string>? treePath,
        uint token, string memberName, CancellationToken ct)
    {
        if (treePath is not null && treePath.Count > 0)
        {
            var found = await FindAsync(list, session, treePath, ct);
            if (found is not null)
            {
                return found;
            }
        }
        return await FindEntityAsync(list, session, filePath, token, memberName, ct);
    }

    private static async Task<BrowseNode?> FindAsync(AssemblyList list, BrowseSession session, IReadOnlyList<string> path, CancellationToken ct)
    {
        var filePath = AssemblyPath.Parse(path[0]);
        if (filePath is null)
        {
            return null;
        }

        var assembly = FindModule(list, filePath);
        if (assembly is null)
        {
            return null;
        }

        var node = await BrowseSessions.EnsureRootAsync(session, assembly, ct);
        for (var i = 1; i < path.Count; i++)
        {
            BrowseNode? next = null;
            foreach (var child in await BrowseSessions.ListChildrenAsync(session, node, ct))
            {
                var name = NameOf(child);
                if (name != path[i])
                {
                    continue;
                }

                next = child;
                break;
            }
            if (next is null)
            {
                return null;
            }

            node = next;
        }
        return node;
    }

    /// <summary>The node of the entity <paramref name="token"/> names, or its nearest ancestor; null when
    /// the entity is not the one called <paramref name="memberName"/>, as after a rebuild the token can name
    /// another.</summary>
    private static async Task<BrowseNode?> FindEntityAsync(AssemblyList list, BrowseSession session, AssemblyPath filePath, uint token, string memberName,
        CancellationToken ct)
    {
        if (token == 0)
        {
            return null;
        }

        var assembly = FindModule(list, filePath);
        if (assembly is null)
        {
            return null;
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(assembly, ct);
        return await read.Match(
            r => FindEntityAsync(session, assembly, r.TypeSystem.MainModule, token, memberName, ct),
            static _ => Task.FromResult<BrowseNode?>(null));
    }

    private static async Task<BrowseNode?> FindEntityAsync(BrowseSession session, LoadedModule assembly, MetadataModule main, uint token, string memberName,
        CancellationToken ct)
    {
        var entity = Resolve(main, (int)token);
        if (entity is null || entity.FullName != memberName)
        {
            return null;
        }

        try
        {
            return await BrowseSessions.FindOrAncestorAsync(session, assembly, main, entity.MetadataToken, ct);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static IEntity? Resolve(MetadataModule main, int token)
    {
        try
        {
            return main.ResolveEntity(MetadataTokens.EntityHandle(token));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static LoadedModule? FindModule(AssemblyList list, AssemblyPath filePath)
    {
        try
        {
            return AssemblyListing.TryLoadAssembly(list, filePath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
