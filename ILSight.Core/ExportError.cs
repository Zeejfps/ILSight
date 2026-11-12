namespace ILSight.Core;

/// <summary>Something the decompiler could not handle: what it was doing, and the full error.</summary>
public sealed record ExportError(string Message, string Details);
