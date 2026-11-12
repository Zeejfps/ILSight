namespace ILSight;

/// <summary>What keeps an export from starting.</summary>
public abstract record ExportBlocker
{
    private ExportBlocker()
    {
    }

    public sealed record NoAssembly : ExportBlocker;

    public sealed record NoFolder : ExportBlocker;

    public sealed record NamelessProject : ExportBlocker;

    /// <summary>Two ticked assemblies take the project name <see cref="Name"/>.</summary>
    public sealed record Clash(string Name) : ExportBlocker;

    public sealed record KeyFileMissing : ExportBlocker;
}
