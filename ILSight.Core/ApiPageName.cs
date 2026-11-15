namespace ILSight.Core;

/// <summary>What names a page of Microsoft's .NET API reference, as read from the library's objects
/// (<see cref="MicrosoftDocs.ReadPageName"/>): the reflection name of a type, and of a member of it, its name; null for
/// the type's own page.</summary>
internal sealed record ApiPageName
{
    public required string Type { get; init; }

    public required string? Member { get; init; }
}
