namespace ILSight.Core;

/// <summary>A resource a service index lists: the types it is of, and its URL when it is a web URL.</summary>
internal sealed record FeedResource(IReadOnlyList<string> Types, WebUrl? Url);
