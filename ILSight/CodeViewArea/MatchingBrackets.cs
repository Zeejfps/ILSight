using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>The bracket at a code view's caret, or just before it, and the one that matches it. Brackets in strings
/// and comments do not count, and the match is looked for only so many lines away.</summary>
public static class MatchingBrackets
{
    private const string Opens = "([{";
    private const string Closes = ")]}";
    private const int MaxLinesScanned = 2000;

    internal static BracketPair? Match(DecompiledDocument document, TextPosition caret)
    {
        var after = MatchAt(document, caret);
        if (after is not null)
        {
            return after;
        }

        if (caret.Column == 0)
        {
            return null;
        }

        var before = caret with { Column = caret.Column - 1 };
        return MatchAt(document, before);
    }

    private static BracketPair? MatchAt(DecompiledDocument document, TextPosition at)
    {
        var bracket = FindBracketAt(document, at);
        if (bracket is null)
        {
            return null;
        }

        var open = Opens.IndexOf(bracket.Value);
        if (open >= 0)
        {
            return MatchForward(document, at, Opens[open], Closes[open]);
        }

        var close = Closes.IndexOf(bracket.Value);
        if (close >= 0)
        {
            return MatchBackward(document, at, Opens[close], Closes[close]);
        }

        return null;
    }

    private static char? FindBracketAt(DecompiledDocument document, TextPosition at)
    {
        if (at.Line < 0 || at.Line >= document.Lines.Length)
        {
            return null;
        }

        var line = document.Lines[at.Line];
        var span = CodeText.SpanAt(line.Spans, at.Column);
        if (span is null)
        {
            return null;
        }

        if (!IsCode(line.Spans[span.Value]))
        {
            return null;
        }

        return line.Text[at.Column];
    }

    private static bool IsCode(DocumentSpan span)
    {
        if (span.Kind == SpanKind.String)
        {
            return false;
        }

        return span.Kind != SpanKind.Comment;
    }

    private static BracketPair? MatchForward(DecompiledDocument document, TextPosition open, char opening, char closing)
    {
        var depth = 0;
        var last = Math.Min(document.Lines.Length - 1, open.Line + MaxLinesScanned);
        for (var index = open.Line; index <= last; index++)
        {
            var line = document.Lines[index];
            var from = index == open.Line ? open.Column + 1 : 0;
            foreach (var span in line.Spans)
            {
                if (span.End <= from)
                {
                    continue;
                }

                if (!IsCode(span))
                {
                    continue;
                }

                for (var column = Math.Max(span.Start, from); column < span.End; column++)
                {
                    var c = line.Text[column];
                    if (c == opening)
                    {
                        depth++;
                    }

                    if (c != closing)
                    {
                        continue;
                    }

                    if (depth == 0)
                    {
                        return new BracketPair
                        {
                            Open = open,
                            Close = new TextPosition { Line = index, Column = column },
                        };
                    }

                    depth--;
                }
            }
        }
        return null;
    }

    private static BracketPair? MatchBackward(DecompiledDocument document, TextPosition close, char opening, char closing)
    {
        var depth = 0;
        var first = Math.Max(0, close.Line - MaxLinesScanned);
        for (var index = close.Line; index >= first; index--)
        {
            var line = document.Lines[index];
            var before = index == close.Line ? close.Column : line.Text.Length;
            var spans = line.Spans;
            for (var s = spans.Length - 1; s >= 0; s--)
            {
                var span = spans[s];
                if (span.Start >= before)
                {
                    continue;
                }

                if (!IsCode(span))
                {
                    continue;
                }

                for (var column = Math.Min(span.End, before) - 1; column >= span.Start; column--)
                {
                    var c = line.Text[column];
                    if (c == closing)
                    {
                        depth++;
                    }

                    if (c != opening)
                    {
                        continue;
                    }

                    if (depth == 0)
                    {
                        return new BracketPair
                        {
                            Open = new TextPosition { Line = index, Column = column },
                            Close = close,
                        };
                    }

                    depth--;
                }
            }
        }
        return null;
    }
}
