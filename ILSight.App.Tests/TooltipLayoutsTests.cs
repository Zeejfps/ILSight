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

    private static DocumentLine Line(params (string Text, SpanKind Kind)[] parts)
    {
        var spans = ImmutableArray.CreateBuilder<DocumentSpan>();
        var start = 0;
        foreach (var (text, kind) in parts)
        {
            spans.Add(new DocumentSpan
            {
                Start = start,
                Length = text.Length,
                Kind = kind,
                Reference = null,
                IsDefinition = false,
            });
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

    private static CodeSignature Compose()
    {
        var line = Line(
            ("string", SpanKind.Keyword), (" ", SpanKind.Plain), ("Compose", SpanKind.Method), ("(", SpanKind.Plain),
            ("string", SpanKind.Keyword), (" ", SpanKind.Plain), ("greeting", SpanKind.Parameter), (", ", SpanKind.Plain),
            ("string", SpanKind.Keyword), (" ", SpanKind.Plain), ("name", SpanKind.Parameter), (")", SpanKind.Plain));
        // The parameters start at each string inside the brackets.
        return new CodeSignature { Line = line, ParameterStarts = [15, 32], Constraints = [] };
    }

    private static CodeTooltip Tooltip(CodeSignature signature, CodeContainer? container = null, params DocSection[] sections)
    {
        return new CodeTooltip { Signature = signature, Container = container, Sections = sections };
    }

    private static CodeSignature Plain(string text)
    {
        return new CodeSignature { Line = Line((text, SpanKind.Plain)), ParameterStarts = [], Constraints = [] };
    }

    private static string TextOf(IReadOnlyList<RichTextRun> runs)
    {
        return string.Concat(runs.Select(static r => r.Text));
    }

    private static DocBlock.Paragraph Paragraph(string text)
    {
        return new DocBlock.Paragraph([new DocRun { Text = text, Style = DocStyle.Plain }]);
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
        var signature = new CodeSignature
        {
            Line = Line(("class Box<T>", SpanKind.Plain)),
            ParameterStarts = [],
            Constraints = [Line(("    ", SpanKind.Plain), ("where T : class", SpanKind.Plain))],
        };

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(signature));

        Assert.Equal(["where T : class"], layout.Constraints.Select(TextOf));
    }

    [Fact]
    public void Build_InAType_WordsTheContainerWithItsKeywordAndNameAsCode()
    {
        var container = new CodeContainer { Keyword = "class", Name = "Fixtures.Farewell", IsNamespace = false };

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Plain("Say()"), container));

        Assert.Equal("in class Fixtures.Farewell", TextOf(layout.Container));
        Assert.Equal(
            [Styles.Container, CodeStyles[(int)SpanKind.Keyword], Styles.Container, CodeStyles[(int)SpanKind.Type]],
            layout.Container.Select(static r => r.Style));
    }

    [Fact]
    public void Build_InANamespace_ColorsTheNameAsANamespace()
    {
        var container = new CodeContainer { Keyword = "namespace", Name = "Fixtures", IsNamespace = true };

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
        var sections = new DocSection[]
        {
            new DocSection.Summary { Blocks = [Paragraph("Composes a greeting.")] },
            new DocSection.Parameter
            {
                Name = new DocRun
                {
                    Text = "greeting",
                    Style = DocStyle.Plain,
                },
                Blocks = [Paragraph("What to say.")],
            },
            new DocSection.Parameter
            {
                Name = new DocRun
                {
                    Text = "name",
                    Style = DocStyle.Plain,
                },
                Blocks = [Paragraph("Who to.")],
            },
            new DocSection.Exception
            {
                Name = new DocRun
                {
                    Text = "ArgumentException",
                    Style = DocStyle.Plain,
                },
                Blocks = [Paragraph("When empty.")],
            },
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
            new DocListItem
            {
                Term = [new DocRun
                {
                    Text = "First",
                    Style = DocStyle.Plain,
                }],
                Description = [new DocRun
                {
                    Text = "comes first.",
                    Style = DocStyle.Plain,
                }],
            },
            new DocListItem { Term = [], Description = [new DocRun { Text = "Then this.", Style = DocStyle.Plain }] },
        };
        var list = new DocBlock.List { Kind = DocListKind.Number, Header = null, Items = items };
        var summary = new DocSection.Summary { Blocks = [list] };

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
        var header = new DocListItem
        {
            Term = [new DocRun
            {
                Text = "Value",
                Style = DocStyle.Plain,
            }],
            Description = [new DocRun
            {
                Text = "Meaning",
                Style = DocStyle.Plain,
            }],
        };
        var item = new DocListItem
        {
            Term = [new DocRun
            {
                Text = "0",
                Style = DocStyle.Plain,
            }],
            Description = [new DocRun
            {
                Text = "None.",
                Style = DocStyle.Plain,
            }],
        };
        var table = new DocBlock.List { Kind = DocListKind.Table, Header = header, Items = [item] };
        var remarks = new DocSection.Remarks { Blocks = [table, new DocBlock.Code("var x = 0;")] };

        var layout = TooltipLayouts.Build(Styles, English, Tooltip(Plain("Flags"), null, remarks));

        var blocks = Assert.Single(layout.Groups[0].Entries).Blocks;
        var rows = Assert.IsType<ListBlock>(blocks[0]).Rows;
        Assert.Equal(["Value", "0"], rows.Select(static r => TextOf(r.Lead)));
        Assert.Same(Styles.Heading, rows[0].Lead[0].Style);
        Assert.Same(Styles.Heading, rows[0].Content[0].Style);
        Assert.Equal("var x = 0;", TextOf(Assert.IsType<CodeBlock>(blocks[1]).Runs));
    }
}
