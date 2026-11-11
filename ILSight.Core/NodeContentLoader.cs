using System.Diagnostics;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>What a <see cref="BrowseNode"/> shows: a <see cref="DocumentContent"/>, a
/// <see cref="GridContent"/> or an <see cref="ImageContent"/>.</summary>
public abstract record NodeContent;

public sealed record DocumentContent(DecompiledDocument Document) : NodeContent;

public sealed record GridContent(MetadataGrid Grid) : NodeContent;

/// <summary>A picture, and for one stored as markup, such as SVG, that markup as a document.</summary>
public sealed record ImageContent(ResourceImage Image, DecompiledDocument? Source = null) : NodeContent;

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
    public static Task<NodeContent> LoadAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Load(session, node, language, options, cancellationToken), cancellationToken);
    }

    /// <summary>Decompiles <paramref name="node"/> with <paramref name="options"/>, which only C# reads.
    /// Throws <see cref="OperationCanceledException"/> when cancelled; any other failure becomes a
    /// document describing it.</summary>
    public static DecompiledDocument Decompile(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        return NodeDecompiler.Decompile(RequestOf(session, node), language, options, cancellationToken);
    }

    /// <summary>Whether <see cref="LoadAsync"/> gives decompiled code, which changes with the language
    /// and options, rather than a grid, a picture or why the file failed to load, which do not.</summary>
    public static bool DependsOnLanguage(BrowseNode node)
    {
        return GridOf(node) is null && !IsFailedFile(node) && !IsImage(node);
    }

    private static bool IsFailedFile(BrowseNode node)
    {
        return node.Subject is NodeSubject.FailedAssembly;
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

    /// <summary>Writes <paramref name="node"/>'s code in <paramref name="language"/> to the file at
    /// <paramref name="path"/>: for an assembly its whole module, for a namespace all its types, else
    /// what the node shows. Throws when decompiling or writing fails.</summary>
    public static void SaveCode(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options, string path,
        CancellationToken cancellationToken = default)
    {
        NodeDecompiler.SaveCode(ExportOf(session, node), language, options, path, cancellationToken);
    }

    /// <summary>The name a file of <paramref name="node"/>'s code in <paramref name="language"/> is
    /// offered under: the node's name, made safe for a file name, with the language's extension.</summary>
    public static string SuggestCodeFileName(BrowseNode node, DecompilationLanguage language)
    {
        var name = IsModule(node) ? node.Assembly.Name : BrowseNodes.GetEntity(node)?.Name ?? node.Text;
        var extension = DecompilationLanguages.IsIL(language) ? ".il" : ".cs";
        return WholeProjectDecompiler.CleanUpFileName(name, extension);
    }

    private static bool IsModule(BrowseNode node)
    {
        return node.Subject is NodeSubject.Module;
    }

    private static NodeContent Load(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken)
    {
        var grid = GridOf(node);
        if (grid is not null)
        {
            return new GridContent(grid);
        }

        if (!IsImage(node))
        {
            return new DocumentContent(Decompile(session, node, language, options, cancellationToken));
        }

        try
        {
            return LoadImage(session, node, language, options, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new DocumentContent(NodeDecompiler.Decompile(RequestOf(session, node), language, options, cancellationToken, e.Message));
        }
    }

    private static bool IsImage(BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.ResourceEntry entry => entry.Value.IsImage,
            NodeSubject.PackageFile file => file.Value.IsImage,
            _ => false,
        };
    }

    /// <summary>The picture, with the markup it is stored as when it is drawn from shapes.</summary>
    private static ImageContent LoadImage(BrowseSession session, BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        CancellationToken cancellationToken)
    {
        var image = ResourceImages.DecodeImage(BrowseNodes.ReadResource(node));
        if (!image.IsVector)
        {
            return new ImageContent(image);
        }

        var source = Decompile(session, node, language, options, cancellationToken);
        return new ImageContent(image, source);
    }

    private static CodeExport ExportOf(BrowseSession session, BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.Module module => new CodeExport.WholeModule(module.Value),
            NodeSubject.Namespace ns => new CodeExport.Namespace(ns.Value.Module, ns.Value.Name, ns.Value.Types),
            _ => new CodeExport.Document(RequestOf(session, node)),
        };
    }

    private static DocumentRequest RequestOf(BrowseSession session, BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.TypeDefinition type => new DocumentRequest.Type(type.Definition, node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.MemberDefinition member => new DocumentRequest.Member(member.Definition, node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
                or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType =>
                new DocumentRequest.Summary(SummaryOf(session, node), node.SourceModule, node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.Resources => new DocumentRequest.Summary(DescribeResources(session, node), node.SourceModule, node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.ResourceContainer container => container.Value.Contents switch
            {
                ContainerContents.AvaloniaAssets => new DocumentRequest.Summary(DescribeResources(session, node), node.SourceModule, node.Text, BrowseNodes.GetFullName(node)),
                ContainerContents.ResourcesFile => ResourceRequest(node, ResourceNodes.CreateData(container.Value)),
                _ => throw new UnreachableException(),
            },
            NodeSubject.ResourceEntry entry => entry.Value.Source switch
            {
                ResourceSource.Baml baml => new DocumentRequest.Baml(entry.Value.Module, baml.Data, node.Text, BrowseNodes.GetFullName(node)),
                ResourceSource.Manifest or ResourceSource.Entry => ResourceRequest(node, ResourceNodes.CreateData(entry.Value)),
                _ => throw new UnreachableException(),
            },
            NodeSubject.Metadata metadata => new DocumentRequest.Summary(new SummaryInfo.Comments(MetadataTables.ListFolderLines(metadata.Value)), node.SourceModule, node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.MetadataTables tables => new DocumentRequest.Summary(new SummaryInfo.Comments(MetadataTables.ListTablesLines(tables.Folder)), node.SourceModule, node.Text,
                BrowseNodes.GetFullName(node)),
            NodeSubject.MetadataTable table => new DocumentRequest.Summary(new SummaryInfo.Comments(MetadataTables.ListPartLines(table.Value)), node.SourceModule, node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.Package => new DocumentRequest.Summary(new SummaryInfo.Comments(BrowseSessions.ListChildren(session, node).Select(DescribePackageChild).ToList()), null,
                node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.PackageFile file => ResourceRequest(node, ResourceNodes.CreateData(file.Value)),
            NodeSubject.Module module => new DocumentRequest.ModulePreview(module.Value, node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.FailedAssembly failed => new DocumentRequest.Failure(failed.Value, node.Text, BrowseNodes.GetFullName(node)),
            NodeSubject.Namespace => new DocumentRequest.Summary(new SummaryInfo.Namespace(node.Text, ListNamespaceEntries(session, node)), node.SourceModule,
                node.Text, BrowseNodes.GetFullName(node)),
            _ => throw new UnreachableException(),
        };
    }

    private static DocumentRequest.Resource ResourceRequest(BrowseNode node, ResourceData resource)
    {
        return new DocumentRequest.Resource(node.SourceModule, resource.Description, resource.ReadData, node.Text, BrowseNodes.GetFullName(node));
    }

    /// <summary>A comment a resource, describing each that <paramref name="node"/> lists.</summary>
    private static SummaryInfo.Comments DescribeResources(BrowseSession session, BrowseNode node)
    {
        return new SummaryInfo.Comments(BrowseSessions.ListChildren(session, node).Select(static c => BrowseNodes.GetResourceData(c)).OfType<ResourceData>().Select(static r => r.Description).ToList());
    }

    /// <summary>What the summary of <paramref name="node"/>, which has no code of its own, lists.</summary>
    private static SummaryInfo SummaryOf(BrowseSession session, BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.References => new SummaryInfo.References(
                ChildrenAbout<NodeSubject.AssemblyReference>(session, node).Select(static r => new SummaryLink(BrowseNodes.GetFullName(r), NodeDefinitions.FindTarget(r.Subject))).ToList(),
                ChildrenAbout<NodeSubject.ModuleReference>(session, node).Select(static m => m.Text).ToList()),
            NodeSubject.AssemblyReference reference => new SummaryInfo.AssemblyReference(reference.Value.Module, reference.Value.Reference),
            NodeSubject.ReferencedTypes => new SummaryInfo.Used(ListUsed(session, node)),
            NodeSubject.TypeReference or NodeSubject.MemberReference => new SummaryInfo.Used([UsedOf(session, node)]),
            NodeSubject.ModuleReference module => new SummaryInfo.ModuleReference(module.Value.Name, module.Value.ContainsMetadata,
                ChildrenAbout<NodeSubject.ImportedMethod>(session, node).Select(static i => new SummaryLink(BrowseNodes.GetFullName(i), NodeDefinitions.FindTarget(i.Subject))).ToList()),
            NodeSubject.ImportedMethod import => new SummaryInfo.ImportedMethod(new SummaryLink(BrowseNodes.GetFullName(node), NodeDefinitions.FindTarget(node.Subject)), import.Value.EntryPoint,
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
        var file = BrowseNodes.GetResourceData(child);
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
            if (NodeDefinitions.GetVisibility(child.Subject).IsCompilerGenerated)
            {
                continue;
            }

            if (child.Subject is NodeSubject.TypeDefinition type)
            {
                entries.Add(new NamespaceEntry.Declared(type.Definition.Symbol, child.Text, type.Definition.Reference));
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
            NodeSubject.MemberReference member => new UsedEntry.Member(node.Text, UsedMemberSpan(member.Value.Declared), NodeDefinitions.FindTarget(node.Subject)),
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
