using ILSight.Core;

namespace ILSight.Tests;

public sealed class AnchorTests
{
    [Fact]
    public void A_statement_is_found_again_in_the_methods_own_document_and_in_IL()
    {
        var type = Trees.CalculatorDocument(DecompilationLanguage.CSharp);
        var line = type.LineContaining("Changed?.Invoke(this, EventArgs.Empty);");

        var anchor = Documents.FindAnchorAt(type, line)!.Value;
        var method = Trees.TestAssembly.Calculator().Child("Add(T) : void").Decompile(DecompilationLanguage.CSharp);
        var il = Trees.CalculatorDocument(DecompilationLanguage.IL);
        var ilLine = Documents.FindLine(il, anchor);

        Assert.Equal(CodeAnchorKind.Statement, anchor.Kind);
        Assert.Equal(line, Documents.FindLine(type, anchor));
        Assert.Contains("Changed?.Invoke", method.Lines[Documents.FindLine(method, anchor)].Text);
        Assert.StartsWith($"IL_{anchor.ILOffset:x4}", il.Lines[ilLine].Text.TrimStart());
        Assert.Equal(anchor, Documents.FindAnchorAt(il, ilLine));
    }

    [Fact]
    public void A_definition_is_found_again_in_either_language()
    {
        var type = Trees.CalculatorDocument(DecompilationLanguage.CSharp);
        var line = type.LineContaining("public void Add(T item)");

        var anchor = Documents.FindAnchorAt(type, line)!.Value;
        var il = Trees.CalculatorDocument(DecompilationLanguage.IL);

        Assert.Equal(CodeAnchorKind.Definition, anchor.Kind);
        Assert.Equal(line, Documents.FindLine(type, anchor));
        Assert.Equal(Documents.FindDefinitionLine(il, Trees.TestAssembly.Calculator().Child("Add(T) : void")), Documents.FindLine(il, anchor));
    }

    [CaseInsensitivePathsTheory]
    [InlineData("Changed?.Invoke(this, EventArgs.Empty);")]
    [InlineData("public void Add(T item)")]
    public void An_anchor_finds_its_line_whatever_the_case_of_its_path(string text)
    {
        var type = Trees.CalculatorDocument(DecompilationLanguage.CSharp);
        var line = type.LineContaining(text);
        var anchor = Documents.FindAnchorAt(type, line)!.Value;

        var upper = anchor with { FilePath = AssemblyPath.Of(anchor.FilePath.Text.ToUpperInvariant()) };

        Assert.Equal(anchor, upper);
        Assert.Equal(anchor.GetHashCode(), upper.GetHashCode());
        Assert.Equal(line, Documents.FindLine(type, upper));
    }

    [CaseInsensitivePathsFact]
    public void A_type_anchor_is_the_same_whatever_the_case_of_its_path()
    {
        var anchor = new TypeAnchor(AssemblyPath.Of(Path.GetFullPath("/Lib/A.dll")), "N.Outer+Inner`1");

        Assert.Equal(anchor, anchor with { AssemblyPath = AssemblyPath.Of(Path.GetFullPath("/lib/a.DLL")) });
        Assert.Equal(anchor.GetHashCode(), (anchor with { AssemblyPath = AssemblyPath.Of(Path.GetFullPath("/lib/a.DLL")) }).GetHashCode());
    }

    [Fact]
    public void A_type_anchor_differs_from_one_whose_name_differs_only_in_case()
    {
        var anchor = new TypeAnchor(AssemblyPath.Of(Path.GetFullPath("/Lib/A.dll")), "N.Outer+Inner`1");

        Assert.NotEqual(anchor, anchor with { FullName = "n.outer+inner`1" });
    }

    [Fact]
    public void Anchors_looked_up_from_many_threads_at_once_all_find_their_lines()
    {
        var first = Trees.CalculatorDocument(DecompilationLanguage.CSharp);
        var lines = new[] { first.LineContaining("Changed?.Invoke(this, EventArgs.Empty);"), first.LineContaining("public void Add(T item)") };
        var anchors = lines.Select(l => Documents.FindAnchorAt(first, l)!.Value).ToArray();

        for (var round = 0; round < 20; round++)
        {
            var fresh = Trees.TestAssembly.Calculator().Decompile(DecompilationLanguage.CSharp);
            var found = new int[64];
            Parallel.For(0, found.Length, i => found[i] = Documents.FindLine(fresh, anchors[i % anchors.Length]));

            Assert.All(Enumerable.Range(0, found.Length), i => Assert.Equal(lines[i % lines.Length], found[i]));
        }
    }

    [Fact]
    public void A_line_with_no_statement_or_definition_has_no_anchor()
    {
        var type = Trees.CalculatorDocument(DecompilationLanguage.CSharp);
        var brace = type.LineContaining("public void Add(T item)") + 1;

        Assert.Equal("{", type.Lines[brace].Text.Trim());
        Assert.Null(Documents.FindAnchorAt(type, brace));
    }

    [Fact]
    public void A_path_rebuilt_from_its_segments_names_the_same_node()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var path = assembly.Calculator().Child("Add(T) : void").Path;

        var rebuilt = NodeDefinitions.CreatePath(path.FilePath, path.Steps.ToList());

        Assert.Equal(path, rebuilt);
        Assert.Same(assembly.Calculator().Child("Add(T) : void"), list.FindNode(rebuilt));
    }
}
