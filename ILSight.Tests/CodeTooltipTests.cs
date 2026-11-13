using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class CodeTooltipTests
{
    private static MethodDefinitionHandle GetFirstParameterOwner(string path)
    {
        using var pe = new PEReader(File.OpenRead(path));
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.MethodDefinitions)
        {
            foreach (var parameter in metadata.GetMethodDefinition(handle).GetParameters())
            {
                var row = MetadataTokens.GetRowNumber(parameter);
                var sequence = metadata.GetParameter(parameter).SequenceNumber;
                if (row == 1 && sequence > 0)
                {
                    return handle;
                }
            }
        }

        throw new InvalidOperationException($"No method of '{path}' has the first row of its Param table as a named parameter.");
    }

    [Fact]
    public async Task A_reference_whose_declaration_the_library_cannot_read_is_its_failure_saying_why()
    {
        using var temp = new TempDir();
        var owner = GetFirstParameterOwner(Trees.TestAssemblyPath);
        var path = BrokenAssemblies.WriteUnreadableParameterName(temp, Trees.TestAssemblyPath);
        var method = CodeReferences.CreateEntity(EntityKind.Method, "the first parameter's method", AssemblyPath.Of(path), owner);

        var described = await AssemblyListing.DescribeReferenceAsync(new AssemblyList { Runtime = TestRuntime.Installed }, method, CancellationToken.None);

        Assert.IsType<BadImageFormatException>(Assert.IsType<Outcome<Lookup<CodeTooltip>>.Failed>(described).Error);
    }
    private static async Task<CodeTooltip> TooltipOfAsync(AssemblyList list, BrowseNode node)
    {
        var tooltip = (await AssemblyListing.DescribeReferenceAsync(list, node.Definition, CancellationToken.None)).GetValue().FoundOrNull;
        Assert.NotNull(tooltip);
        return tooltip;
    }

    private static DocSection Section(CodeTooltip tooltip, DocSectionKind kind)
    {
        return tooltip.Sections.Single(s => s.Kind == kind);
    }

    private static string TextOf(DocBlock block)
    {
        return TextOf(Assert.IsType<DocBlock.Paragraph>(block).Runs);
    }

    private static string TextOf(IReadOnlyList<DocRun> runs)
    {
        return string.Concat(runs.Select(r => r.Text));
    }

    private static DocRun RunOf(DocBlock block, string text)
    {
        return Assert.IsType<DocBlock.Paragraph>(block).Runs.Single(r => r.Text == text);
    }

    private static List<string> ParameterTexts(CodeSignature signature)
    {
        var text = signature.Line.Text;
        var starts = signature.ParameterStarts;
        var parameters = new List<string>();
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1] : text.Length;
            parameters.Add(text.Substring(starts[i], end - starts[i]));
        }
        return parameters;
    }

    [Fact]
    public async Task The_signature_is_the_declaration_highlighted_as_code_with_its_names_linked()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var add = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void");

        var signature = (await TooltipOfAsync(list, add)).Signature.Line;

        Assert.Equal("public void Calculator<T>.Add(T item)", signature.Text);
        Assert.Equal(SpanKind.Keyword, signature.SpanAt("public").Kind);
        Assert.Equal(SpanKind.Parameter, signature.SpanAt("item").Kind);
        Assert.Equal(add.Definition, signature.SpanAt("Add").Reference);
    }

    [Fact]
    public async Task The_signature_names_types_by_their_short_names_and_says_what_the_entity_is_in()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var mode = await TooltipOfAsync(list, await assembly.CalculatorAsync().GetChildAsync("Mode"));
        var calculator = await TooltipOfAsync(list, await assembly.CalculatorAsync());
        var point = await TooltipOfAsync(list, await assembly.FixturesAsync().GetChildAsync("Point"));

        Assert.Equal("public enum Calculator<T>.Mode", mode.Signature.Line.Text);
        Assert.Equal(new CodeContainer
        {
            Keyword = "class",
            Name = "ILSight.Tests.Fixtures.Calculator<T>",
            IsNamespace = false,
        }, mode.Container);
        Assert.Equal(new CodeContainer
        {
            Keyword = "namespace",
            Name = "ILSight.Tests.Fixtures",
            IsNamespace = true,
        }, calculator.Container);
        Assert.Equal("namespace", point.Container?.Keyword);
    }

    [Fact]
    public async Task Each_type_parameter_constraint_is_a_where_clause_of_its_own()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var pick = await assembly.FixturesAsync().GetChildAsync("Circle", "Pick<TKey,TValue>(TKey, TValue) : TKey");

        var signature = (await TooltipOfAsync(list, pick)).Signature;
        var constraints = signature.Constraints.Select(c => c.Text.Trim());

        Assert.Equal("public TKey Circle.Pick<TKey, TValue>(TKey key, TValue value)", signature.Line.Text);
        Assert.Equal(["where TKey : class, IComparable<TKey>, new()", "where TValue : struct"], constraints);
        Assert.Empty((await TooltipOfAsync(list, await assembly.CalculatorAsync())).Signature.Constraints);
    }

    [Fact]
    public async Task A_constrained_type_parameter_is_colored_as_a_type_in_the_tooltip_and_the_code()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var pick = await assembly.FixturesAsync().GetChildAsync("Circle", "Pick<TKey,TValue>(TKey, TValue) : TKey");

        var constraint = (await TooltipOfAsync(list, pick)).Signature.Constraints[1];
        var code = await pick.DecompileAsync(DecompilationLanguage.CSharp);
        var clause = code.Lines[code.LineContaining("where TValue : struct")];

        Assert.Equal(SpanKind.Type, constraint.SpanAt("TValue").Kind);
        Assert.Equal(SpanKind.Type, clause.SpanAt("TValue").Kind);
    }

    [Fact]
    public async Task A_member_of_a_record_or_struct_is_in_one_by_its_keyword()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var x = await TooltipOfAsync(list, await assembly.FixturesAsync().GetChildAsync("Point", "X : int"));
        var width = await TooltipOfAsync(list, await assembly.FixturesAsync().GetChildAsync("Size", "Width : int"));

        Assert.Equal("record", x.Container?.Keyword);
        Assert.Equal("struct", width.Container?.Keyword);
    }

    [Fact]
    public async Task The_signature_marks_where_each_of_its_parameters_starts()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var tryGet = await assembly.CalculatorAsync().GetChildAsync("TryGet(int, out T?) : bool");

        var signature = (await TooltipOfAsync(list, tryGet)).Signature;
        var text = signature.Line.Text;

        Assert.Equal("public bool Calculator<T>.TryGet(int index, out T? item)", text);
        Assert.Equal(["int index, ", "out T? item)"], ParameterTexts(signature));
        Assert.Empty((await TooltipOfAsync(list, await assembly.CalculatorAsync())).Signature.ParameterStarts);
    }

    [Fact]
    public async Task The_summary_and_each_parameter_come_from_the_xml_documentation_in_order()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();

        var tooltip = await TooltipOfAsync(list, await calculator.GetChildAsync("Add(T) : void"));
        var summary = Assert.Single(Section(tooltip, DocSectionKind.Summary).Blocks);
        var item = Section(tooltip, DocSectionKind.Parameter);
        var changed = RunOf(summary, "Calculator<T>.Changed");

        Assert.Equal([DocSectionKind.Summary, DocSectionKind.Parameter], tooltip.Sections.Select(s => s.Kind));
        Assert.Equal("Adds an item and raises Calculator<T>.Changed.", TextOf(summary));
        Assert.Equal(DocStyle.Reference, changed.Style);
        Assert.Same(await calculator.GetChildAsync("Changed : EventHandler?"), await list.FindNodeAsync(Assert.IsAssignableFrom<CodeReference.Navigable>(changed.Reference)));
        Assert.Equal("item", item.Label?.Text);
        Assert.Equal("The item to add.", TextOf(Assert.Single(item.Blocks)));
    }

    [Fact]
    public async Task Remarks_keep_their_paragraphs_list_items_and_code_blocks()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var grow = await assembly.FixturesAsync().GetChildAsync("Circle", "Grow(double, int) : double");

        var remarks = Section(await TooltipOfAsync(list, grow), DocSectionKind.Remarks).Blocks;
        var bullets = Assert.IsType<DocBlock.List>(remarks[1]);
        var code = Assert.IsType<DocBlock.Code>(remarks[2]);

        Assert.Equal(3, remarks.Count);
        Assert.Equal("Each step multiplies once:", TextOf(remarks[0]));
        Assert.Equal(DocListKind.Bullet, bullets.Kind);
        Assert.Equal(["Zero", ""], bullets.Items.Select(i => TextOf(i.Term)));
        Assert.Equal(["keeps the radius.", "More grows it."], bullets.Items.Select(i => TextOf(i.Description)));
        Assert.Equal("var grown = circle.Grow(2, 3);\nConsole.WriteLine(grown);", code.Text);
    }

    [Fact]
    public async Task A_table_list_keeps_its_header_and_a_numbered_list_its_kind()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var spread = await assembly.FixturesAsync().GetChildAsync("Circle", "Spread() : double");

        var remarks = Section(await TooltipOfAsync(list, spread), DocSectionKind.Remarks).Blocks;
        var table = Assert.IsType<DocBlock.List>(remarks[0]);
        var numbered = Assert.IsType<DocBlock.List>(remarks[1]);

        Assert.Equal(DocListKind.Table, table.Kind);
        Assert.NotNull(table.Header);
        Assert.Equal("Bound", TextOf(table.Header.Term));
        Assert.Equal("Value", TextOf(table.Header.Description));
        Assert.Equal(["Low", "High"], table.Items.Select(i => TextOf(i.Term)));
        Assert.Equal(["The radius.", "Twice the radius."], table.Items.Select(i => TextOf(i.Description)));
        Assert.Equal(DocListKind.Number, numbered.Kind);
        Assert.Equal(["First.", "Second."], numbered.Items.Select(i => TextOf(i.Description)));
    }

    [Fact]
    public async Task Inline_code_keywords_and_links_keep_their_styles()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var grow = await assembly.FixturesAsync().GetChildAsync("Circle", "Grow(double, int) : double");

        var tooltip = await TooltipOfAsync(list, grow);
        var summary = Section(tooltip, DocSectionKind.Summary).Blocks[0];
        var returns = Section(tooltip, DocSectionKind.Returns).Blocks[0];

        Assert.Equal("Multiplies the radius by factor, times over.", TextOf(summary));
        Assert.Equal(DocStyle.Code, RunOf(summary, "factor").Style);
        Assert.Equal(DocStyle.Code, RunOf(summary, "times").Style);
        Assert.Equal("The grown radius, true to the example.", TextOf(returns));
        Assert.Equal(DocStyle.Code, RunOf(returns, "true").Style);
        Assert.Equal(DocStyle.Plain, RunOf(returns, "the example").Style);
    }

    [Fact]
    public async Task An_exception_is_labelled_by_its_type_and_says_when_it_is_thrown()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var grow = await assembly.FixturesAsync().GetChildAsync("Circle", "Grow(double, int) : double");

        var exception = Section(await TooltipOfAsync(list, grow), DocSectionKind.Exception);

        var label = Assert.NotNull(exception.Label);
        Assert.Equal("ArgumentOutOfRangeException", label.Text);
        Assert.NotNull(label.Reference);
        Assert.Equal("times is negative.", TextOf(Assert.Single(exception.Blocks)));
    }

    [Fact]
    public async Task Inheritdoc_takes_the_documentation_of_the_member_it_implements()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var area = await assembly.FixturesAsync().GetChildAsync("Circle", "Area : double");

        var summary = Section(await TooltipOfAsync(list, area), DocSectionKind.Summary);

        Assert.Equal("The space the shape covers.", TextOf(Assert.Single(summary.Blocks)));
    }

    [Fact]
    public async Task An_undocumented_type_has_only_its_signature()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var tooltip = await TooltipOfAsync(list, await assembly.CalculatorAsync());

        Assert.Equal("public class Calculator<T>", tooltip.Signature.Line.Text);
        Assert.Empty(tooltip.Sections);
    }

    [Fact]
    public async Task A_reference_into_another_assembly_is_described_without_listing_it()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void").DecompileAsync(DecompilationLanguage.CSharp);
        var call = document.Lines[document.LineContaining("_items.Add(item)")];

        var tooltip = (await AssemblyListing.DescribeReferenceAsync(list, Assert.IsType<CodeReference.Entity>(call.SpanAt("Add").Reference), CancellationToken.None))
            .GetValue().FoundOrNull;
        Assert.NotNull(tooltip);

        Assert.StartsWith("public void List<T>.Add(T item)", tooltip.Signature.Line.Text);
        Assert.Equal(new CodeContainer
        {
            Keyword = "class",
            Name = "System.Collections.Generic.List<T>",
            IsNamespace = false,
        }, tooltip.Container);
        Assert.NotEmpty(Section(tooltip, DocSectionKind.Summary).Blocks);
        Assert.Equal([assembly], list.Assemblies);
    }

    [Fact]
    public async Task Locals_and_assemblies_have_no_tooltip()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.FixturesAsync().GetChildAsync("Circle", "Grow(double, int) : double").DecompileAsync(DecompilationLanguage.CSharp);
        var local = document.Lines[document.LineContaining("double grown = Radius;")].SpanAt("grown").Reference;
        var references = await assembly.ReadRootAsync().GetChildAsync("References").DecompileAsync(DecompilationLanguage.CSharp);
        var runtime = references.SpansOf().First(s => s.Text.StartsWith("System.Runtime, Version=")).Span.Reference;

        Assert.IsType<CodeReference.Local>(local);
        Assert.IsType<CodeReference.Assembly>(runtime);
    }
}
