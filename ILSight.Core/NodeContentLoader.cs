using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.MetadataArea;
using ILSight.Core.ResourceArea;

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
    /// closed, reading nothing, when it was released (closed) before. What the library throws reading the grid, or what
    /// the document is made from, is the node's <see cref="Outcome{T}.Failed"/>; what the decompiler throws writing the
    /// document becomes a document describing it. The notes the app writes beside the code are in
    /// <paramref name="notes"/>' words.</summary>
    public static Task<Outcome<ModuleRead<NodeContent>>> ReadContentAsync(BrowseSession session, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, IDocumentNotes notes, int firstGridRows, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ReadContentUnderReadingAsync(session, node, language, options, notes, firstGridRows, cancellationToken), cancellationToken);
    }

    /// <summary>Decompiles <paramref name="node"/> with <paramref name="options"/>, which only C# reads, once the read
    /// of its module's type system ended. Throws <see cref="OperationCanceledException"/> when cancelled; what the
    /// library throws reading what the document is made from is the node's <see cref="Outcome{T}.Failed"/>, and what the
    /// decompiler throws writing it becomes a document describing it. A unit reading the node's module, as
    /// <see cref="ReadContentAsync(BrowseSession, BrowseNode, DecompilationLanguage, DecompilerOptions, IDocumentNotes, int, CancellationToken)"/> is,
    /// its notes in <paramref name="notes"/>' words.</summary>
    public static async Task<Outcome<ModuleRead<DecompiledDocument>>> DecompileAsync(BrowseSession session, BrowseNode node,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, CancellationToken cancellationToken = default)
    {
        using var reading = LoadedAssemblies.BeginReading();
        if (!TryReadModuleOf(reading, node, out var closed))
        {
            return new Outcome<ModuleRead<DecompiledDocument>>.Ok(new ModuleRead<DecompiledDocument>.Closed { Module = closed });
        }

        var decompiled = await DecompileAsync(session, reading, node, language, options, notes, null, cancellationToken);
        return ToModuleRead(decompiled);
    }

    private static Outcome<ModuleRead<T>> ToModuleRead<T>(Outcome<T> read)
    {
        return read switch
        {
            Outcome<T>.Ok ok => new Outcome<ModuleRead<T>>.Ok(new ModuleRead<T>.Read { Value = ok.Value }),
            Outcome<T>.Failed failed => new Outcome<ModuleRead<T>>.Failed(failed.Error),
        };
    }

    private static bool TryReadModuleOf(ModuleReadLease reading, BrowseNode node, [NotNullWhen(false)] out LoadedModule? closed)
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

    private static async Task<Outcome<ModuleRead<NodeContent>>> ReadContentUnderReadingAsync(BrowseSession session, BrowseNode node,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, int firstGridRows, CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.BeginReading();
        if (!TryReadModuleOf(reading, node, out var closed))
        {
            return new Outcome<ModuleRead<NodeContent>>.Ok(new ModuleRead<NodeContent>.Closed { Module = closed });
        }

        var content = await ReadContentAsync(session, reading, node, language, options, notes, firstGridRows, cancellationToken);
        return ToModuleRead(content);
    }

    private static async Task<Outcome<DecompiledDocument>> DecompileAsync(BrowseSession session, ModuleReadLease reading, BrowseNode node,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, DocumentNote? note, CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync(session, node, cancellationToken);
        return request switch
        {
            Outcome<DocumentRequest>.Ok ok => await DecompileRequestAsync(reading, ok.Value, language, options, notes, note, cancellationToken),
            Outcome<DocumentRequest>.Failed failed => new Outcome<DecompiledDocument>.Failed(failed.Error),
        };
    }

    private static async Task<Outcome<DecompiledDocument>> DecompileRequestAsync(ModuleReadLease reading, DocumentRequest request,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, DocumentNote? note, CancellationToken cancellationToken)
    {
        return new Outcome<DecompiledDocument>.Ok(await NodeDecompiler.DecompileAsync(reading, request, language, options, notes, cancellationToken, note));
    }

    // The node's grid, null for a node shown otherwise. Its reads are caught where the grid is made
    // (MetadataGrids.CreateGrid), what the library throws the content's failure; the grid is built after them.
    private static Outcome<NodeContent>? FindGridContent(BrowseNode node, int firstRows, CancellationToken ct)
    {
        return GridOf(node, firstRows, ct) switch
        {
            null => null,
            Outcome<MetadataGrid>.Ok ok => new Outcome<NodeContent>.Ok(new NodeContent.Table(ok.Value)),
            Outcome<MetadataGrid>.Failed failed => new Outcome<NodeContent>.Failed(failed.Error),
        };
    }

    private static Outcome<MetadataGrid>? GridOf(BrowseNode node, int firstRows, CancellationToken ct)
    {
        if (node.Subject is NodeSubject.MetadataTable table)
        {
            return MetadataTables.EnsureGrid(table.Value, firstRows, ct);
        }

        if (node.Subject is NodeSubject.ResourceContainer container)
        {
            return ResourceNodes.EnsureGrid(container.Value, firstRows, ct);
        }

        return null;
    }

    private static async Task<Outcome<NodeContent>> ReadContentAsync(BrowseSession session, ModuleReadLease reading, BrowseNode node,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, int firstGridRows, CancellationToken cancellationToken)
    {
        var grid = FindGridContent(node, firstGridRows, cancellationToken);
        if (grid is not null)
        {
            return grid.Value;
        }

        if (!BrowseNodes.IsImage(node.Subject))
        {
            return ToCode(await DecompileAsync(session, reading, node, language, options, notes, null, cancellationToken));
        }

        var read = ReadImage(node, cancellationToken);
        return await read.Match(
            decoded => GetPictureAsync(session, reading, node, decoded.Image, language, options, notes, cancellationToken),
            unreadable => DecompileWithNoteAsync(session, reading, node, language, options, notes, new DocumentNote.PictureUnreadable(unreadable.Fault), cancellationToken));
    }

    private static async Task<Outcome<NodeContent>> DecompileWithNoteAsync(BrowseSession session, ModuleReadLease reading, BrowseNode node,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, DocumentNote note, CancellationToken cancellationToken)
    {
        return ToCode(await DecompileAsync(session, reading, node, language, options, notes, note, cancellationToken));
    }

    private static Outcome<NodeContent> ToCode(Outcome<DecompiledDocument> decompiled)
    {
        return decompiled switch
        {
            Outcome<DecompiledDocument>.Ok ok => new Outcome<NodeContent>.Ok(new NodeContent.Code(ok.Value)),
            Outcome<DecompiledDocument>.Failed failed => new Outcome<NodeContent>.Failed(failed.Error),
        };
    }

    // The app's check that the node is a resource runs before the catch; the decompiler reads the resource's bytes inside
    // it; the decoders read the picture in them after it, each catching its own failures (ResourceImages.DecodeImage).
    private static ResourceImageRead ReadImage(BrowseNode node, CancellationToken ct)
    {
        var resource = BrowseNodes.RequireResource(node);
        byte[] bytes;
        try
        {
            bytes = resource.ReadData();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new ResourceImageRead.Unreadable { Fault = new PictureFault.LibraryFailure(e.Message), Error = e };
        }

        return ResourceImages.DecodeImage(bytes);
    }

    private static async Task<Outcome<NodeContent>> GetPictureAsync(BrowseSession session, ModuleReadLease reading, BrowseNode node, ResourceImage image,
        DecompilationLanguage language, DecompilerOptions options, IDocumentNotes notes, CancellationToken cancellationToken)
    {
        if (!image.IsVector)
        {
            return new Outcome<NodeContent>.Ok(new NodeContent.Picture { Image = image });
        }

        var source = await DecompileAsync(session, reading, node, language, options, notes, null, cancellationToken);
        return source switch
        {
            Outcome<DecompiledDocument>.Ok ok => new Outcome<NodeContent>.Ok(new NodeContent.Picture { Image = image, Source = ok.Value }),
            Outcome<DecompiledDocument>.Failed failed => new Outcome<NodeContent>.Failed(failed.Error),
        };
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
        return RequestOf(node, children, ReadFacts(node, children));
    }

    // What the request needs of the library's objects is read inside the catch (ReadFacts: the targets the summary links
    // to, a used member's kind, a metadata part's lines); the request is built of it after, so a bug of the app's in
    // building it reaches the edge.
    private static async Task<Outcome<DocumentRequest>> ReadRequestAsync(BrowseSession session, BrowseNode node, CancellationToken ct)
    {
        var children = await ListSummarizedChildrenAsync(session, node, ct);
        RequestFacts facts;
        try
        {
            facts = ReadFacts(node, children);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<DocumentRequest>.Failed(e);
        }

        return new Outcome<DocumentRequest>.Ok(RequestOf(node, children, facts));
    }

    // What building the request reads of the library's objects, for the node and the children its summary lists.
    private static RequestFacts ReadFacts(BrowseNode node, SummarizedChildren children)
    {
        var targets = new Dictionary<BrowseNode, CodeReference.Navigable?>(ReferenceEqualityComparer.Instance);
        var kinds = new Dictionary<BrowseNode, SpanKind>(ReferenceEqualityComparer.Instance);
        ReadNodeFacts(node, targets, kinds);
        ReadChildrenFacts(children, targets, kinds);
        return new RequestFacts { Targets = targets, MemberKinds = kinds, Lines = ReadLines(node.Subject) };
    }

    private static void ReadChildrenFacts(SummarizedChildren children, Dictionary<BrowseNode, CodeReference.Navigable?> targets,
        Dictionary<BrowseNode, SpanKind> kinds)
    {
        foreach (var child in children.Children)
        {
            ReadNodeFacts(child, targets, kinds);
            if (children.Nested.TryGetValue(child, out var nested))
            {
                ReadChildrenFacts(nested, targets, kinds);
            }
        }
    }

    private static void ReadNodeFacts(BrowseNode node, Dictionary<BrowseNode, CodeReference.Navigable?> targets, Dictionary<BrowseNode, SpanKind> kinds)
    {
        targets[node] = NodeDefinitions.FindTarget(node.Subject);
        if (node.Subject is NodeSubject.MemberReference member)
        {
            kinds[node] = UsedMemberSpan(NodeDefinitions.GetDeclared(member.Value));
        }
    }

    // A metadata folder's, its tables' or a part's comment lines, read through its file; null for any other node.
    private static List<string>? ReadLines(NodeSubject subject)
    {
        if (subject is NodeSubject.Metadata metadata)
        {
            return MetadataTables.ListFolderLines(metadata.Value).ToList();
        }

        if (subject is NodeSubject.MetadataTables tables)
        {
            return MetadataTables.ListTablesLines(tables.Folder).ToList();
        }

        if (subject is NodeSubject.MetadataTable table)
        {
            return MetadataTables.ListPartLines(table.Value).ToList();
        }

        // Every other node's request is built without lines.
        return null;
    }

    // What the request is built of from the library's objects, read before it is built.
    private sealed class RequestFacts
    {
        public required IReadOnlyDictionary<BrowseNode, CodeReference.Navigable?> Targets { get; init; }

        public required IReadOnlyDictionary<BrowseNode, SpanKind> MemberKinds { get; init; }

        public required IReadOnlyList<string>? Lines { get; init; }

        public IReadOnlyList<string> LinesRead => Lines ?? throw new UnreachableException("A metadata node's lines are read with its facts.");
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

    private static DocumentRequest RequestOf(BrowseNode node, SummarizedChildren children, RequestFacts facts)
    {
        return new DocumentRequest { Title = node.Text, FullName = node.FullName, Subject = SubjectOf(node, children, facts) };
    }

    private static DocumentSubject SubjectOf(BrowseNode node, SummarizedChildren children, RequestFacts facts)
    {
        return node.Subject switch
        {
            NodeSubject.Module module => new DocumentSubject.ModulePreview { Module = module.Value },
            NodeSubject.Namespace => SummarySubject(node, new SummaryInfo.Namespace
            {
                Name = node.Text,
                Entries = ListNamespaceEntries(children, facts),
            }),
            NodeSubject.FailedAssembly failed => new DocumentSubject.Failure { Assembly = failed.Value },
            NodeSubject.TypeDefinition type => new DocumentSubject.Type { Definition = type.Definition },
            NodeSubject.MemberDefinition member => new DocumentSubject.Member { Definition = member.Definition },
            NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
                or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType => SummarySubject(node, SummaryOf(node, children, facts)),
            NodeSubject.UnreadableModule => new DocumentSubject.Summary
            {
                Info = new SummaryInfo.Comments([node.Text]),
                Module = null,
            },
            NodeSubject.Resources => SummarySubject(node, DescribeResources(children)),
            NodeSubject.ResourceContainer container => ContainerSubject(node, children, container.Value),
            NodeSubject.ResourceEntry entry => EntrySubject(node, entry.Value),
            NodeSubject.Metadata or NodeSubject.MetadataTables or NodeSubject.MetadataTable => SummarySubject(node, new SummaryInfo.Comments(facts.LinesRead)),
            NodeSubject.Package => new DocumentSubject.Summary
            {
                Info = new SummaryInfo.Comments(children.Children.Select(DescribePackageChild).ToList()),
                Module = null,
            },
            NodeSubject.PackageFile file => ResourceSubject(node, ResourceNodes.CreateData(file.Value)),
        };
    }

    private static DocumentSubject.Summary SummarySubject(BrowseNode node, SummaryInfo summary)
    {
        return new DocumentSubject.Summary
        {
            Info = summary,
            Module = node.SourceModule,
        };
    }

    private static DocumentSubject ContainerSubject(BrowseNode node, SummarizedChildren children, ResourceContainerInfo container)
    {
        return container.Contents.Match<(BrowseNode Node, SummarizedChildren Children, ResourceContainerInfo Container), DocumentSubject>(
            (node, children, container),
            static (s, _) => ResourceSubject(s.Node, ResourceNodes.CreateData(s.Container)),
            static (s, _) => SummarySubject(s.Node, DescribeResources(s.Children)));
    }

    private static DocumentSubject EntrySubject(BrowseNode node, ResourceEntryInfo entry)
    {
        return entry.Source.Match<(BrowseNode Node, ResourceEntryInfo Entry), DocumentSubject>((node, entry),
            static (s, _) => ResourceSubject(s.Node, ResourceNodes.CreateData(s.Entry)),
            static (s, _) => ResourceSubject(s.Node, ResourceNodes.CreateData(s.Entry)),
            static (s, baml) => new DocumentSubject.Baml
            {
                Module = s.Entry.Module,
                Data = baml.Data,
            });
    }

    private static DocumentSubject.Resource ResourceSubject(BrowseNode node, ResourceData resource)
    {
        return new DocumentSubject.Resource
        {
            Module = node.SourceModule,
            Description = resource.Description,
            Read = resource.ReadData.Invoke,
        };
    }

    private static SummaryInfo.Comments DescribeResources(SummarizedChildren children)
    {
        return new SummaryInfo.Comments(children.Children.Select(static c => BrowseNodes.FindResourceData(c.Subject)).OfType<ResourceData>().Select(static r => r.Description).ToList());
    }

    private static SummaryInfo SummaryOf(BrowseNode node, SummarizedChildren children, RequestFacts facts)
    {
        return node.Subject switch
        {
            NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly or NodeSubject.TypeDefinition
                or NodeSubject.MemberDefinition or NodeSubject.UnreadableModule or NodeSubject.Resources or NodeSubject.ResourceContainer
                or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables or NodeSubject.MetadataTable
                or NodeSubject.Package or NodeSubject.PackageFile => throw new UnreachableException(),
            NodeSubject.References => new SummaryInfo.References
            {
                Assemblies = ChildrenAbout<NodeSubject.AssemblyReference>(children).Select(r => new SummaryLink
                {
                    Text = r.FullName,
                    Target = facts.Targets[r],
                }).ToList(),
                Modules = ChildrenAbout<NodeSubject.ModuleReference>(children).Select(static m => m.Text).ToList(),
            },
            NodeSubject.AssemblyReference reference => new SummaryInfo.AssemblyReference
            {
                Module = reference.Value.Module,
                Reference = reference.Value.Reference,
            },
            NodeSubject.ReferencedTypes => new SummaryInfo.Used(ListUsed(children, facts)),
            NodeSubject.TypeReference or NodeSubject.MemberReference => new SummaryInfo.Used([UsedOf(node, children, facts)]),
            NodeSubject.ModuleReference module => new SummaryInfo.ModuleReference
            {
                Name = module.Value.Name,
                ContainsMetadata = module.Value.ContainsMetadata,
                Imports = ChildrenAbout<NodeSubject.ImportedMethod>(children).Select(i => new SummaryLink
                {
                    Text = i.FullName,
                    Target = facts.Targets[i],
                }).ToList(),
            },
            NodeSubject.ImportedMethod import => new SummaryInfo.ImportedMethod
            {
                Method = new SummaryLink
                {
                    Text = node.FullName,
                    Target = facts.Targets[node],
                },
                EntryPoint = import.Value.EntryPoint,
                ModuleName = import.Value.ModuleName,
            },
            NodeSubject.ForwardedType forwarded => new SummaryInfo.ForwardedType
            {
                FullName = forwarded.Value.FullName,
                Target = facts.Targets[node],
                Destination = forwarded.Value.Destination,
            },
            NodeSubject.BaseTypes or NodeSubject.DerivedTypes => TypesOf(children, facts),
            NodeSubject.BaseType or NodeSubject.DerivedType => new SummaryInfo.Types([new SummaryLink
            {
                Text = node.Text,
                Target = facts.Targets[node],
            }]),
        };
    }

    private static SummaryInfo.Types TypesOf(SummarizedChildren children, RequestFacts facts)
    {
        return new SummaryInfo.Types(children.Children.Select(c => new SummaryLink
        {
            Text = c.Text,
            Target = facts.Targets[c],
        }).ToList());
    }

    private static IEnumerable<BrowseNode> ChildrenAbout<TSubject>(SummarizedChildren children) where TSubject : class
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

    private static List<NamespaceEntry> ListNamespaceEntries(SummarizedChildren children, RequestFacts facts)
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
                entries.Add(new NamespaceEntry.Forwarded(ForwardedLineOf(child, forwarded, facts)));
            }
        }
        return entries;
    }

    private static List<UsedEntry> ListUsed(SummarizedChildren children, RequestFacts facts)
    {
        return children.Children
            .Select(c => UsedOf(c, children.Nested.GetValueOrDefault(c) ?? SummarizedChildren.None, facts))
            .ToList();
    }

    private static UsedEntry UsedOf(BrowseNode node, SummarizedChildren children, RequestFacts facts)
    {
        if (node.Subject is NodeSubject.ForwardedType forwarded)
        {
            return new UsedEntry.Forwarded(ForwardedLineOf(node, forwarded, facts));
        }

        if (node.Subject is NodeSubject.TypeReference type)
        {
            return new UsedEntry.Type
            {
                FullName = type.Value.FullName,
                Target = facts.Targets[node],
                Used = ListUsed(children, facts),
            };
        }

        if (node.Subject is NodeSubject.MemberReference)
        {
            return new UsedEntry.Member
            {
                Label = node.Text,
                Kind = facts.MemberKinds[node],
                Target = facts.Targets[node],
            };
        }

        throw new UnreachableException();
    }

    private static ForwardedLine ForwardedLineOf(BrowseNode node, NodeSubject.ForwardedType forwarded, RequestFacts facts)
    {
        return new ForwardedLine
        {
            Label = forwarded.Value.Label,
            Target = facts.Targets[node],
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
