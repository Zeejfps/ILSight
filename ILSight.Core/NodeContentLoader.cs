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
            return new NodeContent.Picture(image);
        }

        var source = await DecompileAsync(session, node, language, options, cancellationToken);
        return new NodeContent.Picture(image, source);
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
            return new CodeExport.Namespace(ns.Value.Module, ns.Value.Name, NodeDefinitions.GetTypes(ns.Value));
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
        return node.Subject switch
        {
            NodeSubject.TypeDefinition type => new DocumentRequest.Type(type.Definition, node.Text, node.FullName),
            NodeSubject.MemberDefinition member => new DocumentRequest.Member(member.Definition, node.Text, node.FullName),
            NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
                or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType =>
                new DocumentRequest.Summary(SummaryOf(node, children), node.SourceModule, node.Text, node.FullName),
            NodeSubject.Resources => new DocumentRequest.Summary(DescribeResources(children), node.SourceModule, node.Text, node.FullName),
            NodeSubject.ResourceContainer container => container.Value.Contents switch
            {
                ContainerContents.AvaloniaAssets => new DocumentRequest.Summary(DescribeResources(children), node.SourceModule, node.Text, node.FullName),
                ContainerContents.ResourcesFile => ResourceRequest(node, ResourceNodes.CreateData(container.Value)),
                _ => throw new UnreachableException(),
            },
            NodeSubject.ResourceEntry entry => entry.Value.Source switch
            {
                ResourceSource.Baml baml => new DocumentRequest.Baml(entry.Value.Module, baml.Data, node.Text, node.FullName),
                ResourceSource.Manifest or ResourceSource.Entry => ResourceRequest(node, ResourceNodes.CreateData(entry.Value)),
                _ => throw new UnreachableException(),
            },
            NodeSubject.Metadata metadata => new DocumentRequest.Summary(new SummaryInfo.Comments(MetadataTables.ListFolderLines(metadata.Value)), node.SourceModule, node.Text, node.FullName),
            NodeSubject.MetadataTables tables => new DocumentRequest.Summary(new SummaryInfo.Comments(MetadataTables.ListTablesLines(tables.Folder)), node.SourceModule, node.Text,
                node.FullName),
            NodeSubject.MetadataTable table => new DocumentRequest.Summary(new SummaryInfo.Comments(MetadataTables.ListPartLines(table.Value)), node.SourceModule, node.Text, node.FullName),
            NodeSubject.Package => new DocumentRequest.Summary(new SummaryInfo.Comments(children.Children.Select(DescribePackageChild).ToList()), null,
                node.Text, node.FullName),
            NodeSubject.PackageFile file => ResourceRequest(node, ResourceNodes.CreateData(file.Value)),
            NodeSubject.Module module => new DocumentRequest.ModulePreview(module.Value, node.Text, node.FullName),
            NodeSubject.FailedAssembly failed => new DocumentRequest.Failure(failed.Value, node.Text, node.FullName),
            NodeSubject.Namespace => new DocumentRequest.Summary(new SummaryInfo.Namespace(node.Text, ListNamespaceEntries(children)), node.SourceModule,
                node.Text, node.FullName),
            _ => throw new UnreachableException(),
        };
    }

    private static DocumentRequest.Resource ResourceRequest(BrowseNode node, ResourceData resource)
    {
        return new DocumentRequest.Resource(node.SourceModule, resource.Description, resource.ReadData, node.Text, node.FullName);
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
        return node.Subject switch
        {
            NodeSubject.References => new SummaryInfo.References(
                ChildrenAbout<NodeSubject.AssemblyReference>(children).Select(static r => new SummaryLink(r.FullName, NodeDefinitions.FindTarget(r.Subject))).ToList(),
                ChildrenAbout<NodeSubject.ModuleReference>(children).Select(static m => m.Text).ToList()),
            NodeSubject.AssemblyReference reference => new SummaryInfo.AssemblyReference(reference.Value.Module, reference.Value.Reference),
            NodeSubject.ReferencedTypes => new SummaryInfo.Used(ListUsed(children)),
            NodeSubject.TypeReference or NodeSubject.MemberReference => new SummaryInfo.Used([UsedOf(node, children)]),
            NodeSubject.ModuleReference module => new SummaryInfo.ModuleReference(module.Value.Name, module.Value.ContainsMetadata,
                ChildrenAbout<NodeSubject.ImportedMethod>(children).Select(static i => new SummaryLink(i.FullName, NodeDefinitions.FindTarget(i.Subject))).ToList()),
            NodeSubject.ImportedMethod import => new SummaryInfo.ImportedMethod(new SummaryLink(node.FullName, NodeDefinitions.FindTarget(node.Subject)), import.Value.EntryPoint,
                import.Value.ModuleName),
            NodeSubject.ForwardedType forwarded => new SummaryInfo.ForwardedType(forwarded.Value.FullName, NodeDefinitions.FindTarget(node.Subject), forwarded.Value.Destination),
            NodeSubject.BaseTypes or NodeSubject.DerivedTypes =>
                new SummaryInfo.Types(children.Children.Select(static c => new SummaryLink(c.Text, NodeDefinitions.FindTarget(c.Subject))).ToList()),
            NodeSubject.BaseType or NodeSubject.DerivedType => new SummaryInfo.Types([new SummaryLink(node.Text, NodeDefinitions.FindTarget(node.Subject))]),
            NodeSubject.TypeDefinition or NodeSubject.MemberDefinition or NodeSubject.Resources or NodeSubject.ResourceContainer
                or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables or NodeSubject.MetadataTable
                or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly => throw new UnreachableException(),
            _ => throw new UnreachableException(),
        };
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
                entries.Add(new NamespaceEntry.Declared(NodeDefinitions.GetSymbol(type.Definition), child.Text, type.Definition.Reference));
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
        return node.Subject switch
        {
            NodeSubject.ForwardedType forwarded => new UsedEntry.Forwarded(ForwardedLineOf(node, forwarded)),
            NodeSubject.TypeReference type => new UsedEntry.Type(type.Value.FullName, NodeDefinitions.FindTarget(node.Subject), ListUsed(children)),
            NodeSubject.MemberReference member => new UsedEntry.Member(node.Text, UsedMemberSpan(NodeDefinitions.GetDeclared(member.Value)), NodeDefinitions.FindTarget(node.Subject)),
            _ => throw new UnreachableException(),
        };
    }

    private static ForwardedLine ForwardedLineOf(BrowseNode node, NodeSubject.ForwardedType forwarded)
    {
        return new ForwardedLine(forwarded.Value.Label, NodeDefinitions.FindTarget(node.Subject), forwarded.Value.Destination);
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
