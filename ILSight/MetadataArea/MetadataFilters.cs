using ILSight.Core.Metadata;

namespace ILSight.MetadataArea;

/// <summary>The flags filters of the metadata grids: one per kind of row and column, shared by every
/// grid of that kind in any assembly, as ILSpy's are, and kept between runs.</summary>
public sealed class MetadataFilters
{
    private readonly Dictionary<string, FilterState> _states = new();

    /// <summary>What filters something, as read and as last listed, by the kind of row and column.</summary>
    public required Dictionary<string, SavedFilterState> Saved { private get; init; }

    /// <summary>Changes whenever a filter does.</summary>
    public int Revision => _states.Values.Sum(static s => s.Revision);

    /// <summary>Filters starting from a copy of <paramref name="saved"/>, what the settings kept.</summary>
    public static MetadataFilters From(Dictionary<string, SavedFilterState>? saved)
    {
        var copied = saved is not null ? new Dictionary<string, SavedFilterState>(saved) : new Dictionary<string, SavedFilterState>();
        return new MetadataFilters { Saved = copied };
    }

    /// <summary>The filter of <paramref name="column"/>, a column of flags, as last left.</summary>
    public FilterState GetFilter(MetadataGrid grid, int column)
    {
        var key = GetKey(grid, column);
        if (_states.TryGetValue(key, out var state))
        {
            return state;
        }

        var schema = grid.Columns[column].Flags;
        if (schema == null)
        {
            throw new ArgumentException($"{key} is not a flags column.", nameof(column));
        }

        state = FilterState.From(schema);
        if (Saved.TryGetValue(key, out var saved))
        {
            state.Restore(saved);
        }

        _states.Add(key, state);
        return state;
    }

    private static string GetKey(MetadataGrid grid, int column)
    {
        return $"{grid.Key}/{grid.Columns[column].Name}";
    }

    /// <summary>Every filter that filters something, to save.</summary>
    public Dictionary<string, SavedFilterState> ListSaved()
    {
        foreach (var (key, state) in _states)
        {
            var saved = FilterStatePresenter.SaveFilter(state);
            if (saved != null)
            {
                Saved[key] = saved;
            }
            else
            {
                Saved.Remove(key);
            }
        }
        return new Dictionary<string, SavedFilterState>(Saved);
    }
}
