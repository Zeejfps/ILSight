// Adapted from ILSpy's CSharpILMixedLanguage (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using SequencePoint = ICSharpCode.Decompiler.DebugInfo.SequencePoint;

namespace ILSight.Core;

/// <summary>
/// Disassembles a method's body with the C# it decompiles to written above the instructions: each
/// statement's lines before the instruction it starts at, as comments. Written to a
/// <see cref="DocumentBuilder"/>, the statement keeps C#'s colors and the rest of its lines are dimmed.
/// A method whose C# cannot be decompiled says so above its first instruction, and its exception goes to the
/// document's part failures when the output is a document. The app's code the disassembler calls back (CD4): its own
/// work, the C# document's builder among it, marks the output's <see cref="CallbackFailure"/> when it throws, which the
/// catch around the method's C# and the one around the whole write both read.
/// </summary>
internal sealed class MethodBodyWithCSharp : MethodBodyDisassembler
{
    private const string NoCSharp = "(no C# code)";
    private const string CommentPrefix = "// ";

    private readonly ModuleReading _reading;
    private readonly LoadedModule _assembly;
    private readonly DecompilerTypeSystem _typeSystem;
    private readonly DecompilerSettings _settings;
    private readonly CancellationToken _cancellationToken;
    private readonly DocumentBuilder? _document;
    private readonly CallbackFailure _callbacks;
    private List<SequencePoint> _points = [];
    private DecompiledDocument? _csharp;
    private string? _failure;
    private int _next;

    /// <param name="callbacks">What the app's code the disassembler calls back marks when it throws: the read's, which
    /// the catch around the whole write reads (the builder's, or a saved file's own).</param>
    /// <param name="reading">The reading of the unit writing.</param>
    /// <param name="typeSystem">The type system of <paramref name="assembly"/>, as the unit writing read it.</param>
    public MethodBodyWithCSharp(ITextOutput output, CallbackFailure callbacks, ModuleReading reading, LoadedModule assembly, DecompilerTypeSystem typeSystem,
        DecompilerSettings settings, CancellationToken cancellationToken)
        : base(output, cancellationToken)
    {
        _reading = reading;
        _assembly = assembly;
        _typeSystem = typeSystem;
        _settings = settings;
        _cancellationToken = cancellationToken;
        _document = output as DocumentBuilder;
        _callbacks = callbacks;
    }

    public override void Disassemble(MetadataFile module, MethodDefinitionHandle handle)
    {
        try
        {
            DecompileToCSharp(handle);
            base.Disassemble(module, handle);
        }
        finally
        {
            Forget();
        }
    }

    private void DecompileToCSharp(MethodDefinitionHandle handle)
    {
        var builder = new DocumentBuilder { Language = DecompilationLanguage.CSharp, Assembly = _assembly, TypeSystem = _typeSystem, Callbacks = _callbacks };
        var points = TryWriteCSharp(handle, builder);
        if (points is null)
        {
            return;
        }

        _callbacks.Run((Body: this, builder, points, handle), static s => s.Body.Keep(s.builder, s.points, s.handle));
    }

    private void Keep(DocumentBuilder builder, Dictionary<ILFunction, List<SequencePoint>> points, MethodDefinitionHandle handle)
    {
        _csharp = builder.BuildDocument("");
        _points = FindPoints(points, handle);
    }

    private Dictionary<ILFunction, List<SequencePoint>>? TryWriteCSharp(MethodDefinitionHandle handle, DocumentBuilder builder)
    {
        try
        {
            var decompiler = NodeDecompiler.CreateCSharpDecompiler(_reading, _assembly, _typeSystem, _settings, _callbacks, _cancellationToken);
            var syntaxTree = decompiler.Decompile(handle);
            NodeDecompiler.WriteLocatedSyntaxTree(syntaxTree, builder, _settings);
            return decompiler.CreateSequencePoints(syntaxTree);
        }
        catch (Exception e) when (e is not OperationCanceledException && !builder.Failed)
        {
            _failure = $"(C# could not be decompiled: {OneLineText.Message(e.Message)})";
            _document?.NotePartFailure(e);
            return null;
        }
    }

