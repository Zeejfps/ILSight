using ILSight.Core;

namespace ILSight;

/// <summary>Saves the bytes of a resource to a file straight away, through <see cref="IPorts"/>, and reports a file
/// that could not be written.</summary>
public sealed class SaveResourceHandler(SaveResourceHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the resource could not be saved.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Writes the bytes of the resource <paramref name="node"/> stands for to the file at
        /// <paramref name="path"/>, as they are; a file that could not be read or written comes back as
        /// <see cref="Outcome.Failed"/>.</summary>
        Outcome SaveResource(BrowseNode node, string path);
    }

    /// <summary>Writes the bytes of the resource <paramref name="node"/> stands for to the file at
    /// <paramref name="path"/>; reports it when that failed.</summary>
    public void Handle(BrowseNode node, string path)
    {
        var saved = ports.SaveResource(node, path);
        if (saved is Outcome.Failed failed)
        {
            ports.ReportError(new AppProblem.ResourceNotSaved(node.Text, failed.Error.Message));
        }
    }
}
