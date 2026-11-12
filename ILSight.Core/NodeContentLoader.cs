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
        return Task.Run(() => GetContent(session, node, language, options, cancellationToken), cancellationToken);
    }

    /// <summary>Decompiles <paramref name="node"/> with <paramref name="options"/>, which only C# reads.
    /// Throws <see cref="OperationCanceledException"/> when cancelled; any other failure becomes a
    /// document describing it.</summary>
    public static DecompiledDocument Decompile(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        return NodeDecompiler.Decompile(RequestOf(session, node), language, options, cancellationToken);
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

    private static NodeContent GetContent(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken)
    {
        var grid = GridOf(node);
        if (grid is not null)
        {
            return new NodeContent.Table(grid);
        }

        if (!BrowseNodes.IsImage(node.Subject))
        {
            return new NodeContent.Code(Decompile(session, node, language, options, cancellationToken));
        }

        var read = ReadImage(node);
        return read.Match<NodeContent>(
            decoded => GetPicture(session, node, decoded.Image, language, options, cancellationToken),
            unreadable => new NodeContent.Code(NodeDecompiler.Decompile(RequestOf(session, node), language, options, cancellationToken, unreadable.Why)));
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
    private static NodeContent.Picture GetPicture(BrowseSession session, BrowseNode node, ResourceImage image, DecompilationLanguage language,
        DecompilerOptions options, CancellationToken cancellationToken)
    {
        if (!image.IsVector)
        {
            return new NodeContent.Picture(image);
        }

        var source = Decompile(session, node, language, options, cancellationToken);
        return new NodeContent.Picture(image, source);
    }

    /// <summary>What saving <paramref name="node"/>'s code writes: for an assembly its whole module, for a namespace all
    /// its types, else what the node shows.</summary>
    internal static CodeExport ToCodeExport(BrowseSession session, BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.Module module => new CodeExport.WholeModule(module.Value),
            NodeSubject.Namespace ns => new CodeExport.Namespace(ns.Value.Module, ns.Value.Name, NodeDefinitions.GetTypes(ns.Value)),
            _ => new CodeExport.Document(RequestOf(session, node)),
        };
    }

    private static DocumentRequest RequestOf(BrowseSession session, BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.TypeDefinition type => new DocumentRequest.Type(type.Definition, node.Text, node.FullName),
            NodeSubject.MemberDefinition member => new DocumentRequest.Member(member.Definition, node.Text, node.FullName),
            NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
                or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType =>
                new DocumentRequest.Summary(SummaryOf(session, node), node.SourceModule, node.Text, node.FullName),
            NodeSubject.Resources => new DocumentRequest.Summary(DescribeResources(session, node), node.SourceModule, node.Text, node.FullName),
            NodeSubject.ResourceContainer container => container.Value.Contents switch
            {
                ContainerContents.AvaloniaAssets => new DocumentRequest.Summary(DescribeResources(session, node), node.SourceModule, node.Text, node.FullName),
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
            NodeSubject.Package => new DocumentRequest.Summary(new SummaryInfo.Comments(BrowseSessions.ListChildren(session, node).Select(DescribePackageChild).ToList()), null,
                node.Text, node.FullName),
            NodeSubject.PackageFile file => ResourceRequest(node, ResourceNodes.CreateData(file.Value)),
            NodeSubject.Module module => new DocumentRequest.ModulePreview(module.Value, node.Text, node.FullName),
            NodeSubject.FailedAssembly failed => new DocumentRequest.Failure(failed.Value, node.Text, node.FullName),
            NodeSubject.Namespace => new DocumentRequest.Summary(new SummaryInfo.Namespace(node.Text, ListNamespaceEntries(session, node)), node.SourceModule,
                node.Text, node.FullName),
            _ => throw new UnreachableException(),
        };
    }

    private static DocumentRequest.Resource ResourceRequest(BrowseNode node, ResourceData resource)
    {
        return new DocumentRequest.Resource(node.SourceModule, resource.Description, resource.ReadData, node.Text, node.FullName);
    }

    /// <summary>A comment a resource, describing each that <paramref name="node"/> lists.</summary>
    private static SummaryInfo.Comments DescribeResources(BrowseSession session, BrowseNode node)
    {
        return new SummaryInfo.Comments(BrowseSessions.ListChildren(session, node).Select(static c => BrowseNodes.FindResourceData(c.Subject)).OfType<ResourceData>().Select(static r => r.Description).ToList());
    }

    /// <summary>What the summary of <paramref name="node"/>, which has no code of its own, lists.</summary>
    private static SummaryInfo SummaryOf(BrowseSession session, BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.References => new SummaryInfo.References(
                ChildrenAbout<NodeSubject.AssemblyReference>(session, node).Select(static r => new SummaryLink(r.FullName, NodeDefinitions.FindTarget(r.Subject))).ToList(),
                ChildrenAbout<NodeSubject.ModuleReference>(session, node).Select(static m => m.Text).ToList()),
            NodeSubject.AssemblyReference reference => new SummaryInfo.AssemblyReference(reference.Value.Module, reference.Value.Reference),
            NodeSubject.ReferencedTypes => new SummaryInfo.Used(ListUsed(session, node)),
            NodeSubject.TypeReference or NodeSubject.MemberReference => new SummaryInfo.Used([UsedOf(session, node)]),
            NodeSubject.ModuleReference module => new SummaryInfo.ModuleReference(module.Value.Name, module.Value.ContainsMetadata,
                ChildrenAbout<NodeSubject.ImportedMethod>(session, node).Select(static i => new SummaryLink(i.FullName, NodeDefinitions.FindTarget(i.Subject))).ToList()),
            NodeSubject.ImportedMethod import => new SummaryInfo.ImportedMethod(new SummaryLink(node.FullName, NodeDefinitions.FindTarget(node.Subject)), import.Value.EntryPoint,
                import.Value.ModuleName),
            NodeSubject.ForwardedType forwarded => new SummaryInfo.ForwardedType(forwarded.Value.FullName, NodeDefinitions.FindTarget(node.Subject), forwarded.Value.Destination),
            NodeSubject.BaseTypes or NodeSubject.DerivedTypes =>
                new SummaryInfo.Types(BrowseSessions.ListChildren(session, node).Select(static c => new SummaryLink(c.Text, NodeDefinitions.FindTarget(c.Subject))).ToList()),
            NodeSubject.BaseType or NodeSubject.DerivedType => new SummaryInfo.Types([new SummaryLink(node.Text, NodeDefinitions.FindTarget(node.Subject))]),
            NodeSubject.TypeDefinition or NodeSubject.MemberDefinition or NodeSubject.Resources or NodeSubject.ResourceContainer
                or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables or NodeSubject.MetadataTable
                or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly => throw new UnreachableException(),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The children of <paramref name="node"/> that stand for a <typeparamref name="TSubject"/>.</summary>
    private static IEnumerable<BrowseNode> ChildrenAbout<TSubject>(BrowseSession session, BrowseNode node) where TSubject : NodeSubject
    {
        return BrowseSessions.ListChildren(session, node).Where(static c => c.Subject is TSubject);
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
    private static List<NamespaceEntry> ListNamespaceEntries(BrowseSession session, BrowseNode ns)
    {
        var entries = new List<NamespaceEntry>();
        foreach (var child in BrowseSessions.ListChildren(session, ns))
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
    private static List<UsedEntry> ListUsed(BrowseSession session, BrowseNode node)
    {
        return BrowseSessions.ListChildren(session, node)
            .Select(c => UsedOf(session, c))
            .ToList();
    }

    private static UsedEntry UsedOf(BrowseSession session, BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.ForwardedType forwarded => new UsedEntry.Forwarded(ForwardedLineOf(node, forwarded)),
            NodeSubject.TypeReference type => new UsedEntry.Type(type.Value.FullName, NodeDefinitions.FindTarget(node.Subject), ListUsed(session, node)),
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
