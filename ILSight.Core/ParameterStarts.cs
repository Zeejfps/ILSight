using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;

namespace ILSight.Core;

/// <summary>Notes the column each parameter of the declaration starts at; parameters of types
/// inside it, such as a delegate's, are not the declaration's own.</summary>
internal sealed class ParameterStarts(TokenWriter decorated, DocumentBuilder output) : DecoratingTokenWriter(decorated)
{
    private readonly List<int> _starts = new();

    public IReadOnlyList<int> Starts => _starts;

    public override void StartNode(AstNode node)
    {
        if (IsDeclaredParameter(node))
        {
            _starts.Add(output.Column);
        }

        base.StartNode(node);
    }

    private static bool IsDeclaredParameter(AstNode node)
    {
        return node is ParameterDeclaration && node.Parent is EntityDeclaration;
    }
}
