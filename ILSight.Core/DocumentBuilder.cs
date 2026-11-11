using static ILSight.Core.DocumentBuilder;
using System.Diagnostics;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>What the decompiler writes to: its ITextOutput demands the methods here, which forward to
/// <see cref="DocumentWriting"/>.</summary>
internal sealed class DocumentBuilder : ITextOutput
{
    public required DecompilationLanguage Language { get; init; }
    public readonly StringBuilder Line = new();
    public readonly List<DocumentSpan> Spans = new();
    public readonly List<(string Text, int First, int Count)> Lines = new();
    public readonly Dictionary<CodeReference.Navigable, int> Definitions = new();
    public readonly Dictionary<int, (CodeReference.Entity Method, int Offset)> MarkedStatements = new();
    public readonly List<DocumentSpan> Lexed = new();
    public readonly Dictionary<object, int> LocalKeys = new();
    public readonly Stack<OpenFold> OpenFolds = new();
    public readonly List<(OpenFold Start, int EndLine)> Folds = new();
    public LoadedModule? Assembly { get; internal init; }
    internal int LineFirstSpan;
    internal int IndentLevel;
    internal bool LineStarted;
    internal bool LineClassified;
    internal CodeReference.Entity? Scope;

    public string IndentationString { get; set; } = "    ";

    public SpanKind? Kind { get; internal set; }

    /// <summary>Where the next text written to the current line starts.</summary>
    internal int Column => Line.Length;

    /// <summary>Where the next text written lands, from line 1 and column 1, as the decompiler records
    /// it in the syntax tree: past the indentation a fresh line is still to get.</summary>
    internal TextLocation Location
    {
        get
        {
            var column = Line.Length;
            if (!LineStarted)
            {
                column = IndentLevel * IndentationString.Length;
            }

            return new TextLocation(Lines.Count + 1, column + 1);
        }
    }

    public void Write(string text, SpanKind kind, CodeReference? reference = null, bool isDefinition = false)
    {
        DocumentWriting.Write(this, text, kind, reference, isDefinition);
    }

    public void Indent()
    {
        DocumentWriting.IndentMore(this);
    }

    public void Unindent()
    {
        DocumentWriting.IndentLess(this);
    }

    public void Write(char ch)
    {
        DocumentWriting.Write(this, ch);
    }

    public void Write(string text)
    {
        DocumentWriting.Write(this, text);
    }

    public void WriteLine()
    {
        DocumentWriting.WriteLine(this);
    }

    public void WriteReference(OpCodeInfo opCode, bool omitSuffix = false)
    {
        DocumentWriting.WriteReference(this, opCode, omitSuffix);
    }

    public void WriteReference(MetadataFile metadata, Handle handle, string text, string protocol = "decompile", bool isDefinition = false)
    {
        DocumentWriting.WriteReference(this, metadata, handle, text, protocol, isDefinition);
    }

    public void WriteReference(IType type, string text, bool isDefinition = false)
    {
        DocumentWriting.WriteReference(this, type, text, isDefinition);
    }

    public void WriteReference(IMember member, string text, bool isDefinition = false)
    {
        DocumentWriting.WriteReference(this, member, text, isDefinition);
    }

    public void WriteLocalReference(string text, object reference, bool isDefinition = false, bool isHoverOnly = false)
    {
        DocumentWriting.WriteLocalReference(this, text, reference, isDefinition, isHoverOnly);
    }

    public void MarkDefinitionStart()
    {
        DocumentWriting.MarkDefinitionStart(this);
    }

    public void MarkFoldStart(string collapsedText = DocumentWriting.DefaultFoldText, bool defaultCollapsed = false, bool isDefinition = false)
    {
        DocumentWriting.MarkFoldStart(this, collapsedText, defaultCollapsed, isDefinition);
    }

    public void MarkFoldEnd()
    {
        DocumentWriting.MarkFoldEnd(this);
    }

    public readonly record struct OpenFold(int Line, int Column, string Text, bool DefaultCollapsed, bool IsDefinition);
}

/// <summary>Writing a <see cref="DecompiledDocument"/> a token at a time.</summary>
internal static class DocumentWriting
{
    private const int MaxSignatureLines = 8;
    internal const string DefaultFoldText = "...";
    private const string SourceLinePrefix = "// ";

    public static DecompiledDocument BuildDocument(DocumentBuilder builder, string title)
    {
        return BuildDocument(builder, title, null);
    }

