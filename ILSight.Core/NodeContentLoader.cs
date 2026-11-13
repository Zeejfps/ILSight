using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    /// rows, made the first time and kept (over a file the library maps, with its first <paramref name="firstGridRows"/>
    /// rows read, what the app's first show of it needs: <see cref="MetadataGrids.CreateGrid"/>); for a resource named as a PNG, JPEG, BMP, GIF, ICO, CUR or
    /// SVG file its picture, or when that cannot be decoded the resource as a document with why first;
    /// else the node decompiled with <paramref name="options"/>, which only C# reads. A unit reading the node's module:
    /// closed, reading nothing, when it was released (closed) before.</summary>
    public static Task<ModuleRead<NodeContent>> ReadContentAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, int firstGridRows, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ReadContentUnderReadingAsync(session, node, language, options, firstGridRows, cancellationToken), cancellationToken);
    }

    /// <summary>Decompiles <paramref name="node"/> with <paramref name="options"/>, which only C# reads, once the read
    /// of its module's type system ended. Throws <see cref="OperationCanceledException"/> when cancelled; any other
    /// failure becomes a document describing it. A unit reading the node's module, as
    /// <see cref="ReadContentAsync(BrowseSession, BrowseNode, DecompilationLanguage, DecompilerOptions, int, CancellationToken)"/> is.</summary>
    public static async Task<ModuleRead<DecompiledDocument>> DecompileAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, CancellationToken cancellationToken = default)
    {
        using var reading = LoadedAssemblies.BeginReading();
        if (!TryReadModuleOf(reading, node, out var closed))
        {
            return new ModuleRead<DecompiledDocument>.Closed { Module = closed };
        }

        return new ModuleRead<DecompiledDocument>.Read { Value = await DecompileAsync(session, reading, node, language, options, cancellationToken) };
    }

    private static bool TryReadModuleOf(ModuleReading reading, BrowseNode node, [NotNullWhen(false)] out LoadedModule? closed)
    {
        closed = null;
        if (node.Assembly is not LoadedModule module)
        {
            // A package's node: no module of its own to read.
            return true;
        }

        if (LoadedAssemblies.TryRead(reading, module))
        {
            return true;
        }

        closed = module;
        return false;
    }

    private static async Task<ModuleRead<NodeContent>> ReadContentUnderReadingAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, int firstGridRows, CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.BeginReading();
        if (!TryReadModuleOf(reading, node, out var closed))
        {
            return new ModuleRead<NodeContent>.Closed { Module = closed };
        }

        return new ModuleRead<NodeContent>.Read { Value = await ReadContentAsync(session, reading, node, language, options, firstGridRows, cancellationToken) };
    }

    private static async Task<DecompiledDocument> DecompileAsync(BrowseSession session, ModuleReading reading, BrowseNode node,
        DecompilationLanguage language, DecompilerOptions options, CancellationToken cancellationToken)
    {
        return await NodeDecompiler.DecompileAsync(reading, await RequestOfAsync(session, node, cancellationToken), language, options, cancellationToken);
    }

    private static MetadataGrid? GridOf(BrowseNode node, int firstRows)
    {
        if (node.Subject is NodeSubject.MetadataTable table)
        {
            return MetadataTables.EnsureGrid(table.Value, firstRows);
        }

        if (node.Subject is NodeSubject.ResourceContainer container)
        {
            return ResourceNodes.EnsureGrid(container.Value, firstRows);
        }

        return null;
    }

    private static async Task<NodeContent> ReadContentAsync(BrowseSession session, ModuleReading reading, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, int firstGridRows, CancellationToken cancellationToken)
    {
        var grid = GridOf(node, firstGridRows);
        if (grid is not null)
        {
            return new NodeContent.Table(grid);
        }

        if (!BrowseNodes.IsImage(node.Subject))
        {
            return new NodeContent.Code(await DecompileAsync(session, reading, node, language, options, cancellationToken));
        }

        var read = ReadImage(node);
        return await read.Match(
            decoded => GetPictureAsync(session, reading, node, decoded.Image, language, options, cancellationToken),
            unreadable => DecompileWithNoteAsync(session, reading, node, language, options, unreadable.Why, cancellationToken));
    }

    private static async Task<NodeContent> DecompileWithNoteAsync(BrowseSession session, ModuleReading reading, BrowseNode node,
        DecompilationLanguage language, DecompilerOptions options, string note, CancellationToken cancellationToken)
    {
        var request = await RequestOfAsync(session, node, cancellationToken);
        return new NodeContent.Code(await NodeDecompiler.DecompileAsync(reading, request, language, options, cancellationToken, note));
    }

    private static ResourceImageRead ReadImage(BrowseNode node)
    {
        // The decompiler reads the resource's bytes, and the decoders the picture in them.
        try
        {
            return ResourceImages.DecodeImage(BrowseNodes.ReadResource(node));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new ResourceImageRead.Unreadable { Why = e.Message };
        }
    }

    private static async Task<NodeContent> GetPictureAsync(BrowseSession session, ModuleReading reading, BrowseNode node, ResourceImage image,
        DecompilationLanguage language, DecompilerOptions options, CancellationToken cancellationToken)
    {
        if (!image.IsVector)
        {
            return new NodeContent.Picture { Image = image };
        }

        var source = await DecompileAsync(session, reading, node, language, options, cancellationToken);
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

    private static async Task<DocumentRequest> RequestOfAsync(BrowseSession session, BrowseNode node, CancellationToken ct)
    {
        var children = await ListSummarizedChildrenAsync(session, node, ct);
        return RequestOf(node, children);
    }

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

    // Built before the summary is written.
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

    private static DocumentRequest ContainerRequest(BrowseNode node, SummarizedChildren children, ResourceContainerInfo container)
    {
        return container.Contents.Match<(BrowseNode Node, SummarizedChildren Children, ResourceContainerInfo Container), DocumentRequest>(
            (node, children, container),
            static (s, _) => ResourceRequest(s.Node, ResourceNodes.CreateData(s.Container)),
            static (s, _) => SummaryRequest(s.Node, DescribeResources(s.Children)));
    }

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

    private static SummaryInfo.Comments DescribeResources(SummarizedChildren children)
    {
        return new SummaryInfo.Comments(children.Children.Select(static c => BrowseNodes.FindResourceData(c.Subject)).OfType<ResourceData>().Select(static r => r.Description).ToList());
    }

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

    private static SummaryInfo.Types TypesOf(SummarizedChildren children)
    {
        return new SummaryInfo.Types(children.Children.Select(static c => new SummaryLink
        {
            Text = c.Text,
            Target = NodeDefinitions.FindTarget(c.Subject),
        }).ToList());
    }

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

    private static List<UsedEntry> ListUsed(SummarizedChildren children)
    {
        return children.Children
            .Select(c => UsedOf(c, children.Nested.GetValueOrDefault(c) ?? SummarizedChildren.None))
            .ToList();
    }

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
