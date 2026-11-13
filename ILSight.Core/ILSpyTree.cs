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
        return subject.Match<string?>(
            module: static module => module.Value.FilePath.Text,
            @namespace: static ns => ns.Value.Name,
            failedAssembly: static _ => null,
            typeDefinition: static type => NodeDefinitions.GetSymbol(type.Definition).ReflectionName,
            memberDefinition: static member => NodeDefinitions.GetSymbol(member.Definition) switch
            {
                IMethod method => "Method " + Signatures.ConvertSymbol(method),
                IProperty property => "Property " + Signatures.ConvertSymbol(property),
                IField field => "Field " + field.Name,
                IEvent @event => "Event " + @event.Name,
                _ => null,
            },
            references: static _ => null,
            assemblyReference: static _ => null,
            referencedTypes: static _ => null,
            typeReference: static _ => null,
            memberReference: static _ => null,
            moduleReference: static _ => null,
            importedMethod: static _ => null,
            forwardedType: static _ => null,
            baseTypes: static _ => null,
            baseType: static _ => null,
            derivedTypes: static _ => null,
            derivedType: static _ => null,
            unreadableModule: static _ => null,
            resources: static _ => null,
            resourceContainer: static _ => null,
            resourceEntry: static _ => null,
            metadata: static _ => null,
            metadataTables: static _ => null,
            metadataTable: static _ => null,
            package: static _ => null,
            packageFile: static _ => null);
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
    /// <paramref name="memberName"/>, or its nearest ancestor. Loads the assembly without listing it; not found
    /// when it cannot be opened or has neither; unreadable when the library could not read the type system of the
    /// module either is looked for in, and the other found nothing.</summary>
    public static async Task<Lookup<BrowseNode>> LocateAsync(AssemblyList list, BrowseSession session, AssemblyPath filePath,
        IReadOnlyList<string>? treePath, uint token, string memberName, CancellationToken ct)
    {
        Lookup<BrowseNode> byPath = new Lookup<BrowseNode>.NotFound();
        if (treePath is not null && treePath.Count > 0)
        {
            byPath = await FindAsync(list, session, treePath, ct);
            if (byPath is Lookup<BrowseNode>.Found)
            {
                return byPath;
            }
        }

        var byToken = await FindEntityAsync(list, session, filePath, token, memberName, ct);
        return byToken.Match<Lookup<BrowseNode>>(static found => found, _ => byPath, static unreadable => unreadable);
    }

    private static async Task<Lookup<BrowseNode>> FindAsync(AssemblyList list, BrowseSession session, IReadOnlyList<string> path, CancellationToken ct)
    {
        var filePath = AssemblyPath.Parse(path[0]);
        if (filePath is null)
        {
            return new Lookup<BrowseNode>.NotFound();
        }

        var assembly = FindModule(list, filePath);
        if (assembly is null)
        {
            return new Lookup<BrowseNode>.NotFound();
        }

        Lookup<BrowseNode> reached = new Lookup<BrowseNode>.Found { Value = await BrowseSessions.EnsureRootAsync(session, assembly, ct) };
        for (var i = 1; i < path.Count; i++)
        {
            var name = path[i];
            reached = await reached.ThenFindAsync((node, c) => FindChildAsync(session, node, name, c), ct);
        }
        return reached;
    }

    /// <summary>The child of <paramref name="node"/> ILSpy's tree names <paramref name="name"/>, once the read of the
    /// type system its children's build reads ended.</summary>
    private static async Task<Lookup<BrowseNode>> FindChildAsync(BrowseSession session, BrowseNode node, string name, CancellationToken ct)
    {
        var children = await BrowseSessions.ListReadableChildrenAsync(session, node, ct);
        return children.Match(
            done => Lookup.FromNullable(done.Value.FirstOrDefault(child => NameOf(child) == name)),
            static unreadable => new Lookup<BrowseNode>.Unreadable { Module = unreadable.Module },
            static _ => new Lookup<BrowseNode>.NotFound());
    }

    /// <summary>The node of the entity <paramref name="token"/> names, or its nearest ancestor; not found when
    /// the entity is not the one called <paramref name="memberName"/>, as after a rebuild the token can name
    /// another.</summary>
    private static async Task<Lookup<BrowseNode>> FindEntityAsync(AssemblyList list, BrowseSession session, AssemblyPath filePath, uint token,
        string memberName, CancellationToken ct)
    {
        if (token == 0)
        {
            return new Lookup<BrowseNode>.NotFound();
        }

        var assembly = FindModule(list, filePath);
        if (assembly is null)
        {
            return new Lookup<BrowseNode>.NotFound();
        }

        using var reading = LoadedAssemblies.BeginReading();
        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
        return await read.Match(
            async r => Lookup.FromNullable(await FindEntityAsync(session, assembly, r.TypeSystem.MainModule, token, memberName, ct)),
            u => Task.FromResult(Lookup.FromUnreadable<BrowseNode>(assembly, u)),
            static _ => Task.FromResult<Lookup<BrowseNode>>(new Lookup<BrowseNode>.NotFound()));
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
