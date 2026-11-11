using System.Collections;

namespace ILSight.Core;

/// <summary>A list with a place in it, as an undo history has: <see cref="CurrentIndex"/> is always one of
/// the items, or -1 exactly while there are none, as nothing else can move it. Adding drops what was after
/// the current item and makes the new one current, and past <see cref="Capacity"/> the oldest goes.
/// <see cref="Revision"/> counts each change to which items there are or which is current; replacing the
/// current item by one that stands in its place doesn't count.</summary>
public sealed class CursorList<T> : IReadOnlyList<T> where T : class
{
    private readonly List<T> _items = new();

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not above 0.</exception>
    public CursorList(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int CurrentIndex { get; private set; } = -1;

    public int Revision { get; private set; }

    public T? Current => CurrentIndex >= 0 ? _items[CurrentIndex] : null;

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    /// <summary>Drops what is after the current item, adds <paramref name="item"/> and makes it current.</summary>
    public void Add(T item)
    {
        var after = CurrentIndex + 1;
        _items.RemoveRange(after, _items.Count - after);
        _items.Add(item);
        if (_items.Count > Capacity)
        {
            _items.RemoveAt(0);
        }

        CurrentIndex = _items.Count - 1;
        Revision++;
    }

    /// <summary>Removes the item at <paramref name="index"/>. The one before a current item removed becomes
    /// current, or the first left when it was first.</summary>
    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        if (index <= CurrentIndex)
        {
            CurrentIndex--;
        }

        if (CurrentIndex < 0 && _items.Count > 0)
        {
            CurrentIndex = 0;
        }

        Revision++;
    }

    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _items.Clear();
        CurrentIndex = -1;
        Revision++;
    }

    /// <summary>Makes the item at <paramref name="index"/> current; false, changing nothing, when there is
    /// none there or it is current already.</summary>
    public bool TryMoveTo(int index)
    {
        if (index < 0 || index >= _items.Count || index == CurrentIndex)
        {
            return false;
        }

        CurrentIndex = index;
        Revision++;
        return true;
    }

    /// <summary>Puts <paramref name="item"/> in the place of the current item; false, changing nothing, when
    /// there is none.</summary>
    public bool TryReplaceCurrent(T item)
    {
        if (CurrentIndex < 0)
        {
            return false;
        }

        _items[CurrentIndex] = item;
        return true;
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
