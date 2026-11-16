namespace ILSight.Core.Metadata;

/// <summary>The read half of a cell (its value or its tooltip's facts): what it reads of <paramref name="row"/>, its RID <paramref name="rid"/>, through the library, inside the cell's catch.</summary>
public delegate object? GridCellRead(object row, int rid);
