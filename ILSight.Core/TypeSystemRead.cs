using System.Diagnostics;
using System.Runtime.ExceptionServices;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>What reading a module's type system came to: the type system, or what the library threw on the module,
/// caught where it was thrown.</summary>
public abstract record TypeSystemRead
{
    private TypeSystemRead()
    {
    }

    public sealed record Read : TypeSystemRead
    {
        public required DecompilerTypeSystem TypeSystem { get; init; }
    }

    public sealed record Unreadable : TypeSystemRead
    {
        public required ExceptionDispatchInfo Thrown { get; init; }
    }

    public TResult Match<TResult>(Func<Read, TResult> read, Func<Unreadable, TResult> unreadable)
    {
        return this switch
        {
            Read r => read(r),
            Unreadable u => unreadable(u),
            _ => throw new UnreachableException($"TypeSystemRead.Match does not handle {GetType().Name}."),
        };
    }
}
