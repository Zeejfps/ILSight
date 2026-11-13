using ILSight.Core;

namespace ILSight;

/// <summary>Writes a file of a package to a file, through <see cref="IPorts"/>, without holding up the screen, and
/// reports a file that could not be written.</summary>
public sealed class ExtractPackageFileHandler(ExtractPackageFileHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the file could not be extracted.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Writes the file of a package <paramref name="place"/> stands for, as the package holds it, to the
        /// file at <paramref name="path"/>, read and written without holding up the screen; a file that could not be read
        /// or written comes back as <see cref="Outcome.Failed"/>; the cancellation of <paramref name="ct"/> is let
        /// through.</summary>
        Task<Outcome> ExtractPackageFileAsync(PackagePlace place, string path, CancellationToken ct);
    }

    /// <summary>Writes the file of a package <paramref name="place"/> stands for to the file at
    /// <paramref name="path"/>; reports it when that failed. <paramref name="ct"/> is the workflow's token.</summary>
    public async Task HandleAsync(PackagePlace place, string path, CancellationToken ct)
    {
        var extracted = await ports.ExtractPackageFileAsync(place, path, ct);
        if (extracted is Outcome.Failed failed)
        {
            ports.ReportError(new AppProblem.PackageFileNotExtracted
            {
                File = place.File.Name,
                Reason = failed.Error.Message,
            });
        }
    }
}
