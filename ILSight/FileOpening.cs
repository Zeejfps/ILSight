using ILSight.Core;
using ILSight.UserState;
using ILSight.AssemblyHandling;

namespace ILSight;

/// <summary>Files the user asked to open, as a request <see cref="UserIntent.BeginOpening"/> began, for
/// <see cref="AssemblyOpening"/> to open: the files, and the intent the request took, which a newer request makes
/// stale (the last file opened is then not shown). Dropped, the files are never opened.</summary>
[MustUse]
public sealed class FileOpening
{
    public required IReadOnlyList<AssemblyPath> Paths { get; init; }

    public required int Intent { get; init; }
}
