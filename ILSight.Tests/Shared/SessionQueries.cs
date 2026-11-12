using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

/// <summary>The tree of <see cref="TestSession.Shared"/> read as members of what it holds: an assembly's
/// root, a node's children, and the nodes a list's paths, references and ids name.</summary>
internal static class SessionQueries
{
    extension(LoadedAssembly assembly)
    {
        public BrowseNode Root => BrowseSessions.EnsureRoot(TestSession.Shared, assembly);

        public BrowseNode? FindNode(NodePath path)
        {
            return BrowseSessions.FindNode(TestSession.Shared, assembly, path);
        }
    }

    extension(BrowseNode node)
    {
        /// <summary>The children, built on this thread when nobody built them yet; none when they failed.</summary>
        public IReadOnlyList<BrowseNode> Children => BrowseSessions.ListChildren(TestSession.Shared, node);

        public bool HasChildren => TestSession.Shared.HasChildren(node);

        public ChildrenState ChildrenState => TestSession.Shared.PeekChildren(node);

        /// <summary>Builds the node's children; fails the test when they can't be built.</summary>
        public void EnsureChildren()
        {
            Assert.IsType<ChildrenState.Ready>(BrowseSessions.GetChildren(TestSession.Shared, node));
        }

        public IReadOnlyList<BrowseNode> InnerNamespaces => TestSession.Shared.ListInnerNamespaces(node);

        /// <summary>What sort of type the node defines; fails the test when it defines none.</summary>
        public TypeShape TypeShape => Assert.IsType<NodeSubject.TypeDefinition>(node.Subject).Definition.Shape;

        /// <summary>What sort of member the node defines; fails the test when it defines none.</summary>
        public MemberShape MemberShape => Assert.IsType<NodeSubject.MemberDefinition>(node.Subject).Definition.Shape;

        /// <summary>What the part of metadata the node stands for lists; fails the test when it stands for none.</summary>
        public MetadataGridRole MetadataRole => Assert.IsType<NodeSubject.MetadataTable>(node.Subject).Value.Role;

        public bool Is(TypeShape shape)
        {
            return node.Subject is NodeSubject.TypeDefinition type && type.Definition.Shape == shape;
        }

        public bool Is(MemberShape shape)
        {
            return node.Subject is NodeSubject.MemberDefinition member && member.Definition.Shape == shape;
        }

        public bool Is(MetadataGridRole role)
        {
            return node.Subject is NodeSubject.MetadataTable table && table.Value.Role == role;
        }

        /// <summary>Whether the node is a resource or a package's file shown as a picture.</summary>
        public bool IsImage
        {
            get
            {
                if (node.Subject is NodeSubject.ResourceEntry entry)
                {
                    return entry.Value.IsImage;
                }

                if (node.Subject is NodeSubject.PackageFile file)
                {
                    return file.Value.IsImage;
                }

                return false;
            }
        }

        /// <summary>A reference to the type or member the node defines; fails the test when it defines none.</summary>
        public CodeReference.Entity Definition => Assert.IsType<CodeReference.Entity>(node.Reference);

        public NodeAccessibility Accessibility => node.Visibility.Accessibility;

        public bool IsPublicApi => node.Visibility.IsPublicApi;

        public bool IsCompilerGenerated => node.Visibility.IsCompilerGenerated;

        /// <summary>The metadata token of what the node defines; null when it defines nothing.</summary>
        public int? Token => node.Token;

        /// <summary>Where activating the node goes; fails the test when it opens itself.</summary>
        public CodeReference.Navigable Destination => Assert.IsType<Activation.Navigate>(node.Activation).Target;
    }

    extension(AssemblyList list)
    {
        public BrowseNode? FindNode(NodePath path)
        {
            return NodeLookup.Find(list, TestSession.Shared, path);
        }

        public BrowseNode? FindNode(CodeReference.Navigable reference)
        {
            return NodeLookup.Find(list, TestSession.Shared, reference);
        }

        public BrowseNode? FindListedNode(NodePath path)
        {
            return NodeLookup.FindListed(list, TestSession.Shared, path);
        }

        public BrowseNode? FindNodeById(string idString, IEnumerable<LoadedModule> assemblies)
        {
            return NodeLookup.FindById(TestSession.Shared, idString, assemblies);
        }

        public MetadataTarget? FindMetadataRow(CodeReference.Navigable reference)
        {
            return NodeLookup.FindMetadataRow(list, TestSession.Shared, reference);
        }

        public IAsyncEnumerable<IReadOnlyList<SearchFind>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
        {
            return AssemblySearch.SearchAsync(TestSession.Shared, list.Assemblies, query, cancellationToken);
        }

        public IAsyncEnumerable<IReadOnlyList<SearchFind>> SearchAsync(SearchQuery query, IEnumerable<LoadedModule> assemblies,
            CancellationToken cancellationToken = default)
        {
            return AssemblySearch.SearchAsync(TestSession.Shared, assemblies, query, cancellationToken);
        }

        public IAsyncEnumerable<IReadOnlyList<SearchFind>> SearchAsync(string text, SearchKinds kinds = SearchKinds.TypesAndMembers,
            CancellationToken cancellationToken = default)
        {
            return AssemblySearch.SearchAsync(TestSession.Shared, list.Assemblies, SearchQueries.ParseQuery(text, kinds), cancellationToken);
        }
    }
}
