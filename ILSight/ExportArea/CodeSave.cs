using System.Diagnostics;
using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>What saving a node to a file the user picked writes: a resource's bytes as they are, straight away, or the
/// node's code as one file, an export the exports panel queues.</summary>
public abstract record CodeSave
{
    private CodeSave()
    {
    }

    /// <summary>The bytes of the resource <see cref="Node"/> stands for, written to <see cref="File"/> as they are.</summary>
    public sealed record ResourceBytes : CodeSave
    {
        public required BrowseNode Node { get; init; }

        public required string File { get; init; }
    }

    /// <summary>The node's code, written as <see cref="Work"/> says.</summary>
    public sealed record Code : CodeSave
    {
        public required ExportWork.CodeFile Work { get; init; }
    }

    public TResult Match<TResult>(Func<ResourceBytes, TResult> resourceBytes, Func<Code, TResult> code)
    {
        return this switch
        {
            ResourceBytes r => resourceBytes(r),
            Code c => code(c),
            _ => throw new UnreachableException($"CodeSave.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, ResourceBytes> resourceBytes, Action<TState, Code> code)
    {
        switch (this)
        {
            case ResourceBytes r:
                resourceBytes(state, r);
                return;
            case Code c:
                code(state, c);
                return;
            default:
                throw new UnreachableException($"CodeSave.Switch does not handle {GetType().Name}.");
        }
    }
}
