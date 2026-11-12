using System.Collections.Immutable;
using ZGF.Gui;
using ILSight.Core;
using ILSight.CodeTooltipArea;

namespace ILSight.App.Tests;

public sealed class TooltipLayoutsTests
{
    private static readonly TextStyle[] CodeStyles = Enum.GetValues<SpanKind>().Select(static _ => new TextStyle()).ToArray();
    private static readonly TextStyle[] ReferenceStyles = Enum.GetValues<SpanKind>().Select(static _ => new TextStyle()).ToArray();

    private static readonly TooltipStyles Styles = new()
    {
        Plain = new TextStyle(),
        Bold = new TextStyle(),
        Italic = new TextStyle(),
        Heading = new TextStyle(),
        Container = new TextStyle(),
        CodeBlock = new TextStyle(),
        Code = static kind => CodeStyles[(int)kind],
        Reference = static kind => ReferenceStyles[(int)kind],
    };

    private static readonly Strings English = Strings.For(Locale.En);

    /// <summary>A line of code made of <paramref name="parts"/>, each a span of its kind.</summary>
    private static DocumentLine Line(params (string Text, SpanKind Kind)[] parts)
    {
        var spans = ImmutableArray.CreateBuilder<DocumentSpan>();
        var start = 0;
        foreach (var (text, kind) in parts)
        {
            spans.Add(new DocumentSpan(start, text.Length, kind, null, false));
            start += text.Length;
        }

        return new DocumentLine
        {
            Text = string.Concat(parts.Select(static p => p.Text)),
            DocumentSpans = spans.ToImmutable(),
            FirstSpan = 0,
            SpanCount = parts.Length,
        };
    }

    /// <summary><c>string Compose(string greeting, string name)</c>, its parameters starting at
    /// each <c>string</c> inside the brackets.</summary>
    private static CodeSignature Compose()
    {
        var line = Line(
            ("string", SpanKind.Keyword), (" ", SpanKind.Plain), ("Compose", SpanKind.Method), ("(", SpanKind.Plain),
            ("string", SpanKind.Keyword), (" ", SpanKind.Plain), ("greeting", SpanKind.Parameter), (", ", SpanKind.Plain),
            ("string", SpanKind.Keyword), (" ", SpanKind.Plain), ("name", SpanKind.Parameter), (")", SpanKind.Plain));
        return new CodeSignature(line, [15, 32], []);
    }

    private static CodeTooltip Tooltip(CodeSignature signature, CodeContainer? container = null, params DocSection[] sections)
    {
        return new CodeTooltip(signature, container, sections);
    }

    private static CodeSignature Plain(string text)
    {
        return new CodeSignature(Line((text, SpanKind.Plain)), [], []);
    }

    private static string TextOf(IReadOnlyList<RichTextRun> runs)
    {
        return string.Concat(runs.Select(static r => r.Text));
    }

    private static DocBlock.Paragraph Paragraph(string text)
    {
        return new DocBlock.Paragraph([new DocRun(text, DocStyle.Plain)]);
    }

