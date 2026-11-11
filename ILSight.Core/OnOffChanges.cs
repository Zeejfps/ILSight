using System.Collections;
using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>What <see cref="OnOffChanges{T}"/> asks of a choice: the name that orders it, and what it is
/// until it is changed.</summary>
public interface IOnOffChoice
{
    string Name { get; }

    bool DefaultValue { get; }
}

/// <summary>On/off choices set away from their defaults, in the order of their names, and what each is set to.
/// It holds no others: a choice set back to its default goes, and only this type writes, so that however it
/// was come by it holds only what differs. <c>default</c> holds nothing, as a fresh one does.</summary>
public readonly struct OnOffChanges<T> : IEnumerable<KeyValuePair<T, bool>> where T : IOnOffChoice
{
    private static readonly IComparer<T> ByName = Comparer<T>.Create(static (a, b) => string.CompareOrdinal(a.Name, b.Name));

    private static readonly ImmutableSortedDictionary<T, bool> Nothing = ImmutableSortedDictionary.Create<T, bool>(ByName);

    private readonly ImmutableSortedDictionary<T, bool>? _changes;

    private OnOffChanges(ImmutableSortedDictionary<T, bool> changes)
    {
        _changes = changes;
    }

    private ImmutableSortedDictionary<T, bool> Changes => _changes ?? Nothing;

    public int Count => Changes.Count;

    /// <summary>Whether <paramref name="choice"/> is set away from its default.</summary>
    public bool Contains(T choice)
    {
        return Changes.ContainsKey(choice);
    }

    public bool TryGetValue(T choice, out bool value)
    {
        return Changes.TryGetValue(choice, out value);
    }

    /// <summary>These changes with each of <paramref name="choices"/> set to <paramref name="value"/>.</summary>
    public OnOffChanges<T> With(IEnumerable<T> choices, bool value)
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
        return new OnOffChanges<T>(changes.ToImmutable());
    }

    /// <summary>These changes with each of <paramref name="choices"/> back at its default.</summary>
    public OnOffChanges<T> Without(IEnumerable<T> choices)
    {
        var changes = Changes.ToBuilder();
        foreach (var choice in choices)
        {
            changes.Remove(choice);
        }

        return new OnOffChanges<T>(changes.ToImmutable());
    }

    public ImmutableSortedDictionary<T, bool>.Enumerator GetEnumerator()
    {
        return Changes.GetEnumerator();
    }

    IEnumerator<KeyValuePair<T, bool>> IEnumerable<KeyValuePair<T, bool>>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
