using System.Reflection.Metadata.Ecma335;
using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

public sealed class BrowseSessionTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public async Task Children_are_built_once_and_every_caller_gets_the_same_ones()
    {
        var session = new BrowseSession();
        var root = session.EnsureRoot(new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.TestAssemblyPath));

        var waits = Enumerable.Range(0, 16).AsParallel().WithDegreeOfParallelism(8).Select(_ => BrowseSessions.GetChildrenAsync(session, root)).ToArray();
        var waited = await Task.WhenAll(waits);

        var children = Assert.IsType<ChildrenState.Ready>(session.PeekChildren(root)).Children;
        Assert.All(waited, s => Assert.Same(children, Assert.IsType<ChildrenState.Ready>(s).Children));
    }

    [Fact]
    public async Task The_caller_that_starts_a_build_awaits_it_to_its_end_even_when_cancelled()
    {
        var session = new BrowseSession();
        var root = (await BrowseSessions.EnsureRootAsync(session, new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.TestAssemblyPath)));

        var started = await BrowseSessions.GetChildrenAsync(session, root, new CancellationToken(canceled: true));
        var state = await BrowseSessions.GetChildrenAsync(session, root);

        Assert.NotEmpty(Assert.IsType<ChildrenState.Ready>(started).Children);
        Assert.Same(started, state);
    }

    [Fact]
    public async Task Children_that_fail_to_build_say_why_once_and_count_as_children()
    {
        var session = new BrowseSession();
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(BrokenAssemblies.WriteUnreadableAvaloniaContainer(_temp));
        var resources = (await BrowseSessions.ListChildrenAsync(session, (await BrowseSessions.EnsureRootAsync(session, assembly)))).Single(c => c.Subject is NodeSubject.Resources);
        var container = Assert.Single((await BrowseSessions.ListChildrenAsync(session, resources)));

        var failed = Assert.IsType<ChildrenState.Failed>((await BrowseSessions.GetChildrenAsync(session, container)));

        Assert.IsType<InvalidDataException>(failed.Error);
        Assert.Same(failed, (await BrowseSessions.GetChildrenAsync(session, container)));
        Assert.True(session.HasChildren(container));
        Assert.Empty((await BrowseSessions.ListChildrenAsync(session, container)));
    }

    [Fact]
    public async Task Each_session_builds_a_tree_of_its_own()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.TestAssemblyPath);
        var first = new BrowseSession();
        var second = new BrowseSession();

        Assert.Same(first.EnsureRoot(assembly), first.EnsureRoot(assembly));
        Assert.Same(first.EnsureRoot(assembly), (await BrowseSessions.EnsureRootAsync(first, assembly)));
        Assert.NotSame(first.EnsureRoot(assembly), second.EnsureRoot(assembly));
        Assert.IsType<ChildrenState.Unloaded>(second.PeekChildren(first.EnsureRoot(assembly)));
    }

    [Fact]
    public void A_listed_items_root_shows_what_a_node_made_from_its_subject_shows()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var module = Assert.IsType<LoadedModule>(list.Open(Trees.TestAssemblyPath));
        var failed = Assert.IsType<FailedAssembly>(list.Open(_temp.Write("broken.dll", "no assembly")));
        var session = new BrowseSession();

        AssertShowsTheSame(BrowseNodes.Create(null, new NodeSubject.Module { Value = module }), session.EnsureRoot(module));
        AssertShowsTheSame(BrowseNodes.Create(null, new NodeSubject.FailedAssembly { Value = failed }), session.EnsureRoot(failed));
    }

    /// <summary>A fresh session's metadata folder of the test assembly, its header grids and "Tables" node built, the
    /// tables not yet.</summary>
    private static async Task<(BrowseNode Header, BrowseNode Tables)> MetadataHeaderAndTablesAsync(BrowseSession session)
    {
        var root = session.EnsureRoot(Trees.TestAssembly);
        var metadata = (await BrowseSessions.ListChildrenAsync(session, root)).First(static c => c.Subject is NodeSubject.Metadata);
        var parts = (await BrowseSessions.ListChildrenAsync(session, metadata));
        return (parts.First(static c => c.Text == "DOS Header"), parts.Single(static c => c.Subject is NodeSubject.MetadataTables));
    }

    [Fact]
    public async Task A_link_of_a_header_grid_makes_the_tables_from_the_image_and_keeps_them_as_the_tree_lists_them()
    {
        var session = new BrowseSession();
        var (header, tables) = await MetadataHeaderAndTablesAsync(session);
        var before = session.PeekChildren(tables);

        var target = Assert.IsType<MetadataTarget>(session.FindMetadataRow(header, new MetadataAddress
        {
            Table = TableIndex.TypeDef,
            Row = 1,
        }));

        Assert.IsType<ChildrenState.Unloaded>(before);
        var typeDefs = (await BrowseSessions.ListChildrenAsync(session, tables)).Single(static c => c.Subject is NodeSubject.MetadataTable t && t.Value.ListedTable == TableIndex.TypeDef);
        Assert.Same(typeDefs, target.Node);
        Assert.Equal(1, target.Row);
    }

    [Fact]
    public async Task A_link_of_a_table_grid_leads_to_the_row_among_the_tables_built()
    {
        var session = new BrowseSession();
        var (_, tables) = await MetadataHeaderAndTablesAsync(session);
        var built = (await BrowseSessions.ListChildrenAsync(session, tables));
        var methods = built.Single(static c => c.Subject is NodeSubject.MetadataTable t && t.Value.ListedTable == TableIndex.MethodDef);

        var target = Assert.IsType<MetadataTarget>(session.FindMetadataRow(methods, new MetadataAddress
        {
            Table = TableIndex.TypeDef,
            Row = 0,
        }));

        Assert.Same(built.Single(static c => c.Subject is NodeSubject.MetadataTable t && t.Value.ListedTable == TableIndex.TypeDef), target.Node);
        Assert.Equal(0, target.Row);
    }

    [Fact]
    public async Task The_tables_a_link_makes_show_what_a_node_made_from_their_subject_shows()
    {
        var session = new BrowseSession();
        var (header, tables) = await MetadataHeaderAndTablesAsync(session);
        Assert.NotNull(session.FindMetadataRow(header, new MetadataAddress { Table = TableIndex.TypeDef, Row = 0 }));

        var made = Assert.IsType<ChildrenState.Ready>(session.PeekChildren(tables)).Children;

        Assert.NotEmpty(made);
        Assert.All(made, node =>
        {
            AssertShowsTheSame(BrowseNodes.Create(tables, node.Subject), node);
            Assert.Equal(BrowseNodes.Create(tables, node.Subject).Segment, node.Segment);
        });
    }

    private static void AssertShowsTheSame(BrowseNode expected, BrowseNode actual)
    {
        Assert.Equal(expected.Path, actual.Path);
        Assert.Equal(expected.Text, actual.Text);
        Assert.Same(expected.Assembly, actual.Assembly);
        Assert.Equal(expected.Subject.GetType(), actual.Subject.GetType());
        Assert.Equal(expected.FullName, actual.FullName);
        Assert.Equal(expected.Visibility, actual.Visibility);
        Assert.Equal(expected.Reference, actual.Reference);
        Assert.Equal(expected.Activation, actual.Activation);
        Assert.Equal(expected.Analysis, actual.Analysis);
        Assert.Equal(expected.CanHaveChildren, actual.CanHaveChildren);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.IsCode, actual.IsCode);
        Assert.Equal(expected.ResourceFileName, actual.ResourceFileName);
        Assert.Equal(expected.ILSpyName, actual.ILSpyName);
        Assert.Equal(expected.EntityFullName, actual.EntityFullName);
    }
}
