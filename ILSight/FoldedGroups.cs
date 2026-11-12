namespace ILSight;

/// <summary>The groups folded in a panel, by key. Every change bumps <see cref="Revision"/>.</summary>
public sealed class FoldedGroups
{
    public required HashSet<string> Folded { get; init; }

    public int Revision { get; set; }

    public int Count => Folded.Count;
}
