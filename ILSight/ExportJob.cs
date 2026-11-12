namespace ILSight;

/// <summary>An export as the user asked for it: what it is named after, what it writes, and the
/// folder it writes into.</summary>
public sealed record ExportJob(string Name, ExportKind Kind, string Folder);
