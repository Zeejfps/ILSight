using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;

namespace ILSight.Core;

/// <summary>Notes the column each parameter of the declaration starts at; parameters of types
/// inside it, such as a delegate's, are not the declaration's own. The app's code the library calls back: its own work
/// runs through the builder's <see cref="DocumentBuilder.Callbacks"/> (CD4).</summary>
internal sealed class ParameterStarts(TokenWriter decorated, DocumentBuilder output) : DecoratingTokenWriter(decorated)
{
    private readonly List<int> _starts = new();

    public IReadOnlyList<int> Starts => _starts;

    // Whether the node is a declaration's parameter is read of the decompiler's AST before the count (CF9, CJ9); noting
    // where it starts is the counted work.
    public override void StartNode(AstNode node)
    {
        if (IsDeclaredParameter(node))
        {
            output.Callbacks.Run(this, static starts => starts.NoteStart());
        }

        base.StartNode(node);
    }

    private void NoteStart()
    {
        _starts.Add(output.Column);
    }

    private static bool IsDeclaredParameter(AstNode node)
    {
        return node is ParameterDeclaration && node.Parent is EntityDeclaration;
    }
}
