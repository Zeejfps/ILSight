using System.Collections;

namespace ILSight.Core;

/// <summary>The latest items added, oldest first, the oldest dropped once there are more than
/// <see cref="Capacity"/>, which the list can't go past. <see cref="Added"/> counts every item ever added,
/// and clearing leaves it, so it tells of something new even when the count stays the same.</summary>
public sealed class BoundedList<T> : IReadOnlyList<T>
{
    private readonly List<T> _items = new();

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not above 0.</exception>
    public BoundedList(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Added { get; private set; }

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    public void Add(T item)
    {
        _items.Add(item);
        Added++;
        if (_items.Count > Capacity)
        {
            _items.RemoveAt(0);
        }
    }

    public void Clear()
    {
        _items.Clear();
    }

    public List<T>.Enumerator GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _items.GetEnumerator();
    }
}
