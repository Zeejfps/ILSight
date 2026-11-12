using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

/// <summary>
/// The dependency graph of the browse tree, read from Core's own metadata and IL: what each type names in
/// its signatures and method bodies, nested types and lambdas counted as the type they are in. No arrow
/// points back up:
/// <code>
/// UI → BrowseSession, NodeContentLoader
/// NodeContentLoader → BrowseSession, NodeDecompiler, MetadataGrid, ResourceImage
/// BrowseSession → BrowseNode → NodeSubject, LoadedModule (reading only)
/// NodeDecompiler, MetadataGrid → domain data (LoadedModule, type system)
/// </code>
/// A type there holds data, so the functions that do what it means, <see cref="BrowseSessions"/>,
/// <see cref="BrowseNodes"/>, <see cref="LoadedAssemblies"/> and <see cref="AssemblyListing"/>, are held to
/// where their types stand:
/// <code>
/// BrowseSessions → BrowseNodes → LoadedAssemblies, AssemblyListing
/// </code>
/// </summary>
public sealed class DependencyTests
{
    private static readonly Lazy<TypeUseGraph> Graph = new(static () => TypeUseGraph.Read(typeof(BrowseNode).Assembly.Location));

    public static IEnumerable<object[]> ForbiddenEdges()
    {
        return
        [
            [typeof(NodeDecompiler), typeof(BrowseNode)],
            [typeof(NodeDecompiler), typeof(BrowseSession)],
            [typeof(NodeDecompiler), typeof(NodeSubject)],
            [typeof(NodeDecompiler), typeof(NodeContentLoader)],
            [typeof(MetadataGrid), typeof(BrowseNode)],
            [typeof(MetadataGrid), typeof(BrowseSession)],
            [typeof(MetadataGrid), typeof(NodeSubject)],
            [typeof(MetadataGrid), typeof(NodeContentLoader)],
            [typeof(BrowseSession), typeof(NodeContentLoader)],
            [typeof(BrowseSession), typeof(NodeDecompiler)],
            [typeof(BrowseNode), typeof(BrowseSession)],
            [typeof(BrowseNode), typeof(NodeContentLoader)],
            [typeof(BrowseNode), typeof(NodeDecompiler)],
            [typeof(NodeSubject), typeof(BrowseNode)],
            [typeof(NodeSubject), typeof(BrowseSession)],
            [typeof(LoadedAssembly), typeof(BrowseNode)],
            [typeof(LoadedAssembly), typeof(BrowseSession)],
            [typeof(LoadedModule), typeof(BrowseNode)],
            [typeof(LoadedModule), typeof(BrowseSession)],
            [typeof(AssemblyList), typeof(BrowseNode)],
            [typeof(AssemblyList), typeof(BrowseSession)],
            [typeof(LoadedAssembly), typeof(AssemblyExport)],
            [typeof(LoadedModule), typeof(AssemblyExport)],
            [typeof(BrowseSessions), typeof(NodeContentLoader)],
            [typeof(BrowseSessions), typeof(NodeDecompiler)],
            [typeof(BrowseNodes), typeof(BrowseSession)],
            [typeof(BrowseNodes), typeof(BrowseSessions)],
            [typeof(BrowseNodes), typeof(NodeContentLoader)],
            [typeof(BrowseNodes), typeof(NodeDecompiler)],
            [typeof(LoadedAssemblies), typeof(BrowseNode)],
            [typeof(LoadedAssemblies), typeof(BrowseNodes)],
            [typeof(LoadedAssemblies), typeof(BrowseSession)],
            [typeof(LoadedAssemblies), typeof(AssemblyExport)],
            [typeof(AssemblyListing), typeof(BrowseNode)],
            [typeof(AssemblyListing), typeof(BrowseNodes)],
            [typeof(AssemblyListing), typeof(BrowseSession)],
        ];
    }

    [Theory]
    [MemberData(nameof(ForbiddenEdges))]
    public void A_type_lower_in_the_graph_never_names_one_above_it(Type from, Type to)
    {
        Assert.DoesNotContain(NameOf(to), Graph.Value.Uses(NameOf(from)));
    }

    [Fact]
    public void The_graph_sees_the_edges_that_are_there()
    {
        Assert.Contains(NameOf(typeof(BrowseSession)), Graph.Value.Uses(NameOf(typeof(NodeContentLoader))));
        Assert.Contains(NameOf(typeof(NodeDecompiler)), Graph.Value.Uses(NameOf(typeof(NodeContentLoader))));
        Assert.Contains(NameOf(typeof(NodeSubject)), Graph.Value.Uses(NameOf(typeof(BrowseNode))));
        Assert.Contains(NameOf(typeof(LoadedModule)), Graph.Value.Uses(NameOf(typeof(NodeDecompiler))));
    }

    private static string NameOf(Type type)
    {
        return type.FullName ?? throw new ArgumentException($"{type} has no full name.", nameof(type));
    }
}
