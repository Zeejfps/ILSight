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

        var root = await assembly.ReadRootAsync();
        Assert.Equal(assembly.FilePath.Text, ILSpyTree.NameOf(root));
        var fixtures = await assembly.FixturesAsync();
        Assert.Equal(Trees.FixturesNamespace, ILSpyTree.NameOf(fixtures));
        Assert.Equal(Trees.FixturesNamespace + ".Calculator`1", ILSpyTree.NameOf(calculator));
        var mode = await calculator.GetChildAsync("Mode");
        Assert.Equal(Trees.FixturesNamespace + ".Calculator`1+Mode", ILSpyTree.NameOf(mode));
        var items = await calculator.GetChildAsync("_items : List<T>");
        Assert.Equal("Field _items", ILSpyTree.NameOf(items));
        var changed = await calculator.GetChildAsync("Changed : EventHandler?");
        Assert.Equal("Event Changed", ILSpyTree.NameOf(changed));
        var add = await calculator.GetChildAsync("Add(T) : void");
        Assert.StartsWith("Method Add(", ILSpyTree.NameOf(add));
        var count = await calculator.GetChildAsync("Count : int");
        Assert.StartsWith("Property Count", ILSpyTree.NameOf(count));
    }

    [Fact]
    public async Task Every_type_and_member_is_found_again_by_its_path()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var calculator = await assembly.CalculatorAsync();
        var children = await calculator.ListChildrenAsync();
        foreach (var node in children.Prepend(calculator))
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
    public async Task A_bookmark_whose_member_the_library_cannot_read_says_its_module_unreadable_with_why()
    {
        using var temp = new TempDir();
        var grow = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Circle", "Grow");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, Trees.TestAssemblyPath, grow);
        var path = AssemblyPath.Of(unreadablePath);
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var token = (uint)MetadataTokens.GetToken(grow);

        var located = await ILSpyTree.LocateAsync(list, TestSession.Shared, path, null, token, Trees.FixturesNamespace + ".Circle.Grow", CancellationToken.None);

        var unreadable = Assert.IsType<Lookup<BrowseNode>.Unreadable>(located.GetValue());
        Assert.Equal(path, unreadable.Module.Module.FilePath);
        Assert.IsType<BadImageFormatException>(unreadable.Module.Why);
    }

    [Fact]
    public async Task A_bookmark_in_a_file_that_cannot_be_read_is_its_failure_not_a_file_gone()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var path = temp.Write("notes.dll", "not an assembly");
        var unreadable = AssemblyPath.Of(path);
        var gone = AssemblyPath.Of(temp.PathOf("gone.dll"));

        var located = await ILSpyTree.LocateAsync(list, TestSession.Shared, unreadable, null, 0x06000001, "N.T.M", CancellationToken.None);

        Assert.IsType<AssemblyLoadException>(Assert.IsType<Outcome<Lookup<BrowseNode>>.Failed>(located).Error);
        Assert.IsType<Lookup<BrowseNode>.NotFound>((await ILSpyTree.LocateAsync(list, TestSession.Shared, gone, null, 0x06000001, "N.T.M", CancellationToken.None)).GetValue());
    }

    [Fact]
    public async Task A_token_that_names_an_entity_of_another_name_finds_nothing()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var definedToken = calculator.Token;
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
