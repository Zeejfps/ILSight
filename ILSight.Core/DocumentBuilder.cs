using System.Reflection.Metadata;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Disassembler;
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
}
