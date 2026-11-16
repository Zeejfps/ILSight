namespace ILSight.Core.MetadataArea;

/// <summary>The read half of what is shown under a selected row: its facts, read of <paramref name="row"/> through the library, inside the row's catch.</summary>
public delegate object? GridDetailsRead(object row);
