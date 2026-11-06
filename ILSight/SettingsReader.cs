using System.Text.Json;

namespace ILSight;

/// <summary>A settings file as it is being read: the fields it holds, and which of them were left out.</summary>
internal sealed class SettingsReader(Dictionary<string, JsonElement> fields)
{
    public Dictionary<string, JsonElement> Fields { get; } = fields;

    /// <summary>The fields left out, in the order they were read, each once.</summary>
    public List<string> LeftOut { get; } = new();
}
