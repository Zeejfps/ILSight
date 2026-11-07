using System.Collections.Immutable;
using System.Reflection.Metadata.Ecma335;

namespace ILSight.Core;

/// <summary>The language a node is decompiled to.</summary>
public enum DecompilationLanguage
{
    CSharp,
    IL,

    /// <summary>IL with the C# each statement was decompiled from written above its first instruction.</summary>
    ILWithCSharp,
}

/// <summary>What each <see cref="DecompilationLanguage"/> is called and what it shows.</summary>
public static class DecompilationLanguages
{
    public static IReadOnlyList<DecompilationLanguage> All { get; } = Enum.GetValues<DecompilationLanguage>();

    /// <summary>The language's name as ILSpy shows it.</summary>
    public static string Label(DecompilationLanguage language) => language switch
    {
        DecompilationLanguage.IL => "IL",
        DecompilationLanguage.ILWithCSharp => "IL with C#",
        _ => "C#",
    };

    /// <summary>Whether the language shows decompiled C#, which the decompiler options shape.</summary>
    public static bool ShowsCSharp(DecompilationLanguage language) => language != DecompilationLanguage.IL;

    /// <summary>Whether the language is disassembled to IL.</summary>
    public static bool IsIL(DecompilationLanguage language) => language != DecompilationLanguage.CSharp;
}

/// <summary>How a <see cref="DocumentSpan"/> is colored.</summary>
public enum SpanKind : byte
{
    Plain,
    Keyword,
    Type,
    Namespace,
    Method,
    Field,
    Property,
    Event,
    Parameter,
    Local,
    String,
    Number,
    Comment,
    Preprocessor,
    ILOpcode,
    ILLabel,
}

/// <summary>A run of a <see cref="DocumentLine"/>'s text: characters <see cref="Start"/> up to
/// <see cref="End"/>, optionally pointing at a <see cref="Reference"/>, which this run defines when
/// <see cref="IsDefinition"/> is set.</summary>
public readonly record struct DocumentSpan(int Start, int Length, SpanKind Kind, CodeReference? Reference, bool IsDefinition)
{
    public int End => Start + Length;
}

/// <summary>One line of a <see cref="DecompiledDocument"/>. Its <see cref="Spans"/> are in order and
/// cover <see cref="Text"/> exactly, with no gaps.</summary>
public readonly struct DocumentLine
{
    public required string Text { get; init; }

    /// <summary>The spans of the whole document, which the lines share.</summary>
    public required DocumentSpan[] DocumentSpans { get; init; }

    /// <summary>Where this line's spans start in <see cref="DocumentSpans"/>.</summary>
    public required int FirstSpan { get; init; }

    /// <summary>How many spans this line has.</summary>
    public required int SpanCount { get; init; }

    public ReadOnlySpan<DocumentSpan> Spans => new(DocumentSpans, FirstSpan, SpanCount);

    public override string ToString() => Text;
}

/// <summary>A foldable range of a <see cref="DecompiledDocument"/>. Collapsed, line
/// <see cref="StartLine"/> stays in view followed by <see cref="CollapsedText"/>, and the lines after
/// it up to and including <see cref="EndLine"/> are hidden.</summary>
/// <param name="DefaultCollapsed">Whether the region starts out collapsed, as the using directives do.</param>
/// <param name="IsDefinition">Whether the region is the body of a type or member definition.</param>
public readonly record struct FoldRegion(int StartLine, int EndLine, string CollapsedText, bool DefaultCollapsed, bool IsDefinition);

/// <summary>
/// The immutable result of decompiling a node: lines of classified, referencing text. Safe to read
/// from any thread while other documents are being produced.
/// </summary>
public sealed class DecompiledDocument
{
    public required IReadOnlyDictionary<CodeReference.Navigable, int> Definitions { get; init; }

    public required IReadOnlyDictionary<int, CodeAnchor> Statements { get; init; }

    /// <summary>The line of each definition by its anchor, filled by <see cref="Documents.FindLine"/> the first time it is asked for.</summary>
    public Dictionary<CodeAnchor, int>? DefinitionLines;

    /// <summary>The statements of each method by its anchor, filled by <see cref="Documents.FindLine"/> the first time it is asked for.</summary>
    public Dictionary<CodeAnchor, (int Offset, int Line)[]>? StatementLines;

    /// <summary>A document with no lines.</summary>
    public static DecompiledDocument Empty { get; } = Documents.CreateFromText("", "");

    /// <summary>A short name for the document, e.g. for a tab.</summary>
    public required string Title { get; init; }

    public required DecompilationLanguage Language { get; init; }

    /// <summary>What decompiling failed with, which the lines describe; null when it did not fail.</summary>
    public required Exception? Error { get; init; }

    public required ImmutableArray<DocumentLine> Lines { get; init; }

