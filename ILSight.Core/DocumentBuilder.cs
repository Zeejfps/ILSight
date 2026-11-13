using System.Diagnostics;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Writing a <see cref="DecompiledDocument"/> a token at a time: what the decompiler writes to, through the
/// ITextOutput its methods implement. The line being written (its text, its spans, where they start, whether it is
/// indented, classified), the indentation, the span kind a token writer asked for and the method being written are
/// its own, changed only by its methods, which keep them in step with the lines and spans written so far.</summary>
internal sealed class DocumentBuilder : ITextOutput
{
    private const int MaxSignatureLines = 8;
    private const string DefaultFoldText = "...";
    private const string SourceLinePrefix = "// ";

    public required DecompilationLanguage Language { get; init; }
    private readonly StringBuilder _line = new();
    private readonly List<DocumentSpan> _spans = new();
    private readonly List<(string Text, int First, int Count)> _lines = new();
    private readonly Dictionary<CodeReference.Navigable, int> _definitions = new();
    private readonly Dictionary<int, (CodeReference.Entity Method, int Offset)> _markedStatements = new();
    private readonly List<DocumentSpan> _lexed = new();
    private readonly Dictionary<object, int> _localKeys = new();
    private readonly Stack<OpenFold> _openFolds = new();
    private readonly List<(OpenFold Start, int EndLine)> _folds = new();
    public LoadedModule? Assembly { get; internal init; }

    /// <summary>The type system of <see cref="Assembly"/>, as the unit writing awaited its read before it began; null
    /// when there is none to read with, or the library could not read it.</summary>
    public DecompilerTypeSystem? TypeSystem { get; internal init; }
    private int _lineFirstSpan;
    private int _indentLevel;
    private bool _lineStarted;
    private bool _lineClassified;
    private CodeReference.Entity? _scope;

    /// <summary>What each level of indentation writes.</summary>
    private string _indentation = Settings.Indentation;

    /// <summary>The kind of the span a token writer started (<see cref="StartSpan"/>), which what is written in it takes;
    /// null outside one.</summary>
    private SpanKind? _kind;

    /// <summary>ITextOutput's, which the decompiler may read or set; this app's code indents through
    /// <see cref="IndentAs"/>.</summary>
    string ITextOutput.IndentationString
    {
        get => _indentation;
        set => _indentation = value;
    }

    /// <summary>Where the next text written to the current line starts.</summary>
    internal int Column => _line.Length;

    /// <summary>Where the next text written lands, from line 1 and column 1, as the decompiler records
    /// it in the syntax tree: past the indentation a fresh line is still to get.</summary>
    internal TextLocation Location
    {
        get
        {
            var column = _line.Length;
            if (!_lineStarted)
            {
                column = _indentLevel * _indentation.Length;
            }

            return new TextLocation(_lines.Count + 1, column + 1);
        }
    }

    /// <summary>Indents each level as <paramref name="formatting"/> says, for C# written in it.</summary>
    public void IndentAs(CSharpFormattingOptions formatting)
    {
        _indentation = formatting.IndentationString;
    }

    /// <summary>Starts a span of <paramref name="kind"/>, which what is written takes until the returned scope ends it;
    /// none when null.</summary>
    public SpanScope StartSpan(SpanKind? kind)
    {
        _kind = kind;
        return new SpanScope { Builder = this };
    }

    /// <summary>Ends the span <see cref="StartSpan"/> started.</summary>
    public void EndSpan()
    {
        _kind = null;
    }


    public DecompiledDocument BuildDocument(string title)
    {
        return BuildDocument(title, null);
    }

    /// <summary>The document written so far, describing how decompiling failed with <paramref name="error"/>.</summary>
    public DecompiledDocument BuildFailure(string title, Exception error)
    {
        return BuildDocument(title, error);
    }

    private DecompiledDocument BuildDocument(string title, Exception? error)
    {
        if (_line.Length > 0 || _lineFirstSpan < _spans.Count || _lines.Count == 0)
        {
            WriteLine();
        }

        var spans = _spans.ToImmutableArray();
        var lines = ImmutableArray.CreateBuilder<DocumentLine>(_lines.Count);
        foreach (var (text, first, count) in _lines)
        {
            lines.Add(new DocumentLine { Text = text, DocumentSpans = spans, FirstSpan = first, SpanCount = count });
        }

        var built = lines.MoveToImmutable();
        var statements = ListStatements();
        return new DecompiledDocument { Title = title, Language = Language, Error = error, Lines = built, Definitions = _definitions, Statements = statements,
            EntityNames = NameAnchoredEntities(statements), Folds = ListFoldRegions(built) };
    }

