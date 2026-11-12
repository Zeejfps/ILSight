namespace ILSight;

/// <summary>One color or icon theme File's JSON as it is read: comments and trailing commas allowed,
/// every token's line known, and what was wrong collected in <see cref="Errors"/> with the File and line,
/// reading going on past a bad key or value. A File that is no JSON object at all is
/// <see cref="IsUnreadable"/>.</summary>
public sealed class ThemeJson
{
    public const string SchemaKey = "$schema";
    public const string NameKey = "name";
    public const string ExtendsKey = "extends";

    public readonly List<ThemeError> FoundErrors = new();
    public List<int> LineStarts = new();

    /// <summary>Where the File is, as errors name it.</summary>
    public required string File { get; init; }

    public IReadOnlyList<ThemeError> Errors => FoundErrors;

    /// <summary>True when the File could not be read as a JSON object, so nothing in it counts.</summary>
    public bool IsUnreadable { get; set; }
}
