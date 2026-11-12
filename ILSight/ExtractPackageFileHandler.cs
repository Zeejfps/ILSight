using ILSight.Core;

namespace ILSight;

/// <summary>Writes a file of a package to a file straight away, through <see cref="IPorts"/>, and reports a file that
/// could not be written.</summary>
public sealed class ExtractPackageFileHandler(ExtractPackageFileHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the file could not be extracted.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Writes the file of a package <paramref name="place"/> stands for, as the package holds it, to the
        /// file at <paramref name="path"/>; a file that could not be read or written comes back as
        /// <see cref="Outcome.Failed"/>.</summary>
        Outcome ExtractPackageFile(PackagePlace place, string path);
    }

    /// <summary>Writes the file of a package <paramref name="place"/> stands for to the file at
    /// <paramref name="path"/>; reports it when that failed.</summary>
    public void Handle(PackagePlace place, string path)
    {
        var extracted = ports.ExtractPackageFile(place, path);
        if (extracted is Outcome.Failed failed)
        {
            ports.ReportError(new AppProblem.PackageFileNotExtracted(place.File.Name, failed.Error.Message));
        }
    }
}
