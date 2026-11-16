using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Colors what the decompiler's output visitor writes, writing into a <see cref="DocumentBuilder"/>: the app's
/// code the library calls back, so its own work (the node stack, how it classifies a token) runs through the builder's
/// <see cref="DocumentBuilder.Callbacks"/> (CD4); what it hands on to the library's writer (<c>base</c>) is the
/// library's, and so is what it reads of the library's objects (CF9, CJ9): the statements a node's annotations list, an
/// identifier's parent, symbol and resolve result, read before the count or outside it; a node compared by identity or
/// tested for its type reads nothing of it.</summary>
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
        // Read of the node's annotations and the IL tree outside the count, and marked by the builder, which reads each
        // method's reference of the library before its own count (CF9, CJ9).
        var statements = ReadStatements(node);
        if (statements is null)
        {
            return;
        }

        foreach (var (method, offset) in statements)
        {
            _builder.MarkStatement(method, offset);
        }
    }

    // The statements the node's annotations begin: each top-level method and the IL offset; null for none.
    private static List<(IMethod Method, int Offset)>? ReadStatements(AstNode node)
    {
        List<(IMethod Method, int Offset)>? statements = null;
        foreach (var instruction in node.Annotations.OfType<ILInstruction>())
        {
            if (instruction.ILRangeIsEmpty || instruction.Parent is null || instruction is BlockContainer or Block)
            {
                continue;
            }

            var function = instruction.Parent.Ancestors.OfType<ILFunction>().FirstOrDefault();
            if (function is not null && function.Kind == ILFunctionKind.TopLevelFunction && function.Method is not null)
            {
                statements ??= [];
                statements.Add((function.Method, instruction.StartILOffset));
            }
        }
        return statements;
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
        var kind = _builder.Callbacks.Run((Writer: this, token), static s => s.Writer.KindOfToken(s.token));
        using var span = StartSpan(kind);
        base.WriteToken(token);
    }

    // The node on top of the writer's stack is tested for its type, which reads nothing of it.
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

    // The node on top of the writer's stack is taken in the count; what it names (its parent, symbol and resolve
    // result) is read of the decompiler's AST outside it (CF9, CJ9).
    public override void WriteIdentifier(Identifier identifier)
    {
        var node = _builder.Callbacks.Run(_nodes, static nodes => nodes.TryPeek(out var top) ? top : null);
        var kind = node is null ? null : ReadKind(node);
        using var span = StartSpan(kind);
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

    // What the identifier written under node names, read of the AST and its annotations (a symbol, a resolve result).
    private static SpanKind? ReadKind(AstNode? node)
    {
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
