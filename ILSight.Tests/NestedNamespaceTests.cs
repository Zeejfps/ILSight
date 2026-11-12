using ILSight.Core;

namespace ILSight.Tests;

public sealed class NestedNamespaceTests
{
    private const string TestsNamespace = "ILSight.Tests";

    private static async Task<IReadOnlyList<BrowseNode>> NestedChildrenAsync(BrowseNode node)
    {
        await node.EnsureChildrenAsync();
        return Assert.IsType<ChildrenState.Ready>(TestSession.Shared.PeekNestedChildren(node)).Children;
    }

    [Fact]
    public async Task Every_namespace_a_name_nests_in_is_listed_and_those_without_types_only_enclose_others()
    {
        var root = await Trees.TestAssembly.ReadRootAsync();

        var namespaces = (await root.ListChildrenAsync())
            .Where(c => c.Subject is NodeSubject.Namespace)
            .ToList();

        Assert.Equal(["-", "ILSight", TestsNamespace, Trees.FixturesNamespace], namespaces.Select(n => n.Text));
        Assert.Equal([false, true, false, false], namespaces.Select(n => Assert.IsType<NodeSubject.Namespace>(n.Subject).Value.IsEnclosingOnly));
        Assert.Empty(await root.GetChildAsync("ILSight").ListChildrenAsync());
        Assert.False((await root.GetChildAsync("ILSight")).HasChildren);
    }

    [Fact]
    public async Task Each_namespace_nests_in_the_one_its_name_extends_by_a_segment()
    {
        var root = await Trees.TestAssembly.ReadRootAsync();
        var ilsight = await root.GetChildAsync("ILSight");
        var tests = await root.GetChildAsync(TestsNamespace);
        var fixtures = await root.GetChildAsync(Trees.FixturesNamespace);

        Assert.Null((await root.GetChildAsync("-")).EnclosingNamespace);
        Assert.Null(ilsight.EnclosingNamespace);
        Assert.Same(ilsight, tests.EnclosingNamespace);
        Assert.Same(tests, fixtures.EnclosingNamespace);
        Assert.Equal([tests], ilsight.InnerNamespaces);
        Assert.Equal([fixtures], tests.InnerNamespaces);
        Assert.Empty(fixtures.InnerNamespaces);
    }

    [Fact]
    public async Task Nested_an_assembly_lists_only_its_outermost_namespaces_and_each_namespace_its_inner_ones()
    {
        var root = await Trees.TestAssembly.ReadRootAsync();

        var outermost = (await NestedChildrenAsync(root))
            .Where(c => c.Subject is NodeSubject.Namespace)
            .Select(c => c.Text);

        Assert.Equal(["-", "ILSight"], outermost);
        Assert.Equal([await root.GetChildAsync(TestsNamespace)], await NestedChildrenAsync(await root.GetChildAsync("ILSight")));
        Assert.Equal((await root.ListChildrenAsync()).Where(c => c.Subject is not NodeSubject.Namespace), (await NestedChildrenAsync(root)).Where(c => c.Subject is not NodeSubject.Namespace));
    }

    [Fact]
    public async Task Nested_a_namespace_lists_its_inner_namespaces_among_its_own_types_by_name()
    {
        var root = await Trees.TestAssembly.ReadRootAsync();
        var tests = await root.GetChildAsync(TestsNamespace);
        var fixtures = await root.GetChildAsync(Trees.FixturesNamespace);

        var nested = await NestedChildrenAsync(tests);
        var at = nested.ToList().IndexOf(fixtures);

        Assert.Equal(await tests.ListChildrenAsync(), nested.Where(c => c != fixtures));
        Assert.True(at > 0);
        Assert.True(NaturalStringComparer.Instance.Compare(nested[at - 1].Text, "Fixtures") < 0);
        Assert.True(NaturalStringComparer.Instance.Compare(nested[at + 1].Text, "Fixtures") > 0);
        Assert.Equal(await fixtures.ListChildrenAsync(), await NestedChildrenAsync(fixtures));
        Assert.Contains(await fixtures.GetChildAsync("Calculator<T>"), await NestedChildrenAsync(fixtures));
    }

    [Fact]
    public async Task A_namespace_that_only_encloses_others_is_found_again_by_its_full_name()
    {
        var ilsight = await Trees.TestAssembly.ReadRootAsync().GetChildAsync("ILSight");
        var (fresh, _) = Trees.OpenTestAssembly();

        var found = await fresh.FindNodeAsync(ilsight.Path);

        Assert.Equal([new NodePathSegment.Namespace("ILSight")], ilsight.Path.Steps.Select(static s => s.Segment));
        Assert.NotNull(found);
        Assert.True(Assert.IsType<NodeSubject.Namespace>(found.Subject).Value.IsEnclosingOnly);
        Assert.Equal("ILSight", found.Text);
        Assert.StartsWith("namespace ILSight", Documents.GetText(await ilsight.DecompileAsync(DecompilationLanguage.CSharp)));
    }

    [Fact]
    public void A_namespace_name_ends_in_its_last_segment()
    {
        Assert.Equal("Generic", NamespaceNames.LastSegment("System.Collections.Generic"));
        Assert.Equal("System", NamespaceNames.LastSegment("System"));
        Assert.Equal(["System.Collections.Generic", "System.Collections", "System"], NamespaceNames.AndEnclosing("System.Collections.Generic"));
        Assert.Equal([""], NamespaceNames.AndEnclosing(""));
    }
}
