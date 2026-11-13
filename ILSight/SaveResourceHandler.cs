using ILSight.Core;

namespace ILSight;

/// <summary>Saves the bytes of a resource to a file, through <see cref="IPorts"/>, without holding up the screen, and
/// reports a file that could not be written, or says why it was refused.</summary>
public sealed class SaveResourceHandler(SaveResourceHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the resource could not be saved.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user, where they acted, what was refused and why.</summary>
        void ShowRefusal(Refusal refusal);

        /// <summary>Writes the bytes of the resource <paramref name="node"/> stands for to the file at
        /// <paramref name="path"/>, as they are, read and written without holding up the screen; its assembly closed
        /// before it was read comes back as <see cref="ResourceSave.Closed"/>, a file that could not be read or written
        /// as <see cref="ResourceSave.Failed"/>; the cancellation of <paramref name="ct"/> is let through.</summary>
        Task<ResourceSave> SaveResourceAsync(BrowseNode node, string path, CancellationToken ct);
    }

    /// <summary>Writes the bytes of the resource <paramref name="node"/> stands for to the file at
    /// <paramref name="path"/>; reports it when that failed, and says why when its assembly was closed.
    /// <paramref name="ct"/> is the workflow's token.</summary>
    public async Task HandleAsync(BrowseNode node, string path, CancellationToken ct)
    {
        var saved = await ports.SaveResourceAsync(node, path, ct);
        saved.Switch(
            static _ => { },
            closed => ports.ShowRefusal(new Refusal.ResourceNotSaved { Resource = node.Text, Assembly = closed.Assembly }),
            failed => ports.ReportError(new AppProblem.ResourceNotSaved { Resource = node.Text, Reason = failed.Error.Message }));
    }
}
