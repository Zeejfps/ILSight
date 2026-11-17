using System.Runtime.ExceptionServices;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>What reading a module's type system came to: the type system, or what the library threw on the module,
/// caught where it was thrown.</summary>
public union TypeSystemRead(TypeSystemRead.Read, TypeSystemRead.Unreadable, TypeSystemRead.Closed)
{
    public sealed record Read
    {
        public required DecompilerTypeSystem TypeSystem { get; init; }
    }

    public sealed record Unreadable
    {
        public required ExceptionDispatchInfo Thrown { get; init; }
    }

    /// <summary>The module was released (closed, and nothing read it) before the unit's reading began: nothing was
    /// read.</summary>
    public sealed record Closed
    {
        public required LoadedModule Module { get; init; }
    }
}
