using System.Text.Json;

namespace ILSight;

/// <summary>A list's open documents as a settings file can hold them, each tab read on its own.</summary>
internal sealed record DocumentsJson(List<JsonElement>? Tabs, string? Active, string? Preview, DockLayoutJson? Layout);
