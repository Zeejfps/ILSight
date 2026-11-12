namespace ILSight.Core;

/// <summary>Which of an assembly's names an <see cref="SearchKinds.Assemblies"/> search matches.</summary>
public enum AssemblyNameKind
{
    NameOrFileName,
    FilePath,
    FullName,
}
