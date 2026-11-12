using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>A top-level type an assembly forwards to another: its exported type row, its namespace,
/// its name as the tree shows it (<c>Dictionary&lt;,&gt;</c>), its name without type parameters and how
/// many it has, which it sorts by, and the assembly it is forwarded to.</summary>
internal sealed record ForwardedType(ExportedTypeHandle Handle, string Namespace, string Label, string SortName, int Arity, string Destination);
