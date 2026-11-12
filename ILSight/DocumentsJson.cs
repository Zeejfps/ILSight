using System.Text.Json;

namespace ILSight;

/// <summary>A list's open documents as a settings file can hold them, each tab read on its own.</summary>
internal sealed record DocumentsJson
{
    public List<JsonElement>? Tabs { get; init; }

    public string? Active { get; init; }

    public string? Preview { get; init; }

    public DockLayoutJson? Layout { get; init; }
}
