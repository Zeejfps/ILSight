using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Colors what the decompiler's output visitor writes, writing into a <see cref="DocumentBuilder"/>: the app's
/// code the library calls back, so its own work (the node stack, the statements it marks, how it classifies a token)
/// runs through the builder's <see cref="DocumentBuilder.Callbacks"/> (CD4); what it hands on to the library's writer
/// (<c>base</c>) is the library's, and so is what it reads of the library's objects (CF9): the nodes, their annotations
/// and symbols are the decompiler's, held in memory, so reading them in the count reads nothing of the user's data;
/// a method's reference, which is read of the type system, the builder reads before its count.</summary>
internal sealed class HighlightingTokenWriter : DecoratingTokenWriter
{
    private readonly DocumentBuilder _builder;
    private readonly Stack<AstNode> _nodes = new();

    public HighlightingTokenWriter(TokenWriter decorated, DocumentBuilder builder) : base(decorated)
    {
        _builder = builder;
    }

    public override void StartNode(AstNode node)
    {
        _builder.Callbacks.Run((Writer: this, node), static s => s.Writer._nodes.Push(s.node));
        base.StartNode(node);
        _builder.Callbacks.Run((Writer: this, node), static s => s.Writer.ListStatements(s.node));
        // Marked outside this writer's count: the builder reads each method's reference of the library before its own
        // count (CF9).
        foreach (var (method, offset) in _statements)
        {
            _builder.MarkStatement(method, offset);
        }
    }

    private readonly List<(IMethod Method, int Offset)> _statements = [];

    // The node's annotations and the IL tree are the decompiler's, built and held in memory: nothing of the user's data
    // is read here.
    private void ListStatements(AstNode node)
    {
        _statements.Clear();
        foreach (var instruction in node.Annotations.OfType<ILInstruction>())
        {
            if (instruction.ILRangeIsEmpty || instruction.Parent is null || instruction is BlockContainer or Block)
            {
                continue;
            }

            var function = instruction.Parent.Ancestors.OfType<ILFunction>().FirstOrDefault();
            if (function is not null && function.Kind == ILFunctionKind.TopLevelFunction && function.Method is not null)
            {
                _statements.Add((function.Method, instruction.StartILOffset));
            }
        }
    }

    public override void EndNode(AstNode node)
    {
        base.EndNode(node);
        _builder.Callbacks.Run(_nodes, static nodes => nodes.Pop());
    }

    public override void WriteKeyword(string keyword)
    {
        using var span = StartSpan(SpanKind.Keyword);
        base.WriteKeyword(keyword);
    }

    public override void WritePrimitiveType(string type)
    {
        using var span = StartSpan(SpanKind.Keyword);
        base.WritePrimitiveType(type);
    }

    public override void WritePrimitiveValue(object? value, LiteralFormat format = LiteralFormat.None)
    {
        using var span = StartSpan(KindOfValue(value));
        base.WritePrimitiveValue(value, format);
    }

    private static SpanKind KindOfValue(object? value)
    {
        return value switch
        {
            null or bool => SpanKind.Keyword,
            string or char => SpanKind.String,
            _ => SpanKind.Number,
        };
    }

    public override void WriteInterpolatedText(string text)
    {
        using var span = StartSpan(SpanKind.String);
        base.WriteInterpolatedText(text);
    }

    public override void WriteToken(string token)
    {
        using var span = StartSpan(_builder.Callbacks.Run((Writer: this, token), static s => s.Writer.KindOfToken(s.token)));
        base.WriteToken(token);
    }

    private SpanKind KindOfToken(string token)
    {
        return IsStringDelimiter(token) ? SpanKind.String : SpanKind.Plain;
    }

    public override void WriteComment(CommentType commentType, string content)
    {
        using var span = StartSpan(SpanKind.Comment);
        base.WriteComment(commentType, content);
    }

    public override void WritePreProcessorDirective(PreProcessorDirectiveType type, string? argument)
    {
        using var span = StartSpan(SpanKind.Preprocessor);
        base.WritePreProcessorDirective(type, argument);
    }

    public override void WriteIdentifier(Identifier identifier)
    {
        using var span = StartSpan(_builder.Callbacks.Run((Writer: this, identifier), static s => s.Writer.Classify(s.identifier)));
        base.WriteIdentifier(identifier);
    }

    private SpanScope StartSpan(SpanKind? kind)
    {
        return _builder.StartSpan(kind);
    }

    private bool IsStringDelimiter(string token)
    {
        return token.StartsWith('$') || (token == "\"" && _nodes.TryPeek(out var node) && node is InterpolatedStringExpression);
    }

    // Reads the node stack (the writer's), the AST and its annotations (a symbol, a resolve result), all in memory: a
    // symbol's kind (a constructor, a destructor) is set when the type system makes it.
    private SpanKind? Classify(Identifier identifier)
    {
        if (!_nodes.TryPeek(out var node))
        {
            return null;
        }

        if (node is Identifier)
        {
            node = node.Parent;
        }

        if (node is null)
        {
            return null;
        }

        switch (node.GetSymbol())
        {
            case IMethod { IsConstructor: true } or IMethod { IsDestructor: true }:
                return SpanKind.Type;
            case IMethod:
                return SpanKind.Method;
            case IField:
                return SpanKind.Field;
            case IProperty:
                return SpanKind.Property;
            case IEvent:
                return SpanKind.Event;
            case IParameter:
                return SpanKind.Parameter;
            case IVariable:
                return SpanKind.Local;
            case INamespace:
                return SpanKind.Namespace;
            case IType:
                return SpanKind.Type;
        }

        switch (node.GetResolveResult())
        {
            case NamespaceResolveResult:
                return SpanKind.Namespace;
            case ILVariableResolveResult { Variable.Kind: VariableKind.Parameter }:
                return SpanKind.Parameter;
            case ILVariableResolveResult:
                return SpanKind.Local;
            case TypeResolveResult:
                return SpanKind.Type;
        }

        if (IsConstrainedTypeParameter(node))
        {
            return SpanKind.Type;
        }

        for (var ancestor = node; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is UsingDeclaration or NamespaceDeclaration)
            {
                return SpanKind.Namespace;
            }

            if (ancestor is EntityDeclaration or Statement)
            {
                break;
            }
        }

        return node switch
        {
            TypeParameterDeclaration or TypeDeclaration or DelegateDeclaration => SpanKind.Type,
            ParameterDeclaration => SpanKind.Parameter,
            SingleVariableDesignation or CatchClause or ForeachStatement => SpanKind.Local,
            _ => null,
        };
    }

    // The decompiler writes the type parameter a where clause is about without a symbol.
    private static bool IsConstrainedTypeParameter(AstNode node)
    {
        if (node.Parent is not Constraint constraint)
        {
            return false;
        }

        return constraint.TypeParameter == node;
    }
}
