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

    private readonly List<ThemeError> _errors = new();
    private List<int> _lineStarts = new();

    /// <summary>Where the File is, as errors name it.</summary>
    public required string File { get; init; }

    public IReadOnlyList<ThemeError> Errors => _errors;

    /// <summary>True when the File could not be read as a JSON object, so nothing in it counts.</summary>
    public bool IsUnreadable { get; private set; }

    /// <summary>Indexes the lines of <paramref name="content"/>, the JSON about to be read, so that a token's offset in
    /// it gives its line (<see cref="GetLineAt"/>).</summary>
    public void IndexLines(ReadOnlySpan<byte> content)
    {
        _lineStarts = FindLineStarts(content);
    }

    public int GetLineAt(long offset)
    {
        var index = _lineStarts.BinarySearch((int)offset);
        if (index >= 0)
        {
            return index + 1;
        }

        return ~index;
    }

    public void MarkUnreadable(int line, ThemeProblem problem, string detail = "")
    {
        IsUnreadable = true;
        AddError(line, problem, "", detail);
    }

    public void AddError(int line, ThemeProblem problem, string subject = "", string detail = "")
    {
        _errors.Add(new ThemeError
        {
            File = File,
            Line = line,
            Problem = problem,
            Subject = subject,
            Detail = detail,
        });
    }

    private static List<int> FindLineStarts(ReadOnlySpan<byte> content)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == (byte)'\n')
            {
                starts.Add(i + 1);
            }
        }
        return starts;
    }
}