    /// <summary>The ILSpy full name of the entity of the module written each anchor names: a
    /// statement's method, a definition's type or member, resolved through the module's type system. None when the
    /// document names nothing of a module, or the type system could not be read.</summary>
    private Dictionary<(AssemblyPath FilePath, int Token), string> NameAnchoredEntities(Dictionary<int, CodeAnchor> statements)
    {
        var names = new Dictionary<(AssemblyPath FilePath, int Token), string>();
        var module = Assembly;
        if (module is null)
        {
            return names;
        }

        var tokens = ListAnchoredTokens(statements, module.FilePath);
        if (tokens.Count == 0)
        {
            return names;
        }

        var typeSystem = TypeSystem;
        if (typeSystem is null)
        {
            return names;
        }

        var main = typeSystem.MainModule;
        foreach (var token in tokens)
        {
            var entity = ResolveEntity(main, token);
            if (entity is not null)
            {
                names[(module.FilePath, token)] = entity.FullName;
            }
        }
        return names;
    }

    private HashSet<int> ListAnchoredTokens(Dictionary<int, CodeAnchor> statements, AssemblyPath filePath)
    {
        var tokens = new HashSet<int>();
        foreach (var anchor in statements.Values)
        {
            if (anchor.FilePath.Equals(filePath))
            {
                tokens.Add(anchor.Token);
            }
        }
        foreach (var reference in _definitions.Keys)
        {
            if (reference is CodeReference.Entity entity && entity.FilePath.Equals(filePath))
            {
                tokens.Add(MetadataTokens.GetToken(entity.Handle));
            }
        }
        return tokens;
    }

    private static IEntity? ResolveEntity(MetadataModule main, int token)
    {
        try
        {
            return main.ResolveEntity(MetadataTokens.EntityHandle(token));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public void Write(string text, SpanKind kind, CodeReference? reference = null, bool isDefinition = false)
    {
        var start = 0;
        while (true)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline;
            var segmentEnd = end > start && text[end - 1] == '\r' ? end - 1 : end;
            Append(text.AsSpan(start, segmentEnd - start), kind, reference, isDefinition);
            if (newline < 0)
            {
                return;
            }

            WriteLine();
            start = newline + 1;
        }
    }

    public void Indent()
    {
        _indentLevel++;
    }

    public void Unindent()
    {
        _indentLevel = Math.Max(0, _indentLevel - 1);
    }

    public void Write(char ch)
    {
        if (ch == '\n')
        {
            WriteLine();
        }
        else if (ch != '\r')
        {
            Append(new ReadOnlySpan<char>(in ch), _kind ?? SpanKind.Plain, null, false);
        }
    }

    public void Write(string text)
    {
        Write(text, _kind ?? SpanKind.Plain);
    }

    public void WriteLine()
    {
        if (IsUnlexedIL())
        {
            LexILLine();
        }

        _lines.Add((_line.ToString(), _lineFirstSpan, _spans.Count - _lineFirstSpan));
        _line.Clear();
        _lineFirstSpan = _spans.Count;
        _lineStarted = false;
        _lineClassified = false;
    }

    private bool IsUnlexedIL()
    {
        if (_lineClassified)
        {
            return false;
        }

        return DecompilationLanguages.IsIL(Language);
    }

    /// <summary>Writes <paramref name="line"/> of C# as a comment line, from column <paramref name="from"/>
    /// on: the statement from <paramref name="start"/> up to <paramref name="end"/> in C#'s colors, the
    /// rest dimmed as a comment. Names of types and members stay links; locals do not, as they are the
    /// C# document's.</summary>
    internal void WriteSourceLine(DocumentLine line, int from, int start, int end)
    {
        Append(SourceLinePrefix, SpanKind.Comment, null, false);
        foreach (var span in line.Spans)
        {
            AppendSourceSpan(line.Text, span, from, start, end);
        }

        _lineClassified = true;
        WriteLine();
    }

    /// <summary>Appends <paramref name="span"/>'s text from column <paramref name="from"/> on: the part
    /// inside the statement as it is, the parts before and after it as a comment.</summary>
    private void AppendSourceSpan(string text, DocumentSpan span, int from, int start, int end)
    {
        var first = Math.Max(span.Start, from);
        var reference = FindLink(span.Reference);
        AppendPart(text, first, Math.Min(span.End, start), SpanKind.Comment, null);
        AppendPart(text, Math.Max(first, start), Math.Min(span.End, end), span.Kind, reference);
        AppendPart(text, Math.Max(first, end), span.End, SpanKind.Comment, null);
    }

    private void AppendPart(string text, int from, int to, SpanKind kind, CodeReference? reference)
    {
        if (to <= from)
        {
            return;
        }

        Append(text.AsSpan(from, to - from), kind, reference, false);
    }

    /// <summary>The reference a copied span keeps: a type's or member's, not a local's or label's.</summary>
    private static CodeReference? FindLink(CodeReference? reference)
    {
        if (reference is CodeReference.Local or CodeReference.Label)
        {
            return null;
        }

        return reference;
    }

    public void WriteReference(OpCodeInfo opCode, bool omitSuffix = false)
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
        Write(name, SpanKind.ILOpcode);
    }

