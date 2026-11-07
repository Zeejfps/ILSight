using System.Text.Json;

namespace ILSight;

/// <summary>A settings file as it is being read: the fields it holds, and which of them were left out.</summary>
internal sealed class SettingsReader
{
    public required Dictionary<string, JsonElement> Fields { get; init; }

    /// <summary>The fields left out, in the order they were read, each once.</summary>
    public List<string> LeftOut { get; } = new();
}
