using ILSight.Core;

namespace ILSight;

/// <summary>The reopen of each listed file that counts: starting another for the file makes the one
/// before it stale, and its result is then ignored.</summary>
public sealed class Reopens
{
    /// <summary>The ticket of the reopen that counts, by the file it is of.</summary>
    public Dictionary<AssemblyPath, int> Current { get; } = new();

    public int Last { get; set; }

    public bool IsRunning => Current.Count > 0;

    /// <summary>The paths of the files being reopened.</summary>
    public IReadOnlyCollection<AssemblyPath> Files => Current.Keys;
}