    /// <summary>The foldable regions, ordered by <see cref="FoldRegion.StartLine"/>, each starting on
    /// a line of its own and nested whole inside any region it starts in.</summary>
    public required ImmutableArray<FoldRegion> Folds { get; init; }

    public override string ToString()
    {
        return Title;
    }
}

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
            if (start < line) low = mid + 1;
            else if (start > line) high = mid - 1;
            else return mid;
        }
        return null;
    }

    /// <summary>A document of plain lines, e.g. a placeholder while decompiling.</summary>
    public static DecompiledDocument CreateFromText(string title, string text, DecompilationLanguage language = DecompilationLanguage.CSharp)
    {
        var builder = new DocumentBuilder { Language = language };
        builder.Write(text, SpanKind.Plain);
        return DocumentWriting.BuildDocument(builder, title);
    }

    /// <summary>The index of the line that defines <paramref name="reference"/>, or -1.</summary>
    public static int FindDefinitionLine(DecompiledDocument document, CodeReference.Navigable reference)
    {
        return document.Definitions.GetValueOrDefault(reference, -1);
    }

    /// <summary>The index of the line that defines the entity <paramref name="node"/> stands for, or -1
    /// if it has none or this document does not define it.</summary>
    public static int FindDefinitionLine(DecompiledDocument document, BrowseNode node)
    {
        var reference = NodeDefinitions.ReferenceOf(node.Subject);
        if (reference == null)
            return -1;
        return FindDefinitionLine(document, reference);
    }

    /// <summary>What names <paramref name="line"/> so it can be found again: the statement starting on
    /// it, else the entity it defines; null for a line with neither, such as a brace or a blank line.</summary>
    public static CodeAnchor? FindAnchorAt(DecompiledDocument document, int line)
    {
        if (line < 0 || line >= document.Lines.Length) return null;
        if (document.Statements.TryGetValue(line, out var statement)) return statement;
        foreach (var span in document.Lines[line].Spans)
        {
            if (span.IsDefinition && span.Reference is CodeReference.Entity entity)
                return GetDefinitionAnchor(entity);
        }
        return null;
    }

    /// <summary>The line <paramref name="anchor"/> names in this document, or -1 if it shows no such
    /// line. A statement no longer starting a line of its own finds the last one before it in its
    /// method, or the method's first.</summary>
    public static int FindLine(DecompiledDocument document, CodeAnchor anchor)
    {
        if (anchor.Kind == CodeAnchorKind.Definition)
            return EnsureDefinitionLines(document).GetValueOrDefault(anchor, -1);

        var statementLines = EnsureStatementLines(document);
        if (!statementLines.TryGetValue(GetMethodAnchor(anchor), out var points)) return -1;
        var found = points[0].Line;
        foreach (var (offset, line) in points)
        {
            if (offset > anchor.ILOffset) break;
            found = line;
        }
        return found;
    }

    /// <summary>The kept lines of the definitions; any thread may be the one to fill them. Filling is a function of
    /// its own, whose closure a document that has them does not pay for.</summary>
    private static Dictionary<CodeAnchor, int> EnsureDefinitionLines(DecompiledDocument document)
    {
        var kept = Volatile.Read(ref document.DefinitionLines);
        if (kept != null)
            return kept;
        return FillDefinitionLines(document);
    }

    private static Dictionary<CodeAnchor, int> FillDefinitionLines(DecompiledDocument document)
    {
        return LazyInitializer.EnsureInitialized(ref document.DefinitionLines, () => ListDefinitionLines(document));
    }

    /// <summary>The kept lines of the statements; any thread may be the one to fill them.</summary>
    private static Dictionary<CodeAnchor, (int Offset, int Line)[]> EnsureStatementLines(DecompiledDocument document)
    {
        var kept = Volatile.Read(ref document.StatementLines);
        if (kept != null)
            return kept;
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
                lines.TryAdd(GetDefinitionAnchor(entity), line);
        }
        return lines;
    }

    private static Dictionary<CodeAnchor, (int Offset, int Line)[]> ListStatementLines(DecompiledDocument document)
    {
        return document.Statements
            .GroupBy(static s => GetMethodAnchor(s.Value))
            .ToDictionary(static g => g.Key, static g => g.Select(static s => (s.Value.ILOffset, s.Key)).Order().ToArray());
    }

    /// <summary>The statement anchor <paramref name="statement"/> with its offset left out, naming its method.</summary>
    private static CodeAnchor GetMethodAnchor(CodeAnchor statement)
    {
        return statement with { ILOffset = 0 };
    }

    private static CodeAnchor GetDefinitionAnchor(CodeReference.Entity entity)
    {
        return new(CodeAnchorKind.Definition, entity.FilePath, MetadataTokens.GetToken(entity.Handle));
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
            if (i > first) builder.Append('\n');
            builder.Append(document.Lines[i].Text);
        }
        return builder.ToString();
    }
}
