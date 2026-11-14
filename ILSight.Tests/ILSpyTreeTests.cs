using System.Reflection.Metadata.Ecma335;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class ILSpyTreeTests
{
    private static NodePath? PathOf(BrowseNode? node)
    {
        return node is null ? null : node.Path;
    }

    [Fact]
    public async Task Nodes_are_named_as_ILSpys_tree_names_them()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();

        Assert.Equal(assembly.FilePath.Text, ILSpyTree.NameOf(await assembly.ReadRootAsync()));
        Assert.Equal(Trees.FixturesNamespace, ILSpyTree.NameOf(await assembly.FixturesAsync()));
        Assert.Equal(Trees.FixturesNamespace + ".Calculator`1", ILSpyTree.NameOf(calculator));
        Assert.Equal(Trees.FixturesNamespace + ".Calculator`1+Mode", ILSpyTree.NameOf(await calculator.GetChildAsync("Mode")));
        Assert.Equal("Field _items", ILSpyTree.NameOf(await calculator.GetChildAsync("_items : List<T>")));
        Assert.Equal("Event Changed", ILSpyTree.NameOf(await calculator.GetChildAsync("Changed : EventHandler?")));
        Assert.StartsWith("Method Add(", ILSpyTree.NameOf(await calculator.GetChildAsync("Add(T) : void")));
        Assert.StartsWith("Property Count", ILSpyTree.NameOf(await calculator.GetChildAsync("Count : int")));
    }

    [Fact]
    public async Task Every_type_and_member_is_found_again_by_its_path()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        foreach (var node in (await assembly.CalculatorAsync().ListChildrenAsync()).Prepend(await assembly.CalculatorAsync()))
        {
            var path = ILSpyTree.PathOf(node);
            if (path is null)
            {
                continue;
            }

            Assert.Equal(node.Path, PathOf((await ILSpyTree.LocateAsync(list, TestSession.Shared, assembly.FilePath, path, token: 0, memberName: "", CancellationToken.None)).GetValue().FoundOrNull));
        }
    }

    [Fact]
    public async Task A_bookmark_without_a_tree_path_is_found_by_its_token()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var definedToken = calculator.Token;
        Assert.NotNull(definedToken);
        var token = (uint)definedToken.Value;
        var name = calculator.EntityFullName;
        Assert.NotNull(name);

        Assert.Equal(calculator.Path, PathOf((await ILSpyTree.LocateAsync(list, TestSession.Shared, assembly.FilePath, null, token, name, CancellationToken.None)).GetValue().FoundOrNull));
        Assert.Equal(calculator.Path, PathOf((await ILSpyTree.LocateAsync(list, TestSession.Shared, assembly.FilePath, ["no such file"], token, name, CancellationToken.None)).GetValue().FoundOrNull));
        Assert.IsType<Lookup<BrowseNode>.NotFound>((await ILSpyTree.LocateAsync(list, TestSession.Shared, assembly.FilePath, null, 0x06FFFFFF, name, CancellationToken.None)).GetValue());
    }

    [Fact]
    public async Task A_bookmark_whose_member_the_library_cannot_read_is_its_failure_saying_why()
    {
        using var temp = new TempDir();
        var grow = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Circle", "Grow");
        var path = AssemblyPath.Of(BrokenAssemblies.WriteUnreadableMethodName(temp, Trees.TestAssemblyPath, grow));
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var token = (uint)MetadataTokens.GetToken(grow);

        var located = await ILSpyTree.LocateAsync(list, TestSession.Shared, path, null, token, Trees.FixturesNamespace + ".Circle.Grow", CancellationToken.None);

        Assert.IsType<BadImageFormatException>(Assert.IsType<Outcome<Lookup<BrowseNode>>.Failed>(located).Error);
    }

    [Fact]
    public async Task A_token_that_names_an_entity_of_another_name_finds_nothing()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var definedToken = (await assembly.CalculatorAsync()).Token;
        Assert.NotNull(definedToken);
        var token = (uint)definedToken.Value;

        Assert.IsType<Lookup<BrowseNode>.NotFound>((await ILSpyTree.LocateAsync(list, TestSession.Shared, assembly.FilePath, null, token, Trees.FixturesNamespace + ".Renamed", CancellationToken.None)).GetValue());
        Assert.IsType<Lookup<BrowseNode>.NotFound>((await ILSpyTree.LocateAsync(list, TestSession.Shared, assembly.FilePath, ["no such file"], token, "", CancellationToken.None)).GetValue());
    }

    [Fact]
    public async Task A_node_and_its_document_keep_the_names_ILSpy_records_a_bookmark_by()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();
        var document = await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp);
        var anchor = Documents.FindAnchorAt(document, document.LineContaining("Changed?.Invoke(this, EventArgs.Empty);"));
        Assert.NotNull(anchor);

        var method = document.EntityNames.GetValueOrDefault((anchor.Value.FilePath, anchor.Value.Token));

        Assert.Equal(Trees.FixturesNamespace + ".Calculator.Add", method);
        Assert.Equal(Trees.FixturesNamespace + ".Calculator", calculator.EntityFullName);
        Assert.Equal([assembly.FilePath.Text, Trees.FixturesNamespace, Trees.FixturesNamespace + ".Calculator`1"], ILSpyTree.PathOf(calculator));
    }
}
