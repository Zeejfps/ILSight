// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

namespace ILSight.Core.Diagrams;

/// <summary>The members a type inherits from one of its base types: its plain ones as mermaid
/// members and its relations, as <see cref="DiagramType"/> has them.</summary>
internal sealed class InheritedMembers
{
    public string? FlatMembers { get; init; }
    public Dictionary<string, string>? HasOne { get; init; }
    public Dictionary<string, string>? HasMany { get; init; }
}
