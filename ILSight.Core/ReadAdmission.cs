namespace ILSight.Core;

/// <summary>What an <see cref="AssemblyList"/> answered an assembly read for it: the assembly to use from now on, the
/// one read or one it already held; and the read when nothing keeps it (the list was cleared meanwhile, or another
/// read of its file was kept first), for the I/O side to release. Dropped, an unkept read is never released.</summary>
[MustUse]
internal sealed record ReadAdmission<T> where T : LoadedAssembly
{
    public required T Use { get; init; }

    public required T? Unkept { get; init; }
}