    /// <summary>The document written so far, describing how decompiling failed with <paramref name="error"/>.</summary>
    public static DecompiledDocument BuildFailure(DocumentBuilder builder, string title, Exception error)
    {
        return BuildDocument(builder, title, error);
    }

    private static DecompiledDocument BuildDocument(DocumentBuilder builder, string title, Exception? error)
    {
        if (builder.Line.Length > 0 || builder.LineFirstSpan < builder.Spans.Count || builder.Lines.Count == 0)
        {
            WriteLine(builder);
        }

        var spans = builder.Spans.ToImmutableArray();
        var lines = ImmutableArray.CreateBuilder<DocumentLine>(builder.Lines.Count);
        foreach (var (text, first, count) in builder.Lines)
        {
            lines.Add(new DocumentLine { Text = text, DocumentSpans = spans, FirstSpan = first, SpanCount = count });
        }

        var built = lines.MoveToImmutable();
        return new DecompiledDocument { Title = title, Language = builder.Language, Error = error, Lines = built, Definitions = builder.Definitions, Statements = ListStatements(builder), Folds = ListFoldRegions(builder, built) };
    }

    public static void Write(DocumentBuilder builder, string text, SpanKind kind, CodeReference? reference = null, bool isDefinition = false)
    {
        var start = 0;
        while (true)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline;
            var segmentEnd = end > start && text[end - 1] == '\r' ? end - 1 : end;
            Append(builder, text.AsSpan(start, segmentEnd - start), kind, reference, isDefinition);
            if (newline < 0)
            {
                return;
            }

            WriteLine(builder);
            start = newline + 1;
        }
    }

    public static void IndentMore(DocumentBuilder builder)
    {
        builder.IndentLevel++;
    }

    public static void IndentLess(DocumentBuilder builder)
    {
        builder.IndentLevel = Math.Max(0, builder.IndentLevel - 1);
    }

    public static void Write(DocumentBuilder builder, char ch)
    {
        if (ch == '\n')
        {
            WriteLine(builder);
        }
        else if (ch != '\r')
        {
            Append(builder, new ReadOnlySpan<char>(in ch), builder.Kind ?? SpanKind.Plain, null, false);
        }
    }

    public static void Write(DocumentBuilder builder, string text)
    {
        Write(builder, text, builder.Kind ?? SpanKind.Plain);
    }

    public static void WriteLine(DocumentBuilder builder)
    {
        if (IsUnlexedIL(builder))
        {
            LexILLine(builder);
        }

        builder.Lines.Add((builder.Line.ToString(), builder.LineFirstSpan, builder.Spans.Count - builder.LineFirstSpan));
        builder.Line.Clear();
        builder.LineFirstSpan = builder.Spans.Count;
        builder.LineStarted = false;
        builder.LineClassified = false;
    }

    private static bool IsUnlexedIL(DocumentBuilder builder)
    {
        if (builder.LineClassified)
        {
            return false;
        }

        return DecompilationLanguages.IsIL(builder.Language);
    }

    /// <summary>Writes <paramref name="line"/> of C# as a comment line, from column <paramref name="from"/>
    /// on: the statement from <paramref name="start"/> up to <paramref name="end"/> in C#'s colors, the
    /// rest dimmed as a comment. Names of types and members stay links; locals do not, as they are the
    /// C# document's.</summary>
    internal static void WriteSourceLine(DocumentBuilder builder, DocumentLine line, int from, int start, int end)
    {
        Append(builder, SourceLinePrefix, SpanKind.Comment, null, false);
        foreach (var span in line.Spans)
        {
            AppendSourceSpan(builder, line.Text, span, from, start, end);
        }

        builder.LineClassified = true;
        WriteLine(builder);
    }

    /// <summary>Appends <paramref name="span"/>'s text from column <paramref name="from"/> on: the part
    /// inside the statement as it is, the parts before and after it as a comment.</summary>
    private static void AppendSourceSpan(DocumentBuilder builder, string text, DocumentSpan span, int from, int start, int end)
    {
        var first = Math.Max(span.Start, from);
        var reference = GetLink(span.Reference);
        AppendPart(builder, text, first, Math.Min(span.End, start), SpanKind.Comment, null);
        AppendPart(builder, text, Math.Max(first, start), Math.Min(span.End, end), span.Kind, reference);
        AppendPart(builder, text, Math.Max(first, end), span.End, SpanKind.Comment, null);
    }

    private static void AppendPart(DocumentBuilder builder, string text, int from, int to, SpanKind kind, CodeReference? reference)
    {
        if (to <= from)
        {
            return;
        }

        Append(builder, text.AsSpan(from, to - from), kind, reference, false);
    }

    /// <summary>The reference a copied span keeps: a type's or member's, not a local's or label's.</summary>
    private static CodeReference? GetLink(CodeReference? reference)
    {
        if (reference is CodeReference.Local or CodeReference.Label)
        {
            return null;
        }

        return reference;
    }

    public static void WriteReference(DocumentBuilder builder, OpCodeInfo opCode, bool omitSuffix = false)
    {
        var name = opCode.Name;
        if (omitSuffix)
        {
            var lastDot = name.LastIndexOf('.');
            if (lastDot > 0)
            {
                name = name[..(lastDot + 1)];
            }
        }
        Write(builder, name, SpanKind.ILOpcode);
    }

    public static void WriteReference(DocumentBuilder builder, MetadataFile metadata, Handle handle, string text, string protocol = "decompile", bool isDefinition = false)
    {
        var reference = FindReference(builder, metadata, handle, text);
        var kind = builder.Kind ?? (reference is null ? SpanKind.Plain : isDefinition && text.StartsWith('.') ? SpanKind.Keyword : GetSpanKind(reference));
        WriteReferenceSpan(builder, text, kind, reference, isDefinition);
    }

    public static void WriteReference(DocumentBuilder builder, IType type, string text, bool isDefinition = false)
    {
        var definition = type.GetDefinition();
        var reference = definition != null ? CodeReferences.Find(definition, text) : null;
        WriteReferenceSpan(builder, text, builder.Kind ?? SpanKind.Type, reference, isDefinition);
    }

    public static void WriteReference(DocumentBuilder builder, IMember member, string text, bool isDefinition = false)
    {
        var definition = member.MemberDefinition ?? member;
        var kind = builder.Kind ?? GetSpanKind(CodeReferences.GetKind(definition.SymbolKind));
        WriteReferenceSpan(builder, text, kind, CodeReferences.Find(definition, text), isDefinition);
    }

    public static void WriteLocalReference(DocumentBuilder builder, string text, object reference, bool isDefinition = false, bool isHoverOnly = false)
    {
        var spanKind = reference switch
        {
            int => SpanKind.ILLabel,
            string s when s.StartsWith("param_", StringComparison.Ordinal) => SpanKind.Parameter,
            ILVariable { Kind: VariableKind.Parameter } => SpanKind.Parameter,
            IParameter => SpanKind.Parameter,
            _ => SpanKind.Local,
        };
        if (isDefinition && reference is int offset && builder.Scope != null && builder.Scope.Kind == EntityKind.Method)
        {
            MarkStatement(builder, builder.Scope, offset);
        }

        var scope = reference is ILVariable ? null : builder.Scope;
        if (!builder.LocalKeys.TryGetValue(reference, out var key))
        {
            builder.LocalKeys.Add(reference, key = builder.LocalKeys.Count);
        }

        CodeReference local = reference is int ? new CodeReference.Label { Name = text, Key = key, Scope = scope } : new CodeReference.Local { Name = text, Key = key, Scope = scope };
        Write(builder, text, builder.Kind ?? spanKind, local, isDefinition);
    }

    /// <summary>Marks the line being written as where the statement at <paramref name="ilOffset"/> of
    /// <paramref name="method"/> starts. A line keeps the first method marked on it and its lowest offset.</summary>
    public static void MarkStatement(DocumentBuilder builder, IMethod method, int ilOffset)
    {
        var reference = CodeReferences.Find(method);
        if (reference != null)
        {
            MarkStatement(builder, reference, ilOffset);
        }
    }

    private static void MarkStatement(DocumentBuilder builder, CodeReference.Entity method, int ilOffset)
    {
        var line = builder.Lines.Count;
        if (builder.MarkedStatements.TryGetValue(line, out var marked) && (marked.Method != method || marked.Offset <= ilOffset))
        {
            return;
        }

        builder.MarkedStatements[line] = (method, ilOffset);
    }

    private static Dictionary<int, CodeAnchor> ListStatements(DocumentBuilder builder)
    {
        var anchors = new Dictionary<int, CodeAnchor>(builder.MarkedStatements.Count);
        foreach (var (line, (method, offset)) in builder.MarkedStatements)
        {
            anchors[line] = new CodeAnchor(CodeAnchorKind.Statement, method.FilePath, MetadataTokens.GetToken(method.Handle), offset);
        }

        return anchors;
    }

    public static void MarkDefinitionStart(DocumentBuilder builder)
    {
    }

    public static void MarkFoldStart(DocumentBuilder builder, string collapsedText = DefaultFoldText, bool defaultCollapsed = false, bool isDefinition = false)
    {
        builder.OpenFolds.Push(new OpenFold(builder.Lines.Count, builder.LineStarted ? builder.Line.Length : 0, collapsedText, defaultCollapsed, isDefinition));
    }

    public static void MarkFoldEnd(DocumentBuilder builder)
    {
        if (builder.OpenFolds.TryPop(out var start))
        {
            builder.Folds.Add((start, builder.LineStarted ? builder.Lines.Count : builder.Lines.Count - 1));
        }
    }

    private static ImmutableArray<FoldRegion> ListFoldRegions(DocumentBuilder builder, ImmutableArray<DocumentLine> lines)
    {
        if (builder.Folds.Count == 0)
        {
            return ImmutableArray<FoldRegion>.Empty;
        }

        var regions = new List<FoldRegion>(builder.Folds.Count);
        foreach (var (start, endLine) in builder.Folds)
        {
            if (endLine <= start.Line - 1 || endLine >= lines.Length)
            {
                continue;
            }

            var closes = lines[endLine].Text.AsSpan().TrimStart().StartsWith("}");
            var opening = FirstCharFrom(lines, start.Line, start.Column, endLine, out var openingLine);
            var ownLine = lines[start.Line].Text.AsSpan(0, Math.Min(start.Column, lines[start.Line].Text.Length)).IsWhiteSpace();
            if (opening != '{' && closes)
            {
                openingLine = BodyAfterSignature(lines, start.Line, endLine);
            }

            var brace = closes && openingLine >= 0;
            var header = !brace ? start.Line
                : openingLine > start.Line ? openingLine - 1
                : ownLine && start.Line > 0 ? start.Line - 1
                : start.Line;
            if (endLine <= header)
            {
                continue;
            }

            var text = HasOwnText(start, lines[header]) ? start.Text
                : !brace ? "…"
                : openingLine > header ? "{ … }"
                : "… " + lines[endLine].Text.Trim();
            regions.Add(new FoldRegion(header, endLine, text, start.DefaultCollapsed, start.IsDefinition));
        }
        regions.Sort(static (a, b) => a.StartLine != b.StartLine ? a.StartLine.CompareTo(b.StartLine) : b.EndLine.CompareTo(a.EndLine));

        var nested = ImmutableArray.CreateBuilder<FoldRegion>(regions.Count);
        var open = new Stack<FoldRegion>();
        foreach (var region in regions)
        {
            while (open.Count > 0 && open.Peek().EndLine < region.StartLine)
            {
                open.Pop();
            }

            if (nested.Count > 0 && nested[^1].StartLine == region.StartLine)
            {
                continue;
            }

            if (open.Count > 0 && region.EndLine > open.Peek().EndLine)
            {
                continue;
            }

            nested.Add(region);
            open.Push(region);
        }
        return nested.ToImmutable();
    }

    /// <summary>Whether <paramref name="start"/> names a collapsed text that says more than its
    /// header line, which stays in view; an XML doc comment names its own first line.</summary>
    private static bool HasOwnText(OpenFold start, DocumentLine header)
    {
        if (start.Text == DefaultFoldText)
        {
            return false;
        }

        return start.Text.Trim() != header.Text.Trim();
    }

    private static int BodyAfterSignature(ImmutableArray<DocumentLine> lines, int start, int endLine)
    {
        for (var line = start + 1; line < endLine && line <= start + MaxSignatureLines; line++)
        {
            var text = lines[line].Text.AsSpan().Trim();
            if (text.StartsWith("{"))
            {
                return text.Length == 1 ? line : -1;
            }
        }
        return -1;
    }

    private static char FirstCharFrom(ImmutableArray<DocumentLine> lines, int line, int column, int lastLine, out int foundLine)
    {
        for (foundLine = line; foundLine <= lastLine; foundLine++, column = 0)
        {
            var text = lines[foundLine].Text;
            for (var i = column; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return text[i];
                }
            }
        }
        foundLine = -1;
        return ' ';
    }

    private static void WriteReferenceSpan(DocumentBuilder builder, string text, SpanKind kind, CodeReference? reference, bool isDefinition)
    {
        if (isDefinition && reference is CodeReference.Entity entity && entity.Kind == EntityKind.Method)
        {
            builder.Scope = entity;
        }

        Write(builder, text, kind, reference, isDefinition);
    }

    private static void Append(DocumentBuilder builder, ReadOnlySpan<char> text, SpanKind kind, CodeReference? reference, bool isDefinition)
    {
        if (text.IsEmpty)
        {
            return;
        }

        if (!builder.LineStarted)
        {
            builder.LineStarted = true;
            for (var i = 0; i < builder.IndentLevel; i++)
            {
                AppendSpan(builder, builder.IndentationString, SpanKind.Plain, null, false);
            }
        }
        if (isDefinition && reference is CodeReference.Navigable navigable)
        {
            builder.Definitions.TryAdd(navigable, builder.Lines.Count);
        }

        AppendSpan(builder, text, kind, reference, isDefinition);
    }

    private static void AppendSpan(DocumentBuilder builder, ReadOnlySpan<char> text, SpanKind kind, CodeReference? reference, bool isDefinition)
    {
        if (text.IsEmpty)
        {
            return;
        }

        var start = builder.Line.Length;
        builder.Line.Append(text);
        if (reference is null && !isDefinition && builder.Spans.Count > builder.LineFirstSpan)
        {
            var last = builder.Spans[^1];
            if (last.Kind == kind && last.Reference is null && !last.IsDefinition && last.End == start)
            {
                builder.Spans[^1] = last with { Length = last.Length + text.Length };
                return;
            }
        }
        builder.Spans.Add(new DocumentSpan(start, text.Length, kind, reference, isDefinition));
    }

    private static void LexILLine(DocumentBuilder builder)
    {
        var count = builder.Spans.Count - builder.LineFirstSpan;
        var any = false;
        for (var i = 0; i < count && !any; i++)
        {
            any = builder.Spans[builder.LineFirstSpan + i] is { Kind: SpanKind.Plain, Reference: null };
        }

        if (!any)
        {
            return;
        }

        builder.Lexed.Clear();
        var text = builder.Line.ToString();
        var inComment = false;
        for (var i = 0; i < count; i++)
        {
            var span = builder.Spans[builder.LineFirstSpan + i];
            if (span.Kind != SpanKind.Plain || span.Reference is not null)
            {
                builder.Lexed.Add(inComment && span.Reference is null ? span with { Kind = SpanKind.Comment } : span);
                continue;
            }
            ILLexer.Classify(text, span.Start, span.End, ref inComment, builder.Lexed);
        }
        builder.Spans.RemoveRange(builder.LineFirstSpan, count);
        builder.Spans.AddRange(builder.Lexed);
    }

    private static CodeReference.Navigable? FindReference(DocumentBuilder builder, MetadataFile metadata, Handle handle, string text)
    {
        if (handle.IsNil)
        {
            return null;
        }

        if (handle.Kind is HandleKind.TypeReference or HandleKind.TypeSpecification or HandleKind.MemberReference or HandleKind.MethodSpecification)
        {
            var definition = ResolveDefinition(builder, metadata, (EntityHandle)handle);
            if (definition != null)
            {
                return CodeReferences.Find(definition, text);
            }
        }
        return CodeReferences.Find(metadata.Metadata, handle, text, AssemblyPath.Of(metadata.FileName));
    }

    private static IEntity? ResolveDefinition(DocumentBuilder builder, MetadataFile metadata, EntityHandle handle)
    {
        if (builder.Assembly is null || !ReferenceEquals(metadata, builder.Assembly.File))
        {
            return null;
        }

        try
        {
            return Definitions.Resolve(LoadedAssemblies.EnsureMainModule(builder.Assembly), handle);
        }
        catch (Exception e) when (e is BadImageFormatException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static SpanKind GetSpanKind(CodeReference.Navigable reference)
    {
        return reference switch
            {
                CodeReference.Entity entity => GetSpanKind(entity.Kind),
                CodeReference.Assembly => SpanKind.Plain,
                _ => throw new UnreachableException(),
            };
    }

    private static SpanKind GetSpanKind(EntityKind kind)
    {
        return kind switch
            {
                EntityKind.Type => SpanKind.Type,
                EntityKind.Method => SpanKind.Method,
                EntityKind.Field => SpanKind.Field,
                EntityKind.Property => SpanKind.Property,
                EntityKind.Event => SpanKind.Event,
                _ => throw new UnreachableException(),
            };
    }
}
