using ILSight.Core;

namespace ILSight.Tests;

public sealed class NestedNamespaceTests
{
    private const string TestsNamespace = "ILSight.Tests";

    private static IReadOnlyList<BrowseNode> NestedChildren(BrowseNode node)
    {
        node.LoadChildren();
        return Assert.IsType<ChildrenState.Ready>(TestSession.Shared.PeekNestedChildren(node)).Children;
    }

    [Fact]
    public void Every_namespace_a_name_nests_in_is_listed_and_those_without_types_only_enclose_others()
    {
        var root = Trees.TestAssembly.Root;

        var namespaces = root.Children
            .Where(c => c.Subject is NodeSubject.Namespace)
            .ToList();

        Assert.Equal(["-", "ILSight", TestsNamespace, Trees.FixturesNamespace], namespaces.Select(n => n.Text));
        Assert.Equal([false, true, false, false], namespaces.Select(n => Assert.IsType<NodeSubject.Namespace>(n.Subject).Value.IsEnclosingOnly));
        Assert.Empty(root.Child("ILSight").Children);
        Assert.False(root.Child("ILSight").HasChildren);
    }

    [Fact]
    public void Each_namespace_nests_in_the_one_its_name_extends_by_a_segment()
    {
        var root = Trees.TestAssembly.Root;
        var ilsight = root.Child("ILSight");
        var tests = root.Child(TestsNamespace);
        var fixtures = root.Child(Trees.FixturesNamespace);

        Assert.Null(root.Child("-").EnclosingNamespace);
        Assert.Null(ilsight.EnclosingNamespace);
        Assert.Same(ilsight, tests.EnclosingNamespace);
        Assert.Same(tests, fixtures.EnclosingNamespace);
        Assert.Equal([tests], ilsight.InnerNamespaces);
        Assert.Equal([fixtures], tests.InnerNamespaces);
        Assert.Empty(fixtures.InnerNamespaces);
    }

    [Fact]
    public void Nested_an_assembly_lists_only_its_outermost_namespaces_and_each_namespace_its_inner_ones()
    {
        var root = Trees.TestAssembly.Root;

        var outermost = NestedChildren(root)
            .Where(c => c.Subject is NodeSubject.Namespace)
            .Select(c => c.Text);

        Assert.Equal(["-", "ILSight"], outermost);
        Assert.Equal([root.Child(TestsNamespace)], NestedChildren(root.Child("ILSight")));
        Assert.Equal(root.Children.Where(c => c.Subject is not NodeSubject.Namespace), NestedChildren(root).Where(c => c.Subject is not NodeSubject.Namespace));
    }

    [Fact]
    public void Nested_a_namespace_lists_its_inner_namespaces_among_its_own_types_by_name()
    {
        var root = Trees.TestAssembly.Root;
        var tests = root.Child(TestsNamespace);
        var fixtures = root.Child(Trees.FixturesNamespace);

        var nested = NestedChildren(tests);
        var at = nested.ToList().IndexOf(fixtures);

        Assert.Equal(tests.Children, nested.Where(c => c != fixtures));
        Assert.True(at > 0);
        Assert.True(NaturalStringComparer.Instance.Compare(nested[at - 1].Text, "Fixtures") < 0);
        Assert.True(NaturalStringComparer.Instance.Compare(nested[at + 1].Text, "Fixtures") > 0);
        Assert.Equal(fixtures.Children, NestedChildren(fixtures));
        Assert.Contains(fixtures.Child("Calculator<T>"), NestedChildren(fixtures));
    }

    [Fact]
    public void A_namespace_that_only_encloses_others_is_found_again_by_its_full_name()
    {
        var ilsight = Trees.TestAssembly.Root.Child("ILSight");
        var (fresh, _) = Trees.OpenTestAssembly();

        var found = fresh.FindNode(BrowseNodes.GetPath(ilsight));

        Assert.Equal([new NodePathSegment.Namespace("ILSight")], BrowseNodes.GetPath(ilsight).Steps.Select(static s => s.Segment));
        Assert.NotNull(found);
        Assert.True(Assert.IsType<NodeSubject.Namespace>(found!.Subject).Value.IsEnclosingOnly);
        Assert.Equal("ILSight", found.Text);
        Assert.StartsWith("namespace ILSight", Documents.GetText(ilsight.Decompile(DecompilationLanguage.CSharp)));
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
