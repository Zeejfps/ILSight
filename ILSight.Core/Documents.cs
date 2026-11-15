using System.Collections.Immutable;
using System.Reflection.Metadata.Ecma335;

namespace ILSight.Core;

/// <summary>Finding things in a <see cref="DecompiledDocument"/>.</summary>
public static class Documents
{
    /// <summary>The index in <see cref="DecompiledDocument.Folds"/> of the region starting on <paramref name="line"/>, or null.</summary>
    public static int? FindFoldStartingAt(DecompiledDocument document, int line)
    {
        int low = 0, high = document.Folds.Length - 1;
        while (low <= high)
        {
            var mid = (low + high) >> 1;
            var start = document.Folds[mid].StartLine;
            if (start < line)
            {
                low = mid + 1;
            }
            else if (start > line)
            {
                high = mid - 1;
            }
            else
            {
                return mid;
            }
        }
        return null;
    }

    /// <summary>A document of plain lines, e.g. a placeholder while decompiling.</summary>
    public static DecompiledDocument CreateFromText(string title, string text, DecompilationLanguage language = DecompilationLanguage.CSharp)
    {
        var builder = new DocumentBuilder { Language = language };
        builder.Write(text, SpanKind.Plain);
        return builder.BuildDocument(title);
    }

    /// <summary>The index of the line that defines <paramref name="reference"/>; null when this document does not define it.</summary>
    public static int? FindDefinitionLine(DecompiledDocument document, CodeReference.Navigable reference)
    {
        if (document.Definitions.TryGetValue(reference, out var line))
        {
            return line;
        }

        return null;
    }

    /// <summary>The index of the line that defines the entity <paramref name="node"/> stands for; null
    /// if it has none or this document does not define it.</summary>
    public static int? FindDefinitionLine(DecompiledDocument document, BrowseNode node)
    {
        var reference = node.Reference;
        if (reference == null)
        {
            return null;
        }

        return FindDefinitionLine(document, reference);
    }

    /// <summary>What names <paramref name="line"/> so it can be found again: the statement starting on
    /// it, else the entity it defines; null for a line with neither, such as a brace or a blank line.</summary>
    public static CodeAnchor? FindAnchorAt(DecompiledDocument document, int line)
    {
        if (line < 0 || line >= document.Lines.Length)
        {
            return null;
        }

        if (document.Statements.TryGetValue(line, out var statement))
        {
            return statement;
        }

        foreach (var span in document.Lines[line].Spans)
        {
            if (span.IsDefinition && span.Reference is CodeReference.Entity entity)
            {
                return GetDefinitionAnchor(entity);
            }
        }
        return null;
    }

    /// <summary>The line <paramref name="anchor"/> names in this document; null if it shows no such
    /// line. A statement no longer starting a line of its own finds the last one before it in its
    /// method, or the method's first.</summary>
    public static int? FindLine(DecompiledDocument document, CodeAnchor anchor)
    {
        if (anchor.Kind == CodeAnchorKind.Definition)
        {
            var definitionLines = EnsureDefinitionLines(document);
            if (definitionLines.TryGetValue(anchor, out var definitionLine))
            {
                return definitionLine;
            }

            return null;
        }

        var statementLines = EnsureStatementLines(document);
        var methodAnchor = GetMethodAnchor(anchor);
        if (!statementLines.TryGetValue(methodAnchor, out var points))
        {
            return null;
        }

        var found = points[0].Line;
        foreach (var (offset, line) in points)
        {
            if (offset > anchor.ILOffset)
            {
                break;
            }

            found = line;
        }
        return found;
    }

    // Any thread may be the one to fill them. Filling is a function of its own, whose closure a document that has them
    // does not pay for.
    private static Dictionary<CodeAnchor, int> EnsureDefinitionLines(DecompiledDocument document)
    {
        var kept = Volatile.Read(ref document.DefinitionLines);
        if (kept is not null)
        {
            return kept;
        }

        return FillDefinitionLines(document);
    }

    private static Dictionary<CodeAnchor, int> FillDefinitionLines(DecompiledDocument document)
    {
        return LazyInitializer.EnsureInitialized(ref document.DefinitionLines, () => ListDefinitionLines(document));
    }

    // Any thread may be the one to fill them.
    private static Dictionary<CodeAnchor, (int Offset, int Line)[]> EnsureStatementLines(DecompiledDocument document)
    {
        var kept = Volatile.Read(ref document.StatementLines);
        if (kept is not null)
        {
            return kept;
        }

        return FillStatementLines(document);
    }

    private static Dictionary<CodeAnchor, (int Offset, int Line)[]> FillStatementLines(DecompiledDocument document)
    {
        return LazyInitializer.EnsureInitialized(ref document.StatementLines, () => ListStatementLines(document));
    }

    private static Dictionary<CodeAnchor, int> ListDefinitionLines(DecompiledDocument document)
    {
        var lines = new Dictionary<CodeAnchor, int>(document.Definitions.Count);
        foreach (var (reference, line) in document.Definitions)
        {
            if (reference is CodeReference.Entity entity)
            {
                lines.TryAdd(GetDefinitionAnchor(entity), line);
            }
        }
        return lines;
    }

