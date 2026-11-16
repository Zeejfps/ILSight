namespace ILSight.Core.Metadata;

/// <summary>The read of a schema's rows of <typeparamref name="TRow"/> through the library, which the grid lists in order.</summary>
public delegate IEnumerable<TRow> RowsRead<out TRow>();
