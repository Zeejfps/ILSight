namespace ILSight.ExportArea;

/// <summary>What keeps an export from starting.</summary>
public union ExportBlocker(ExportBlocker.NoAssembly, ExportBlocker.NoFolder, ExportBlocker.NamelessProject,
    ExportBlocker.Clash, ExportBlocker.KeyFileMissing, ExportBlocker.Checking, ExportBlocker.CheckFailed)
{
    public sealed record NoAssembly;

    public sealed record NoFolder;

    public sealed record NamelessProject;

    /// <summary>Two ticked assemblies take the project name <see cref="Name"/>.</summary>
    public sealed record Clash(string Name);

    public sealed record KeyFileMissing;

    /// <summary>What the disk says about the folder and key file named is still being looked at.</summary>
    public sealed record Checking;

    /// <summary>The look at the disk for the folder and key file named failed, as <see cref="Why"/> says; the user can
    /// ask it again.</summary>
    public sealed record CheckFailed(string Why);
}