    private static Dictionary<CodeAnchor, (int Offset, int Line)[]> ListStatementLines(DecompiledDocument document)
    {
        return document.Statements
            .GroupBy(static s => GetMethodAnchor(s.Value))
            .ToDictionary(static g => g.Key, static g => g.Select(static s => (s.Value.ILOffset, s.Key)).Order().ToArray());
    }

    private static CodeAnchor GetMethodAnchor(CodeAnchor statement)
    {
        return statement with { ILOffset = 0 };
    }

    private static CodeAnchor GetDefinitionAnchor(CodeReference.Entity entity)
    {
        return new CodeAnchor
        {
            Kind = CodeAnchorKind.Definition,
            FilePath = entity.FilePath,
            Token = MetadataTokens.GetToken(entity.Handle),
        };
    }

    /// <summary><paramref name="document"/> with its notes (<see cref="DecompiledDocument.Notes"/>) in
    /// <paramref name="notes"/>' words: each note's line keeps what stands before the note and says the note found again
    /// from its cause; every other line, the folds and the anchors are the document's own, nothing decompiled again.
    /// The document itself when its notes already read so.</summary>
    public static DecompiledDocument Reword(DecompiledDocument document, IDocumentNotes notes)
    {
        if (document.Notes.IsEmpty)
        {
            return document;
        }

        var reworded = new Dictionary<int, (int Column, IReadOnlyList<NoteRun> Runs)>(document.Notes.Length);
        var changed = false;
        foreach (var placed in document.Notes)
        {
            var runs = placed.Note.Runs(notes);
            reworded[placed.Line] = (placed.Column, runs);
            changed |= !SaysRuns(document.Lines[placed.Line], placed.Column, runs);
        }

        if (!changed)
        {
            return document;
        }

        var texts = new string[document.Lines.Length];
        var counts = new int[document.Lines.Length];
        var spans = ImmutableArray.CreateBuilder<DocumentSpan>(document.Lines.IsEmpty ? 0 : document.Lines[0].DocumentSpans.Length);
        for (var i = 0; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];
            var first = spans.Count;
            if (reworded.TryGetValue(i, out var note))
            {
                texts[i] = RewordLine(line, note.Column, note.Runs, spans);
            }
            else
            {
                texts[i] = line.Text;
                spans.AddRange(line.Spans);
            }

            counts[i] = spans.Count - first;
        }

        var shared = spans.ToImmutable();
        var lines = ImmutableArray.CreateBuilder<DocumentLine>(document.Lines.Length);
        var start = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            lines.Add(new DocumentLine { Text = texts[i], DocumentSpans = shared, FirstSpan = start, SpanCount = counts[i] });
            start += counts[i];
        }

        return new DecompiledDocument
        {
            Title = document.Title,
            Language = document.Language,
            Error = document.Error,
            Lines = lines.MoveToImmutable(),
            Definitions = document.Definitions,
            Statements = document.Statements,
            EntityNames = document.EntityNames,
            Folds = document.Folds,
            UnreadReferences = document.UnreadReferences,
            Notes = document.Notes,
            AsMade = document.AsMade,
        };
    }

    private static bool SaysRuns(DocumentLine line, int column, IReadOnlyList<NoteRun> runs)
    {
        var at = column;
        foreach (var run in runs)
        {
            if (at + run.Text.Length > line.Text.Length)
            {
                return false;
            }

            var order = string.CompareOrdinal(line.Text, at, run.Text, 0, run.Text.Length);
            if (order != 0)
            {
                return false;
            }

            at += run.Text.Length;
        }

        return at == line.Text.Length;
    }

    // The line's text and spans up to the note, then the note's runs to the line's end.
    private static string RewordLine(DocumentLine line, int column, IReadOnlyList<NoteRun> runs, ImmutableArray<DocumentSpan>.Builder spans)
    {
        foreach (var span in line.Spans)
        {
            if (span.Start >= column)
            {
                break;
            }

            spans.Add(span.End <= column ? span : span with { Length = column - span.Start });
        }

        var text = new System.Text.StringBuilder(line.Text, 0, Math.Min(column, line.Text.Length), column + 64);
        foreach (var run in runs)
        {
            if (run.Text.Length == 0)
            {
                continue;
            }

            spans.Add(new DocumentSpan { Start = text.Length, Length = run.Text.Length, Kind = run.Kind, Reference = run.Reference, IsDefinition = false });
            text.Append(run.Text);
        }

        return text.ToString();
    }

    /// <summary>The whole text, lines joined by '\n'.</summary>
    public static string GetText(DecompiledDocument document)
    {
        return GetText(document, 0, document.Lines.Length);
    }

    /// <summary>The text of lines <paramref name="first"/> up to but not including <paramref name="end"/>,
    /// joined by '\n'.</summary>
    public static string GetText(DecompiledDocument document, int first, int end)
    {
        first = Math.Clamp(first, 0, document.Lines.Length);
        end = Math.Clamp(end, first, document.Lines.Length);
        var builder = new System.Text.StringBuilder();
        for (var i = first; i < end; i++)
        {
            if (i > first)
            {
                builder.Append('\n');
            }

            builder.Append(document.Lines[i].Text);
        }
        return builder.ToString();
    }
}
