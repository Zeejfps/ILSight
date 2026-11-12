using System.Text.Json;

namespace ILSight.SettingsFormat;

/// <summary>The real ILSpy's bookmarks file as it can hold them, each bookmark read on its own.</summary>
internal sealed record BookmarkFileJson(int Version, List<JsonElement>? Bookmarks);
