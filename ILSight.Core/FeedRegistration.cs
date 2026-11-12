using System.Text.Json;

namespace ILSight.Core;

/// <summary>A version's registration: when it was listed, and its catalog entry, inline or as the URL of one.</summary>
internal sealed record FeedRegistration(DateTimeOffset? Published, JsonElement? CatalogEntry);
