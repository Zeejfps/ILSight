namespace ILSight.ExportArea;

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

    /// <summary>What the disk says about the folder and key file named is still being looked at.</summary>
    public sealed record Checking : ExportBlocker;
}