    public void WriteReference(MetadataFile metadata, Handle handle, string text, string protocol = "decompile", bool isDefinition = false)
    {
        var reference = FindReference(metadata, handle, text);
        var kind = _kind ?? (reference is null ? SpanKind.Plain : isDefinition && text.StartsWith('.') ? SpanKind.Keyword : GetSpanKind(reference));
        WriteReferenceSpan(text, kind, reference, isDefinition);
    }

    public void WriteReference(IType type, string text, bool isDefinition = false)
    {
        var definition = type.GetDefinition();
        var reference = definition is not null ? CodeReferences.Find(definition, text) : null;
        WriteReferenceSpan(text, _kind ?? SpanKind.Type, reference, isDefinition);
    }

    public void WriteReference(IMember member, string text, bool isDefinition = false)
    {
        var definition = member.MemberDefinition ?? member;
        var kind = _kind ?? GetSpanKind(CodeReferences.GetKind(definition.SymbolKind));
        WriteReferenceSpan(text, kind, CodeReferences.Find(definition, text), isDefinition);
    }

    public void WriteLocalReference(string text, object reference, bool isDefinition = false, bool isHoverOnly = false)
    {
        var spanKind = reference switch
        {
            int => SpanKind.ILLabel,
            string s when s.StartsWith("param_", StringComparison.Ordinal) => SpanKind.Parameter,
            ILVariable { Kind: VariableKind.Parameter } => SpanKind.Parameter,
            IParameter => SpanKind.Parameter,
            _ => SpanKind.Local,
        };
        if (isDefinition && reference is int offset && _scope != null && _scope.Kind == EntityKind.Method)
        {
            MarkStatement(_scope, offset);
        }

        var scope = reference is ILVariable ? null : _scope;
        if (!_localKeys.TryGetValue(reference, out var key))
        {
            _localKeys.Add(reference, key = _localKeys.Count);
        }

        CodeReference local = reference is int ? new CodeReference.Label { Name = text, Key = key, Scope = scope } : new CodeReference.Local { Name = text, Key = key, Scope = scope };
        Write(text, _kind ?? spanKind, local, isDefinition);
    }

    /// <summary>Marks the line being written as where the statement at <paramref name="ilOffset"/> of
    /// <paramref name="method"/> starts. A line keeps the first method marked on it and its lowest offset.</summary>
    public void MarkStatement(IMethod method, int ilOffset)
    {
        var reference = CodeReferences.Find(method);
        if (reference != null)
        {
            MarkStatement(reference, ilOffset);
        }
    }

    private void MarkStatement(CodeReference.Entity method, int ilOffset)
    {
        var line = _lines.Count;
        if (_markedStatements.TryGetValue(line, out var marked) && (marked.Method != method || marked.Offset <= ilOffset))
        {
            return;
        }

        _markedStatements[line] = (method, ilOffset);
    }

    private Dictionary<int, CodeAnchor> ListStatements()
    {
        var anchors = new Dictionary<int, CodeAnchor>(_markedStatements.Count);
        foreach (var (line, (method, offset)) in _markedStatements)
        {
            anchors[line] = new CodeAnchor
            {
                Kind = CodeAnchorKind.Statement,
                FilePath = method.FilePath,
                Token = MetadataTokens.GetToken(method.Handle),
                ILOffset = offset,
            };
        }

        return anchors;
    }

    public void MarkDefinitionStart()
    {
    }

    public void MarkFoldStart(string collapsedText = DefaultFoldText, bool defaultCollapsed = false, bool isDefinition = false)
    {
        _openFolds.Push(new OpenFold
        {
            Line = _lines.Count,
            Column = _lineStarted ? _line.Length : 0,
            Text = collapsedText,
            DefaultCollapsed = defaultCollapsed,
            IsDefinition = isDefinition,
        });
    }

    public void MarkFoldEnd()
    {
        if (_openFolds.TryPop(out var start))
        {
            _folds.Add((start, _lineStarted ? _lines.Count : _lines.Count - 1));
        }
    }

    private ImmutableArray<FoldRegion> ListFoldRegions(ImmutableArray<DocumentLine> lines)
    {
        if (_folds.Count == 0)
        {
            return ImmutableArray<FoldRegion>.Empty;
        }

        var regions = new List<FoldRegion>(_folds.Count);
        foreach (var (start, endLine) in _folds)
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
            regions.Add(new FoldRegion
            {
                StartLine = header,
                EndLine = endLine,
                CollapsedText = text,
                DefaultCollapsed = start.DefaultCollapsed,
                IsDefinition = start.IsDefinition,
            });
        }
        regions.Sort(static (a, b) => a.StartLine != b.StartLine ? a.StartLine.CompareTo(b.StartLine) : b.EndLine.CompareTo(a.EndLine));

