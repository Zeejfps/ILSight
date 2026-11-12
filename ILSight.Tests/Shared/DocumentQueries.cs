using ILSight.Core;

namespace ILSight.Tests;

internal static class DocumentQueries
{
    public static (int Line, int Column) GetPosition(this DecompiledDocument document, string text, int skip = 0)
    {
        for (var i = 0; i < document.Lines.Length; i++)
        {
            var at = document.Lines[i].Text.IndexOf(text, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            if (skip-- == 0)
            {
                return (i, at);
            }
        }
        throw new InvalidOperationException($"'{text}' is not in the document:\n{Documents.GetText(document)}");
    }

    public static int LineContaining(this DecompiledDocument document, string text)
    {
        return document.GetPosition(text).Line;
    }

    /// <summary>The span over <paramref name="column"/> of line <paramref name="line"/>.</summary>
    public static DocumentSpan SpanAt(this DecompiledDocument document, int line, int column)
    {
        foreach (var span in document.Lines[line].Spans)
        {
            if (span.Start <= column && column < span.End)
            {
                return span;
            }
        }
        throw new InvalidOperationException("No span there.");
    }

    /// <summary>The span that is exactly the first <paramref name="token"/> of <paramref name="line"/>.</summary>
    public static DocumentSpan SpanAt(this DocumentLine line, string token)
    {
        var start = line.Text.IndexOf(token, StringComparison.Ordinal);
        foreach (var span in line.Spans)
        {
            if (span.Start == start && span.Length == token.Length)
            {
                return span;
            }
        }
        throw new InvalidOperationException($"No span '{token}' in '{line.Text}'");
    }

    public static IEnumerable<(string Text, DocumentSpan Span)> SpansOf(this DecompiledDocument document)
    {
        foreach (var line in document.Lines)
        {
            var text = line.Text;
            var spans = line.Spans.ToArray();
            foreach (var span in spans)
            {
                yield return (text.Substring(span.Start, span.Length), span);
            }
        }
    }

    /// <summary>The index in <see cref="DecompiledDocument.Folds"/> of the fold starting on the line containing <paramref name="text"/>.</summary>
    public static int FoldIndexAt(this DecompiledDocument document, string text)
    {
        var line = document.LineContaining(text);
        var index = Documents.FindFoldStartingAt(document, line);
        if (index is null)
        {
            throw new InvalidOperationException($"No fold starts at '{text}'");
        }

        return index.Value;
    }

    public static FoldRegion FoldAt(this DecompiledDocument document, string text)
    {
        return document.Folds[document.FoldIndexAt(text)];
    }
}
