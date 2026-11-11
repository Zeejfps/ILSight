using System.Collections;
using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>On/off choices set away from their defaults, in the order of their names, and what each is set to.
/// It holds no others: a choice set back to its default goes, and only this type writes, so that however it
/// was come by it holds only what differs. <c>default</c> holds nothing, as a fresh one does.</summary>
public readonly struct OnOffChanges : IEnumerable<KeyValuePair<DecompilerToggle, bool>>
{
    private static readonly IComparer<DecompilerToggle> ByName = Comparer<DecompilerToggle>.Create(static (a, b) => string.CompareOrdinal(a.Name, b.Name));

    private static readonly ImmutableSortedDictionary<DecompilerToggle, bool> Nothing = ImmutableSortedDictionary.Create<DecompilerToggle, bool>(ByName);

    private readonly ImmutableSortedDictionary<DecompilerToggle, bool>? _changes;

    private OnOffChanges(ImmutableSortedDictionary<DecompilerToggle, bool> changes)
    {
        _changes = changes;
    }

    private ImmutableSortedDictionary<DecompilerToggle, bool> Changes => _changes ?? Nothing;

    public int Count => Changes.Count;

    /// <summary>Whether <paramref name="choice"/> is set away from its default.</summary>
    public bool Contains(DecompilerToggle choice)
    {
        return Changes.ContainsKey(choice);
    }

    public bool TryGetValue(DecompilerToggle choice, out bool value)
    {
        return Changes.TryGetValue(choice, out value);
    }

    /// <summary>These changes with each of <paramref name="choices"/> set to <paramref name="value"/>.</summary>
    public OnOffChanges With(IEnumerable<DecompilerToggle> choices, bool value)
    {
        var changes = Changes.ToBuilder();
        foreach (var choice in choices)
        {
            if (value == choice.DefaultValue)
            {
                changes.Remove(choice);
            }
            else
            {
                changes[choice] = value;
            }
        }
        return new OnOffChanges(changes.ToImmutable());
    }

    /// <summary>These changes with each of <paramref name="choices"/> back at its default.</summary>
    public OnOffChanges Without(IEnumerable<DecompilerToggle> choices)
    {
        var changes = Changes.ToBuilder();
        foreach (var choice in choices)
        {
            changes.Remove(choice);
        }

        return new OnOffChanges(changes.ToImmutable());
    }

    public ImmutableSortedDictionary<DecompilerToggle, bool>.Enumerator GetEnumerator()
    {
        return Changes.GetEnumerator();
    }

    IEnumerator<KeyValuePair<DecompilerToggle, bool>> IEnumerable<KeyValuePair<DecompilerToggle, bool>>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
