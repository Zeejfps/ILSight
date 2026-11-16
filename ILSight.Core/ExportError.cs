namespace ILSight.Core;

/// <summary>Something the decompiler could not handle: what it was doing, and the full error.</summary>
public sealed record ExportError
{
    /// <summary>What was thrown, so its record has the diagnostic (CF3).</summary>
    public required Exception Error { get; init; }

    public string Message => Error.Message;

    /// <summary>The exception's type, message and stack, as the technical details.</summary>
    public string Details => Error.ToString();
}
