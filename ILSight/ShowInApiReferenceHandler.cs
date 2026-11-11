using ILSight.Core;

namespace ILSight;

/// <summary>Opens the page of the .NET API reference a type or member has.</summary>
public sealed class ShowInApiReferenceHandler(ShowInApiReferenceHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the type or member has no page.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The address of the page of the .NET API reference <paramref name="reference"/> has, null when it
        /// has none, looked for without holding up the screen; a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<string?>> LookUpApiReferencePageAsync(CodeReference.Entity reference, CancellationToken ct);
    }

    /// <summary>Has the view open the page of the .NET API reference <paramref name="reference"/> has, or reports
    /// that it has none.</summary>
    public async Task HandleAsync(AppState appState, CodeReference.Entity reference, CancellationToken ct)
    {
        var found = await ports.LookUpApiReferencePageAsync(reference, ct);
        if (found is Outcome<string?>.Ok ok && ok.Value is not null)
        {
            appState.ViewCommands.Enqueue(new ViewCommand.OpenWebPage(ok.Value));
        }
        else
        {
            ports.ReportError(new AppProblem.NoApiReferencePage(reference.Name));
        }
    }
}