    // The IL functions and their methods' tokens are the decompiler's, in memory: nothing of the user's data is read in
    // the count (CF9).
    private static List<SequencePoint> FindPoints(Dictionary<ILFunction, List<SequencePoint>> points, MethodDefinitionHandle handle)
    {
        foreach (var (function, list) in points)
        {
            // An async method or iterator compiles to its state machine's MoveNext.
            var method = function.MoveNextMethod ?? function.Method;
            if (method is not null && method.MetadataToken == handle)
            {
                return list;
            }
        }
        return [];
    }

    private void Forget()
    {
        _points = [];
        _csharp = null;
        _failure = null;
        _next = 0;
    }

    protected override void WriteInstruction(ITextOutput output, MetadataFile metadataFile, MethodDefinitionHandle methodHandle, ref BlobReader blob,
        int methodRva)
    {
        _callbacks.Run((Body: this, output, blob.Offset), static s => s.Body.WriteCSharpAt(s.output, s.Offset));
        base.WriteInstruction(output, metadataFile, methodHandle, ref blob, methodRva);
    }

    private void WriteCSharpAt(ITextOutput output, int offset)
    {
        WriteFailureAt(output, offset);
        var csharp = _csharp;
        if (csharp is null)
        {
            return;
        }

        // The instructions come in order, so the points before this one are passed by.
        while (_next < _points.Count && _points[_next].Offset < offset)
        {
            _next++;
        }

        while (_next < _points.Count && _points[_next].Offset == offset)
        {
            WriteStatement(output, csharp, _points[_next]);
            _next++;
        }
    }

    private void WriteFailureAt(ITextOutput output, int offset)
    {
        if (_failure is null)
        {
            return;
        }

        if (offset != 0)
        {
            return;
        }

        WriteComment(output, _failure);
    }

    private void WriteStatement(ITextOutput output, DecompiledDocument csharp, SequencePoint point)
    {
        if (point.IsHidden)
        {
            WriteComment(output, NoCSharp);
            return;
        }
        var lines = csharp.Lines;
        var indent = IndentOf(lines[point.StartLine - 1].Text);
        for (var line = point.StartLine; line <= point.EndLine; line++)
        {
            WriteStatementLine(output, point, lines[line - 1], line, indent);
        }
    }

    private static void WriteStatementLine(ITextOutput output, SequencePoint point, DocumentLine line, int number, int indent)
    {
        var text = line.Text;
        var from = Math.Min(indent, IndentOf(text));
        var start = StartColumnOn(point, number, text);
        var end = EndColumnOn(point, number, text);
        if (output is DocumentBuilder builder)
        {
            builder.WriteSourceLine(line, from, start, end);
            return;
        }
        WriteComment(output, text.Substring(from));
    }

    private static int StartColumnOn(SequencePoint point, int number, string text)
    {
        if (number != point.StartLine)
        {
            return 0;
        }

        // A sequence point counts columns from 1.
        return Math.Clamp(point.StartColumn - 1, 0, text.Length);
    }

    private static int EndColumnOn(SequencePoint point, int number, string text)
    {
        if (number != point.EndLine)
        {
            return text.Length;
        }

        // A sequence point counts columns from 1.
        return Math.Clamp(point.EndColumn - 1, 0, text.Length);
    }

    private static int IndentOf(string text)
    {
        var indent = 0;
        while (indent < text.Length && char.IsWhiteSpace(text[indent]))
        {
            indent++;
        }

        return indent;
    }

    // Handed to the output: the app's builder (its own count), or the library's plain text writer for a saved file, which
    // only appends the app's text in memory.
    private static void WriteComment(ITextOutput output, string text)
    {
        output.WriteLine(CommentPrefix + text);
    }
}