        var nested = ImmutableArray.CreateBuilder<FoldRegion>(regions.Count);
        var open = new Stack<FoldRegion>();
        foreach (var region in regions)
        {
            while (open.TryPeek(out var innermost) && innermost.EndLine < region.StartLine)
            {
                open.Pop();
            }

            if (nested.Count > 0 && nested[^1].StartLine == region.StartLine)
            {
                continue;
            }

            if (open.TryPeek(out var enclosing) && region.EndLine > enclosing.EndLine)
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

    private void WriteReferenceSpan(string text, SpanKind kind, CodeReference? reference, bool isDefinition)
    {
        if (isDefinition && reference is CodeReference.Entity entity && entity.Kind == EntityKind.Method)
        {
            _scope = entity;
        }

        Write(text, kind, reference, isDefinition);
    }

    private void Append(ReadOnlySpan<char> text, SpanKind kind, CodeReference? reference, bool isDefinition)
    {
        if (text.IsEmpty)
        {
            return;
        }

        if (!_lineStarted)
        {
            _lineStarted = true;
            for (var i = 0; i < _indentLevel; i++)
            {
                AppendSpan(_indentation, SpanKind.Plain, null, false);
            }
        }
        if (isDefinition && reference is CodeReference.Navigable navigable)
        {
            _definitions.TryAdd(navigable, _lines.Count);
        }

        AppendSpan(text, kind, reference, isDefinition);
    }

    private void AppendSpan(ReadOnlySpan<char> text, SpanKind kind, CodeReference? reference, bool isDefinition)
    {
        if (text.IsEmpty)
        {
            return;
        }

        var start = _line.Length;
        _line.Append(text);
        if (reference is null && !isDefinition && _spans.Count > _lineFirstSpan)
        {
            var last = _spans[^1];
            if (last.Kind == kind && last.Reference is null && !last.IsDefinition && last.End == start)
            {
                _spans[^1] = last with { Length = last.Length + text.Length };
                return;
            }
        }
        _spans.Add(new DocumentSpan
        {
            Start = start,
            Length = text.Length,
            Kind = kind,
            Reference = reference,
            IsDefinition = isDefinition,
        });
    }

    private void LexILLine()
    {
        var count = _spans.Count - _lineFirstSpan;
        var any = false;
        for (var i = 0; i < count && !any; i++)
        {
            any = _spans[_lineFirstSpan + i] is { Kind: SpanKind.Plain, Reference: null };
        }

        if (!any)
        {
            return;
        }

        _lexed.Clear();
        var text = _line.ToString();
        var inComment = false;
        for (var i = 0; i < count; i++)
        {
            var span = _spans[_lineFirstSpan + i];
            if (span.Kind != SpanKind.Plain || span.Reference is not null)
            {
                _lexed.Add(inComment && span.Reference is null ? span with { Kind = SpanKind.Comment } : span);
                continue;
            }
            ILLexer.Classify(text, span.Start, span.End, ref inComment, _lexed);
        }
        _spans.RemoveRange(_lineFirstSpan, count);
        _spans.AddRange(_lexed);
    }

    private CodeReference.Navigable? FindReference(MetadataFile metadata, Handle handle, string text)
    {
        if (handle.IsNil)
        {
            return null;
        }

        if (handle.Kind is HandleKind.TypeReference or HandleKind.TypeSpecification or HandleKind.MemberReference or HandleKind.MethodSpecification)
        {
            var definition = ResolveDefinition(metadata, (EntityHandle)handle);
            if (definition is not null)
            {
                return CodeReferences.Find(definition, text);
            }
        }
        return CodeReferences.Find(metadata.Metadata, handle, text, AssemblyPath.Of(metadata.FileName));
    }

    private IEntity? ResolveDefinition(MetadataFile metadata, EntityHandle handle)
    {
        var assembly = Assembly;
        var typeSystem = TypeSystem;
        if (assembly is null || typeSystem is null)
        {
            return null;
        }

        var own = LoadedAssemblies.GetFile(assembly);
        if (!ReferenceEquals(metadata, own))
        {
            return null;
        }

        try
        {
            return Definitions.Resolve(typeSystem.MainModule, handle);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
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

    /// <summary>A fold the decompiler started and hasn't ended: where it starts and how it shows folded.</summary>
    private readonly record struct OpenFold
    {
        public required int Line { get; init; }

        public required int Column { get; init; }

        public required string Text { get; init; }

        public required bool DefaultCollapsed { get; init; }

        public required bool IsDefinition { get; init; }
    }
}
