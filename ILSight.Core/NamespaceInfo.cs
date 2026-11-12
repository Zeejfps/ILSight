
namespace ILSight.Core;

/// <summary>A namespace of <see cref="Module"/>: the types it defines there and those it forwards from
/// there to other assemblies. One that holds neither is listed only because others nest in it.</summary>
public sealed class NamespaceInfo
{
    public required LoadedModule Module { get; init; }

    /// <summary>The full name; empty for the global namespace.</summary>
    public required string Name { get; init; }

    /// <summary>How many types the namespace holds itself; the types are the I/O side's
    /// (<see cref="NodeDefinitions.GetTypes"/>).</summary>
    public required int TypeCount { get; init; }

    public required IReadOnlyList<ForwardedTypeInfo> Forwarded { get; init; }

    /// <summary>The name as the tree shows it: <c>-</c> for the global namespace.</summary>
    public string Label => Name.Length == 0 ? "-" : Name;

    /// <summary>Whether the module puts no types here, listing it only because others nest in it.</summary>
    public bool IsEnclosingOnly => TypeCount == 0 && Forwarded.Count == 0;

    /// <summary>Whether a type here is visible outside the module: one it forwards, or a public one.</summary>
    public required bool IsPublicApi { get; init; }

    /// <summary>Whether everything here is something the compiler made up.</summary>
    public required bool IsCompilerGenerated { get; init; }
}
