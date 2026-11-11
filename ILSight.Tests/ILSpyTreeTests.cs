using ILSight.Core;

namespace ILSight.Tests;

public sealed class ILSpyTreeTests
{
    private static NodePath? PathOf(BrowseNode? node)
    {
        return node is null ? null : BrowseNodes.GetPath(node);
    }

    [Fact]
    public void Nodes_are_named_as_ILSpys_tree_names_them()
    {
        var assembly = Trees.TestAssembly;
        var calculator = assembly.Calculator();

        Assert.Equal(assembly.FilePath.Text, ILSpyTree.NameOf(assembly.Root));
        Assert.Equal(Trees.FixturesNamespace, ILSpyTree.NameOf(assembly.Fixtures()));
        Assert.Equal(Trees.FixturesNamespace + ".Calculator`1", ILSpyTree.NameOf(calculator));
        Assert.Equal(Trees.FixturesNamespace + ".Calculator`1+Mode", ILSpyTree.NameOf(calculator.Child("Mode")));
        Assert.Equal("Field _items", ILSpyTree.NameOf(calculator.Child("_items : List<T>")));
        Assert.Equal("Event Changed", ILSpyTree.NameOf(calculator.Child("Changed : EventHandler?")));
        Assert.StartsWith("Method Add(", ILSpyTree.NameOf(calculator.Child("Add(T) : void")));
        Assert.StartsWith("Property Count", ILSpyTree.NameOf(calculator.Child("Count : int")));
    }

    [Fact]
    public void Every_type_and_member_is_found_again_by_its_path()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        foreach (var node in assembly.Calculator().Children.Prepend(assembly.Calculator()))
        {
            if (ILSpyTree.PathOf(node) is not { } path)
            {
                continue;
            }

            Assert.Equal(BrowseNodes.GetPath(node), PathOf(ILSpyTree.Locate(list, TestSession.Shared, assembly.FilePath, path, token: 0, memberName: "")));
        }
    }

    [Fact]
    public void A_bookmark_without_a_tree_path_is_found_by_its_token()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = assembly.Calculator();
        var info = ILSpyTree.Describe(calculator, null);

        Assert.Equal(BrowseNodes.GetPath(calculator), PathOf(ILSpyTree.Locate(list, TestSession.Shared, assembly.FilePath, null, info.Token, info.MemberName)));
        Assert.Equal(BrowseNodes.GetPath(calculator), PathOf(ILSpyTree.Locate(list, TestSession.Shared, assembly.FilePath, ["no such file"], info.Token, info.MemberName)));
        Assert.Null(ILSpyTree.Locate(list, TestSession.Shared, assembly.FilePath, null, 0x06FFFFFF, info.MemberName));
    }

    [Fact]
    public void A_token_that_names_an_entity_of_another_name_finds_nothing()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var info = ILSpyTree.Describe(assembly.Calculator(), null);

        Assert.Null(ILSpyTree.Locate(list, TestSession.Shared, assembly.FilePath, null, info.Token, Trees.FixturesNamespace + ".Renamed"));
        Assert.Null(ILSpyTree.Locate(list, TestSession.Shared, assembly.FilePath, ["no such file"], info.Token, ""));
    }

    [Fact]
    public void A_bookmark_is_described_as_ILSpy_records_it()
    {
        var assembly = Trees.TestAssembly;
        var calculator = assembly.Calculator();
        var document = Trees.CalculatorDocument(DecompilationLanguage.CSharp);
        var anchor = Documents.FindAnchorAt(document, document.LineContaining("Changed?.Invoke(this, EventArgs.Empty);"));

        var info = ILSpyTree.Describe(calculator, anchor);

        Assert.Equal(assembly.FullName, info.AssemblyFullName);
        Assert.Equal(assembly.FilePath.FileName, info.ModuleName);
        Assert.Equal((uint)anchor!.Value.Token, info.Token);
        Assert.Equal(Trees.FixturesNamespace + ".Calculator.Add", info.MemberName);
        Assert.Equal(Trees.FixturesNamespace + ".Calculator", info.LocationNodeName);
        Assert.Equal([assembly.FilePath.Text, Trees.FixturesNamespace, Trees.FixturesNamespace + ".Calculator`1"], info.TreePath);
    }
}
