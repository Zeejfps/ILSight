using ILSight.Core;
using ILSight.CodeViewArea;

namespace ILSight.App.Tests;

public sealed class MatchingBracketsTests
{
    private static BracketPair? Find(DecompiledDocument document, int line, int column)
    {
        return new CodeViewState().FindBrackets(document, new TextPosition { Line = line, Column = column });
    }

    private static BracketPair Pair(int openLine, int openColumn, int closeLine, int closeColumn)
    {
        return new BracketPair
        {
            Open = new TextPosition
            {
                Line = openLine,
                Column = openColumn,
            },
            Close = new TextPosition
            {
                Line = closeLine,
                Column = closeColumn,
            },
        };
    }

    [Theory]
    [InlineData(1, 1, 6)]
    [InlineData(7, 1, 6)]
    [InlineData(3, 3, 5)]
    [InlineData(4, 3, 5)]
    [InlineData(6, 1, 6)]
    public void The_bracket_at_or_just_before_the_caret_is_matched(int caret, int open, int close)
    {
        var document = Documents.CreateFromText("t", "f(a[0])");

        Assert.Equal(Pair(0, open, 0, close), Find(document, 0, caret));
    }

    [Fact]
    public void Brackets_match_across_lines_past_nested_ones()
    {
        var document = Documents.CreateFromText("t", "{\n  { x; }\n}");

        Assert.Equal(Pair(0, 0, 2, 0), Find(document, 0, 0));
        Assert.Equal(Pair(0, 0, 2, 0), Find(document, 2, 1));
    }

    [Fact]
    public void Brackets_in_strings_do_not_count()
    {
        var builder = new DocumentBuilder { Language = DecompilationLanguage.CSharp };
        builder.Write("if (", SpanKind.Plain);
        builder.Write("\")\"", SpanKind.String);
        builder.Write(")", SpanKind.Plain);
        var document = builder.BuildDocument("t");

        Assert.Equal(Pair(0, 3, 0, 7), Find(document, 0, 3));
        Assert.Null(Find(document, 0, 5));
    }

    [Fact]
    public void A_bracket_without_a_match_and_text_without_brackets_find_nothing()
    {
        var document = Documents.CreateFromText("t", "f(a b");

        Assert.Null(Find(document, 0, 1));
        Assert.Null(Find(document, 0, 4));
    }
}
