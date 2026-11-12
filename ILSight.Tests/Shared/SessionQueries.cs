using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

/// <summary>The tree of <see cref="TestSession.Shared"/> read as members of what it holds: an assembly's
/// root, a node's children, and the nodes a list's paths, references and ids name, each an async read the test
/// awaits (a node's children also of a node still being read, so the reads chain); a lookup that meets a module
/// that could not be read fails the test (<see cref="LookupQueries"/>).</summary>
internal static class SessionQueries
{
    extension(LoadedAssembly assembly)
    {
        /// <summary>The assembly's root, built when nobody built it yet.</summary>
        public Task<BrowseNode> ReadRootAsync()
        {
            return BrowseSessions.EnsureRootAsync(TestSession.Shared, assembly);
        }

        public async Task<BrowseNode?> FindNodeAsync(NodePath path)
        {
            var found = await BrowseSessions.FindNodeAsync(TestSession.Shared, assembly, path);
            return found.FoundOrNull;
        }
    }

    extension(Task<BrowseNode> read)
    {
        /// <summary>The children of the node <paramref name="read"/> gives, built when nobody built them yet; none
        /// when they failed.</summary>
        public async Task<IReadOnlyList<BrowseNode>> ListChildrenAsync()
        {
            var node = await read;
            return await node.ListChildrenAsync();
        }

        /// <summary>Builds the children of the node <paramref name="read"/> gives; fails the test when they can't be
        /// built.</summary>
        public async Task EnsureChildrenAsync()
        {
            var node = await read;
            await node.EnsureChildrenAsync();
        }
    }

    extension(BrowseNode node)
    {
        public bool HasChildren => TestSession.Shared.HasChildren(node);

        public ChildrenState ChildrenState => TestSession.Shared.PeekChildren(node);

        /// <summary>The children, built when nobody built them yet; none when they failed.</summary>
        public Task<IReadOnlyList<BrowseNode>> ListChildrenAsync()
        {
            return BrowseSessions.ListChildrenAsync(TestSession.Shared, node);
        }

        /// <summary>Builds the node's children; fails the test when they can't be built.</summary>
        public async Task EnsureChildrenAsync()
        {
            var children = await BrowseSessions.GetChildrenAsync(TestSession.Shared, node);
            Assert.IsType<ChildrenState.Ready>(children);
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
        public async Task<BrowseNode?> FindNodeAsync(NodePath path)
        {
            var found = await NodeLookup.FindAsync(list, TestSession.Shared, path, CancellationToken.None);
            return found.FoundOrNull;
        }

        public async Task<BrowseNode?> FindNodeAsync(CodeReference.Navigable reference)
        {
            var found = await NodeLookup.FindAsync(list, TestSession.Shared, reference, CancellationToken.None);
            return found.FoundOrNull;
        }

        public async Task<BrowseNode?> FindListedNodeAsync(NodePath path)
        {
            var found = await NodeLookup.FindListedAsync(list, TestSession.Shared, path, CancellationToken.None);
            return found.FoundOrNull;
        }

        public async Task<BrowseNode?> FindNodeByIdAsync(string idString, IEnumerable<LoadedModule> assemblies)
        {
            var found = await NodeLookup.FindByIdAsync(TestSession.Shared, idString, assemblies, CancellationToken.None);
            return found.FoundOrNull;
        }

        public async Task<MetadataTarget?> FindMetadataRowAsync(CodeReference.Navigable reference)
        {
            var found = await NodeLookup.FindMetadataRowAsync(list, TestSession.Shared, reference, CancellationToken.None);
            return found.FoundValueOrNull;
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