    [Fact]
    public void Build_DeclarationWithParameters_CutsItWhereEachParameterStarts()
    {
        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Compose()));

        Assert.Equal("string Compose(string greeting, string name)", TextOf(layout.Declaration));
        Assert.Equal("string Compose(", TextOf(layout.DeclarationHead));
        Assert.Equal(["string greeting,", "string name)"], layout.Parameters.Select(TextOf));
        Assert.Same(CodeStyles[(int)SpanKind.Keyword], layout.Declaration[0].Style);
        Assert.Same(CodeStyles[(int)SpanKind.Method], layout.Declaration[2].Style);
    }

    [Fact]
    public void Build_DeclarationWithoutParameters_HasNothingToCut()
    {
        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Plain("int Count")));

        Assert.Equal("int Count", TextOf(layout.Declaration));
        Assert.Empty(layout.DeclarationHead);
        Assert.Empty(layout.Parameters);
    }

    [Fact]
    public void Build_Constraints_DropTheirIndentation()
    {
        var signature = new CodeSignature(Line(("class Box<T>", SpanKind.Plain)), [], [Line(("    ", SpanKind.Plain), ("where T : class", SpanKind.Plain))]);

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(signature));

        Assert.Equal(["where T : class"], layout.Constraints.Select(TextOf));
    }

    [Fact]
    public void Build_InAType_WordsTheContainerWithItsKeywordAndNameAsCode()
    {
        var container = new CodeContainer("class", "Fixtures.Farewell", IsNamespace: false);

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Plain("Say()"), container));

        Assert.Equal("in class Fixtures.Farewell", TextOf(layout.Container));
        Assert.Equal(
            [Styles.Container, CodeStyles[(int)SpanKind.Keyword], Styles.Container, CodeStyles[(int)SpanKind.Type]],
            layout.Container.Select(static r => r.Style));
    }

    [Fact]
    public void Build_InANamespace_ColorsTheNameAsANamespace()
    {
        var container = new CodeContainer("namespace", "Fixtures", IsNamespace: true);

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Plain("Farewell"), container));

        Assert.Same(CodeStyles[(int)SpanKind.Namespace], layout.Container[^1].Style);
    }

    [Fact]
    public void Build_InTheGlobalNamespace_HasNoContainer()
    {
        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Plain("Farewell")));

        Assert.Empty(layout.Container);
        Assert.Empty(layout.Groups);
    }

    [Fact]
    public void Build_SectionsOfOneKindInARow_ShareAGroupUnderTheKindsHeading()
    {
        var sections = new[]
        {
            new DocSection(DocSectionKind.Summary, null, [Paragraph("Composes a greeting.")]),
            new DocSection(DocSectionKind.Parameter, new DocRun("greeting", DocStyle.Plain), [Paragraph("What to say.")]),
            new DocSection(DocSectionKind.Parameter, new DocRun("name", DocStyle.Plain), [Paragraph("Who to.")]),
            new DocSection(DocSectionKind.Exception, new DocRun("ArgumentException", DocStyle.Plain), [Paragraph("When empty.")]),
        };

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Compose(), null, sections));

        Assert.Equal([EntryShape.Blocks, EntryShape.Column, EntryShape.Stacked], layout.Groups.Select(static g => g.Shape));
        Assert.Null(layout.Groups[0].Heading);
        var parameters = layout.Groups[1];
        Assert.Equal(English.CodeTooltipParameters, TextOf(parameters.Heading ?? []));
        Assert.Equal(["greeting", "name"], parameters.Entries.Select(static e => TextOf(e.Label ?? [])));
        Assert.Same(CodeStyles[(int)SpanKind.Parameter], parameters.Entries[0].Label?[0].Style);
        Assert.Same(Styles.Plain, layout.Groups[2].Entries[0].Label?[0].Style);
    }

    [Fact]
    public void Build_NumberedList_LeadsEachItemWithItsNumberAndBoldsItsTerm()
    {
        var items = new[]
        {
            new DocListItem([new DocRun("First", DocStyle.Plain)], [new DocRun("comes first.", DocStyle.Plain)]),
            new DocListItem([], [new DocRun("Then this.", DocStyle.Plain)]),
        };
        var list = new DocBlock.List(DocListKind.Number, null, items);
        var summary = new DocSection(DocSectionKind.Summary, null, [list]);

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Plain("Steps"), null, summary));

        var block = Assert.IsType<ListBlock>(Assert.Single(Assert.Single(layout.Groups[0].Entries).Blocks));
        Assert.False(block.IsTable);
        Assert.Equal(["1.", "2."], block.Rows.Select(static r => TextOf(r.Lead)));
        Assert.Equal(["First – comes first.", "Then this."], block.Rows.Select(static r => TextOf(r.Content)));
        Assert.Same(Styles.Bold, block.Rows[0].Content[0].Style);
    }

    [Fact]
    public void Build_TableWithAHeader_SetsTheHeaderRowInTheHeadingStyle()
    {
        var header = new DocListItem([new DocRun("Value", DocStyle.Plain)], [new DocRun("Meaning", DocStyle.Plain)]);
        var item = new DocListItem([new DocRun("0", DocStyle.Plain)], [new DocRun("None.", DocStyle.Plain)]);
        var table = new DocBlock.List(DocListKind.Table, header, [item]);
        var remarks = new DocSection(DocSectionKind.Remarks, null, [table, new DocBlock.Code("var x = 0;")]);

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Plain("Flags"), null, remarks));

        var blocks = Assert.Single(layout.Groups[0].Entries).Blocks;
        var rows = Assert.IsType<ListBlock>(blocks[0]).Rows;
        Assert.Equal(["Value", "0"], rows.Select(static r => TextOf(r.Lead)));
        Assert.Same(Styles.Heading, rows[0].Lead[0].Style);
        Assert.Same(Styles.Heading, rows[0].Content[0].Style);
        Assert.Equal("var x = 0;", TextOf(Assert.IsType<CodeBlock>(blocks[1]).Runs));
    }
}
