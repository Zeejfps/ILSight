using System.Diagnostics;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>
/// What a node shows and what saving its code writes: a grid, a picture, or a document, worked out
/// from what the node stands for and handed to the decompiler as a request that holds no node.
/// Safe to call concurrently.
/// </summary>
public static class NodeContentLoader
{
    /// <summary>What <paramref name="node"/> shows, made on the thread pool: for metadata its grid of
    /// rows, read the first time and kept; for a resource named as a PNG, JPEG, BMP, GIF, ICO, CUR or
    /// SVG file its picture, or when that cannot be decoded the resource as a document with why first;
    /// else the node decompiled with <paramref name="options"/>, which only C# reads.</summary>
    public static Task<NodeContent> GetContentAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ReadContentAsync(session, node, language, options, cancellationToken), cancellationToken);
    }

    /// <summary>Decompiles <paramref name="node"/> with <paramref name="options"/>, which only C# reads, once the read
    /// of its module's type system ended. Throws <see cref="OperationCanceledException"/> when cancelled; any other
    /// failure becomes a document describing it.</summary>
    public static async Task<DecompiledDocument> DecompileAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, CancellationToken cancellationToken = default)
    {
        return await NodeDecompiler.DecompileAsync(await RequestOfAsync(session, node, cancellationToken), language, options, cancellationToken);
    }

    /// <summary>The grid <paramref name="node"/> shows: a metadata header, directory, table or heap's, or a
    /// <c>.resources</c> file's entries; null for a node that shows none.</summary>
    private static MetadataGrid? GridOf(BrowseNode node)
    {
        if (node.Subject is NodeSubject.MetadataTable table)
        {
            return MetadataTables.EnsureGrid(table.Value);
        }

        if (node.Subject is NodeSubject.ResourceContainer container)
        {
            return ResourceNodes.EnsureGrid(container.Value);
        }

        return null;
    }

    private static async Task<NodeContent> ReadContentAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken)
    {
        var grid = GridOf(node);
        if (grid is not null)
        {
            return new NodeContent.Table(grid);
        }

        if (!BrowseNodes.IsImage(node.Subject))
        {
            return new NodeContent.Code(await DecompileAsync(session, node, language, options, cancellationToken));
        }

        var read = ReadImage(node);
        return await read.Match(
            decoded => GetPictureAsync(session, node, decoded.Image, language, options, cancellationToken),
            unreadable => DecompileWithNoteAsync(session, node, language, options, unreadable.Why, cancellationToken));
    }

    /// <summary><paramref name="node"/>'s document, after <paramref name="note"/> as a comment.</summary>
    private static async Task<NodeContent> DecompileWithNoteAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, string note, CancellationToken cancellationToken)
    {
        var request = await RequestOfAsync(session, node, cancellationToken);
        return new NodeContent.Code(await NodeDecompiler.DecompileAsync(request, language, options, cancellationToken, note));
    }

    /// <summary>The picture <paramref name="node"/>'s resource holds, or why it can't be read: the decompiler reads its
    /// bytes, and the decoders the picture in them.</summary>
    private static ResourceImageRead ReadImage(BrowseNode node)
    {
        try
        {
            return ResourceImages.DecodeImage(BrowseNodes.ReadResource(node));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new ResourceImageRead.Unreadable { Why = e.Message };
        }
    }

    /// <summary>The picture, with the markup it is stored as when it is drawn from shapes.</summary>
    private static async Task<NodeContent> GetPictureAsync(BrowseSession session, BrowseNode node, ResourceImage image, DecompilationLanguage language,
        DecompilerOptions options, CancellationToken cancellationToken)
    {
        if (!image.IsVector)
        {
            return new NodeContent.Picture { Image = image };
        }

        var source = await DecompileAsync(session, node, language, options, cancellationToken);
        return new NodeContent.Picture { Image = image, Source = source };
    }

    /// <summary>What saving <paramref name="node"/>'s code writes: for an assembly its whole module, for a namespace all
    /// its types, else what the node shows.</summary>
    internal static async Task<CodeExport> ToCodeExportAsync(BrowseSession session, BrowseNode node, CancellationToken ct)
    {
        if (node.Subject is NodeSubject.Module module)
        {
            return new CodeExport.WholeModule(module.Value);
        }

        if (node.Subject is NodeSubject.Namespace ns)
        {
            return new CodeExport.Namespace
            {
                Module = ns.Value.Module,
                Name = ns.Value.Name,
                Types = NodeDefinitions.GetTypes(ns.Value),
            };
        }

        return new CodeExport.Document(await RequestOfAsync(session, node, ct));
    }

    /// <summary>What decompiling <paramref name="node"/> asks for, with the children a summary of it lists, built on
    /// the way.</summary>
    private static async Task<DocumentRequest> RequestOfAsync(BrowseSession session, BrowseNode node, CancellationToken ct)
    {
        var children = await ListSummarizedChildrenAsync(session, node, ct);
        return RequestOf(node, children);
    }

    /// <summary>The children a summary of <paramref name="node"/> lists, each with the children it lists in turn: those
    /// of a package, resources, references, base and derived types, namespaces and the types used from a reference;
    /// none for a node whose document lists none.</summary>
    private static async Task<SummarizedChildren> ListSummarizedChildrenAsync(BrowseSession session, BrowseNode node, CancellationToken ct)
    {
        if (!IsSummarizedWithChildren(node.Subject))
        {
            return SummarizedChildren.None;
        }

        var children = await BrowseSessions.ListChildrenAsync(session, node, ct);
        var nested = new Dictionary<BrowseNode, SummarizedChildren>(ReferenceEqualityComparer.Instance);
        foreach (var child in children)
        {
            if (child.Subject is NodeSubject.TypeReference)
            {
                nested[child] = await ListSummarizedChildrenAsync(session, child, ct);
            }
        }
        return new SummarizedChildren { Children = children, Nested = nested };
    }

    /// <summary>Whether a summary of a node of <paramref name="subject"/> lists its children.</summary>
    private static bool IsSummarizedWithChildren(NodeSubject subject)
    {
        if (subject is NodeSubject.ResourceContainer container)
        {
            return container.Value.Contents is ContainerContents.AvaloniaAssets;
        }

        return subject is NodeSubject.Package or NodeSubject.Resources or NodeSubject.References or NodeSubject.ReferencedTypes
            or NodeSubject.TypeReference or NodeSubject.ModuleReference or NodeSubject.BaseTypes or NodeSubject.DerivedTypes
            or NodeSubject.Namespace;
    }

    /// <summary>The children a summary lists, built before it is written, and the children each of them lists in
    /// turn.</summary>
    private sealed class SummarizedChildren
    {
        public static SummarizedChildren None { get; } = new SummarizedChildren
        {
            Children = [], Nested = new Dictionary<BrowseNode, SummarizedChildren>(),
        };

        public required IReadOnlyList<BrowseNode> Children { get; init; }

        public required IReadOnlyDictionary<BrowseNode, SummarizedChildren> Nested { get; init; }
    }

    private static DocumentRequest RequestOf(BrowseNode node, SummarizedChildren children)
    {
        return node.Subject.Match<(BrowseNode Node, SummarizedChildren Children), DocumentRequest>((node, children),
            module: static (s, module) => new DocumentRequest.ModulePreview
            {
                Module = module.Value,
                Title = s.Node.Text,
                FullName = s.Node.FullName,
            },
            @namespace: static (s, _) => SummaryRequest(s.Node, new SummaryInfo.Namespace
            {
                Name = s.Node.Text,
                Entries = ListNamespaceEntries(s.Children),
            }),
            failedAssembly: static (s, failed) => new DocumentRequest.Failure
            {
                Assembly = failed.Value,
                Title = s.Node.Text,
                FullName = s.Node.FullName,
            },
            typeDefinition: static (s, type) => new DocumentRequest.Type
            {
                Definition = type.Definition,
                Title = s.Node.Text,
                FullName = s.Node.FullName,
            },
            memberDefinition: static (s, member) => new DocumentRequest.Member
            {
                Definition = member.Definition,
                Title = s.Node.Text,
                FullName = s.Node.FullName,
            },
            references: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            assemblyReference: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            referencedTypes: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            typeReference: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            memberReference: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            moduleReference: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            importedMethod: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            forwardedType: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            baseTypes: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            baseType: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            derivedTypes: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            derivedType: static (s, _) => SummaryRequest(s.Node, SummaryOf(s.Node, s.Children)),
            unreadableModule: static (s, _) => new DocumentRequest.Summary
            {
                Info = new SummaryInfo.Comments([s.Node.Text]),
                Module = null,
                Title = s.Node.Text,
                FullName = s.Node.FullName,
            },
            resources: static (s, _) => SummaryRequest(s.Node, DescribeResources(s.Children)),
            resourceContainer: static (s, container) => ContainerRequest(s.Node, s.Children, container.Value),
            resourceEntry: static (s, entry) => EntryRequest(s.Node, entry.Value),
            metadata: static (s, metadata) => SummaryRequest(s.Node, new SummaryInfo.Comments(MetadataTables.ListFolderLines(metadata.Value))),
            metadataTables: static (s, tables) => SummaryRequest(s.Node, new SummaryInfo.Comments(MetadataTables.ListTablesLines(tables.Folder))),
            metadataTable: static (s, table) => SummaryRequest(s.Node, new SummaryInfo.Comments(MetadataTables.ListPartLines(table.Value))),
            package: static (s, _) => new DocumentRequest.Summary
            {
                Info = new SummaryInfo.Comments(s.Children.Children.Select(DescribePackageChild).ToList()),
                Module = null,
                Title = s.Node.Text,
                FullName = s.Node.FullName,
            },
            packageFile: static (s, file) => ResourceRequest(s.Node, ResourceNodes.CreateData(file.Value)));
    }

    /// <summary>The summary <paramref name="summary"/> of <paramref name="node"/>, read from its module.</summary>
    private static DocumentRequest.Summary SummaryRequest(BrowseNode node, SummaryInfo summary)
    {
        return new DocumentRequest.Summary
        {
            Info = summary,
            Module = node.SourceModule,
            Title = node.Text,
            FullName = node.FullName,
        };
    }

    /// <summary>What <paramref name="node"/>, standing for <paramref name="container"/>, shows: a <c>.resources</c>
    /// file its bytes, Avalonia's asset container the assets <paramref name="children"/> lists.</summary>
    private static DocumentRequest ContainerRequest(BrowseNode node, SummarizedChildren children, ResourceContainerInfo container)
    {
        return container.Contents.Match<(BrowseNode Node, SummarizedChildren Children, ResourceContainerInfo Container), DocumentRequest>(
            (node, children, container),
            static (s, _) => ResourceRequest(s.Node, ResourceNodes.CreateData(s.Container)),
            static (s, _) => SummaryRequest(s.Node, DescribeResources(s.Children)));
    }

    /// <summary>What <paramref name="node"/>, standing for <paramref name="entry"/>, shows: BAML the XAML it was
    /// compiled from, any other resource its bytes.</summary>
    private static DocumentRequest EntryRequest(BrowseNode node, ResourceEntryInfo entry)
    {
        return entry.Source.Match<(BrowseNode Node, ResourceEntryInfo Entry), DocumentRequest>((node, entry),
            static (s, _) => ResourceRequest(s.Node, ResourceNodes.CreateData(s.Entry)),
            static (s, _) => ResourceRequest(s.Node, ResourceNodes.CreateData(s.Entry)),
            static (s, baml) => new DocumentRequest.Baml
            {
                Module = s.Entry.Module,
                Data = baml.Data,
                Title = s.Node.Text,
                FullName = s.Node.FullName,
            });
    }

    private static DocumentRequest.Resource ResourceRequest(BrowseNode node, ResourceData resource)
    {
        return new DocumentRequest.Resource
        {
            Module = node.SourceModule,
            Description = resource.Description,
            Read = resource.ReadData,
            Title = node.Text,
            FullName = node.FullName,
        };
    }

    /// <summary>A comment a resource, describing each that <paramref name="children"/> lists.</summary>
    private static SummaryInfo.Comments DescribeResources(SummarizedChildren children)
    {
        return new SummaryInfo.Comments(children.Children.Select(static c => BrowseNodes.FindResourceData(c.Subject)).OfType<ResourceData>().Select(static r => r.Description).ToList());
    }

    /// <summary>What the summary of <paramref name="node"/>, which has no code of its own, lists of
    /// <paramref name="children"/>.</summary>
    private static SummaryInfo SummaryOf(BrowseNode node, SummarizedChildren children)
    {
        return node.Subject.Match<(BrowseNode Node, SummarizedChildren Children), SummaryInfo>((node, children),
            module: static (_, _) => throw new UnreachableException(),
            @namespace: static (_, _) => throw new UnreachableException(),
            failedAssembly: static (_, _) => throw new UnreachableException(),
            typeDefinition: static (_, _) => throw new UnreachableException(),
            memberDefinition: static (_, _) => throw new UnreachableException(),
            references: static (s, _) => new SummaryInfo.References
            {
                Assemblies = ChildrenAbout<NodeSubject.AssemblyReference>(s.Children).Select(static r => new SummaryLink
                {
                    Text = r.FullName,
                    Target = NodeDefinitions.FindTarget(r.Subject),
                }).ToList(),
                Modules = ChildrenAbout<NodeSubject.ModuleReference>(s.Children).Select(static m => m.Text).ToList(),
            },
            assemblyReference: static (_, reference) => new SummaryInfo.AssemblyReference
            {
                Module = reference.Value.Module,
                Reference = reference.Value.Reference,
            },
            referencedTypes: static (s, _) => new SummaryInfo.Used(ListUsed(s.Children)),
            typeReference: static (s, _) => new SummaryInfo.Used([UsedOf(s.Node, s.Children)]),
            memberReference: static (s, _) => new SummaryInfo.Used([UsedOf(s.Node, s.Children)]),
            moduleReference: static (s, module) => new SummaryInfo.ModuleReference
            {
                Name = module.Value.Name,
                ContainsMetadata = module.Value.ContainsMetadata,
                Imports = ChildrenAbout<NodeSubject.ImportedMethod>(s.Children).Select(static i => new SummaryLink
                {
                    Text = i.FullName,
                    Target = NodeDefinitions.FindTarget(i.Subject),
                }).ToList(),
            },
            importedMethod: static (s, import) => new SummaryInfo.ImportedMethod
            {
                Method = new SummaryLink
                {
                    Text = s.Node.FullName,
                    Target = NodeDefinitions.FindTarget(s.Node.Subject),
                },
                EntryPoint = import.Value.EntryPoint,
                ModuleName = import.Value.ModuleName,
            },
            forwardedType: static (s, forwarded) => new SummaryInfo.ForwardedType
            {
                FullName = forwarded.Value.FullName,
                Target = NodeDefinitions.FindTarget(s.Node.Subject),
                Destination = forwarded.Value.Destination,
            },
            baseTypes: static (s, _) => TypesOf(s.Children),
            baseType: static (s, _) => new SummaryInfo.Types([new SummaryLink
            {
                Text = s.Node.Text,
                Target = NodeDefinitions.FindTarget(s.Node.Subject),
            }]),
            derivedTypes: static (s, _) => TypesOf(s.Children),
            derivedType: static (s, _) => new SummaryInfo.Types([new SummaryLink
            {
                Text = s.Node.Text,
                Target = NodeDefinitions.FindTarget(s.Node.Subject),
            }]),
            unreadableModule: static (_, _) => throw new UnreachableException(),
            resources: static (_, _) => throw new UnreachableException(),
            resourceContainer: static (_, _) => throw new UnreachableException(),
            resourceEntry: static (_, _) => throw new UnreachableException(),
            metadata: static (_, _) => throw new UnreachableException(),
            metadataTables: static (_, _) => throw new UnreachableException(),
            metadataTable: static (_, _) => throw new UnreachableException(),
            package: static (_, _) => throw new UnreachableException(),
            packageFile: static (_, _) => throw new UnreachableException());
    }

    /// <summary>The types <paramref name="children"/> lists, each linked to where it is defined.</summary>
    private static SummaryInfo.Types TypesOf(SummarizedChildren children)
    {
        return new SummaryInfo.Types(children.Children.Select(static c => new SummaryLink
        {
            Text = c.Text,
            Target = NodeDefinitions.FindTarget(c.Subject),
        }).ToList());
    }

    /// <summary>The children of <paramref name="node"/> that stand for a <typeparamref name="TSubject"/>.</summary>
    private static IEnumerable<BrowseNode> ChildrenAbout<TSubject>(SummarizedChildren children) where TSubject : NodeSubject
    {
        return children.Children.Where(static c => c.Subject is TSubject);
    }

    private static string DescribePackageChild(BrowseNode child)
    {
        var file = BrowseNodes.FindResourceData(child.Subject);
        if (file is not null)
        {
            return file.Description;
        }

        if (child.Subject is NodeSubject.Package)
        {
            return child.Text + "/";
        }

        return child.Text;
    }

    /// <summary>The types the namespace defines and forwards, but those the compiler made up.</summary>
    private static List<NamespaceEntry> ListNamespaceEntries(SummarizedChildren children)
    {
        var entries = new List<NamespaceEntry>();
        foreach (var child in children.Children)
        {
            var visibility = child.Visibility;
            if (visibility.IsCompilerGenerated)
            {
                continue;
            }

            if (child.Subject is NodeSubject.TypeDefinition type)
            {
                entries.Add(new NamespaceEntry.Declared
                {
                    Type = NodeDefinitions.GetSymbol(type.Definition),
                    Label = child.Text,
                    Reference = type.Definition.Reference,
                });
            }

            if (child.Subject is NodeSubject.ForwardedType forwarded)
            {
                entries.Add(new NamespaceEntry.Forwarded(ForwardedLineOf(child, forwarded)));
            }
        }
        return entries;
    }

    /// <summary>The types and members <paramref name="node"/> lists as used from a reference.</summary>
    private static List<UsedEntry> ListUsed(SummarizedChildren children)
    {
        return children.Children
            .Select(c => UsedOf(c, children.Nested.GetValueOrDefault(c) ?? SummarizedChildren.None))
            .ToList();
    }

    /// <summary>What a summary says of <paramref name="node"/>, used from a reference, and of
    /// <paramref name="children"/>, the children it lists.</summary>
    private static UsedEntry UsedOf(BrowseNode node, SummarizedChildren children)
    {
        if (node.Subject is NodeSubject.ForwardedType forwarded)
        {
            return new UsedEntry.Forwarded(ForwardedLineOf(node, forwarded));
        }

        if (node.Subject is NodeSubject.TypeReference type)
        {
            return new UsedEntry.Type
            {
                FullName = type.Value.FullName,
                Target = NodeDefinitions.FindTarget(node.Subject),
                Used = ListUsed(children),
            };
        }

        if (node.Subject is NodeSubject.MemberReference member)
        {
            return new UsedEntry.Member
            {
                Label = node.Text,
                Kind = UsedMemberSpan(NodeDefinitions.GetDeclared(member.Value)),
                Target = NodeDefinitions.FindTarget(node.Subject),
            };
        }

        throw new UnreachableException();
    }

    private static ForwardedLine ForwardedLineOf(BrowseNode node, NodeSubject.ForwardedType forwarded)
    {
        return new ForwardedLine
        {
            Label = forwarded.Value.Label,
            Target = NodeDefinitions.FindTarget(node.Subject),
            Destination = forwarded.Value.Destination,
        };
    }

    private static SpanKind UsedMemberSpan(IMember member)
    {
        return member switch
        {
            IField => SpanKind.Field,
            IProperty => SpanKind.Property,
            IEvent => SpanKind.Event,
            _ => SpanKind.Method,
        };
    }
}
