using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>The query last searched for, what it looked for, and the assembly list's version it searched.</summary>
public sealed record SearchedQuery(string Query, SearchKinds Kinds, int Version);
