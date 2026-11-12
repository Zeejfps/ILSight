using ILSight.Core.Metadata;

namespace ILSight;

/// <summary>A flags filter, as a settings file can hold it. A group without values is left as it starts.</summary>
internal sealed record SavedFilterStateJson
{
    public Dictionary<string, uint[]?>? Groups { get; init; }

    public Dictionary<string, TriState>? Flags { get; init; }

    public MatchMode Mode { get; init; }
}
