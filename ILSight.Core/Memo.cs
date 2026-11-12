using System.Diagnostics.CodeAnalysis;

namespace ILSight.Core;

/// <summary>What was worked out, kept with the key it was worked out for. Immutable, so a change is a new
/// one assigned over the old, which replaces both at once, and a copy can't lose anything: a
/// <c>readonly</c> field holding one can't be assigned at all. Allocates nothing. One made by
/// <c>default</c> holds nothing. How keys compare is <typeparamref name="TKey"/>'s own <c>Equals</c>, so a
/// tuple compares each of its parts in turn; wrap what must compare by reference in a
/// <see cref="ByReference{T}"/>, as a record otherwise compares by value.</summary>
public readonly struct Memo<TKey, TValue> where TKey : IEquatable<TKey>
{
    private readonly bool _isHeld;
    private readonly TKey _key;
    private readonly TValue _value;

    public Memo(TKey key, TValue value)
    {
        _isHeld = true;
        _key = key;
        _value = value;
    }

    /// <summary>What is kept, whichever key it was for; the default while nothing is.</summary>
    public TValue? Last => _value;

    /// <summary>Gives what is kept when it was worked out for <paramref name="key"/>.</summary>
    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_isHeld && EqualityComparer<TKey>.Default.Equals(_key, key))
        {
            value = _value;
            return true;
        }
        value = default;
        return false;
    }
}
