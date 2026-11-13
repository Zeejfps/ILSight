namespace ILSight;

/// <summary>The groups folded in a panel, by key. Every change bumps <see cref="Revision"/>.</summary>
public sealed class FoldedGroups
{
    private readonly HashSet<string> _folded = [];

    public required IReadOnlySet<string> Folded
    {
        get => _folded;
        init => _folded = new HashSet<string>(value);
    }

    public int Revision { get; private set; }

    public int Count => _folded.Count;

    public bool IsFolded(string group)
    {
        return _folded.Contains(group);
    }

    public void Fold(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (_folded.Add(name))
            {
                Revision++;
            }
        }
    }

    /// <summary>Unfolds <paramref name="group"/> when it is folded, and folds it otherwise.</summary>
    public void Flip(string group)
    {
        if (!_folded.Remove(group))
        {
            _folded.Add(group);
        }

        Revision++;
    }

    public void UnfoldAll()
    {
        if (_folded.Count == 0)
        {
            return;
        }

        _folded.Clear();
        Revision++;
    }

    /// <summary>The folded groups in order, to save.</summary>
    public List<string> ListInOrder()
    {
        return _folded
            .Order()
            .ToList();
    }
}
