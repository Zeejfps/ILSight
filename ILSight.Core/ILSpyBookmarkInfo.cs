namespace ILSight.Core;

/// <summary>What the real ILSpy records about a bookmark besides its line: the assembly it is in,
/// the metadata token of the entity it names or sits in, that entity's full name, the name of the
/// node whose document holds it, and the path ILSpy's tree finds that node by.</summary>
public readonly record struct ILSpyBookmarkInfo
{
    public required string AssemblyFullName { get; init; }

    public required string ModuleName { get; init; }

    public required uint Token { get; init; }

    public required string MemberName { get; init; }

    public required string? LocationNodeName { get; init; }

    public required IReadOnlyList<string>? TreePath { get; init; }
}
