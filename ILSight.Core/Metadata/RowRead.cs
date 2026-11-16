namespace ILSight.Core.Metadata;

/// <summary>A read of <paramref name="row"/> through the library: one of its fields, or the facts a column or its details need.</summary>
public delegate T RowRead<in TRow, out T>(TRow row);
