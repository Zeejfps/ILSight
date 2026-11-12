using ILSight.Core.Metadata;

namespace ILSight.SettingsFormat;

/// <summary>A flags filter, as a settings file can hold it. A group without values is left as it starts.</summary>
internal sealed record SavedFilterStateJson(Dictionary<string, uint[]?>? Groups, Dictionary<string, TriState>? Flags, MatchMode Mode);
