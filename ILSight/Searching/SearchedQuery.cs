using ILSight.Core;

namespace ILSight.Searching;

/// <summary>The query last searched for, what it looked for, and the assembly list's version it searched.</summary>
public sealed record SearchedQuery(string Query, SearchKinds Kinds, int Version);
