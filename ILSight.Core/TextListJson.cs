using System.Text.Json.Serialization;

namespace ILSight.Core;

/// <summary>Text a feed may give as one string or as a list of strings; what else a list holds is left out.</summary>
[JsonConverter(typeof(TextListConverter))]
internal sealed record TextListJson(IReadOnlyList<string> Items);
