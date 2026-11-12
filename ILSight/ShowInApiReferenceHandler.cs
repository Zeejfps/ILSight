using ILSight.Core;

namespace ILSight;

/// <summary>Opens the page of the .NET API reference a type or member has.</summary>
public sealed class ShowInApiReferenceHandler(ShowInApiReferenceHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the type or member has no page, or is in a module that could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The address of the page of the .NET API reference <paramref name="reference"/> has; not found
        /// when it has none, unreadable when it is in a module whose type system the library could not read; looked
        /// for without holding up the screen; a lookup that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<string>>> LookUpApiReferencePageAsync(CodeReference.Entity reference, CancellationToken ct);
    }

    /// <summary>Has the view open the page of the .NET API reference <paramref name="reference"/> has, through
    /// <paramref name="viewCommands"/>, or reports that it has none, or that it is in a module that could not be
    /// read.</summary>
    public async Task HandleAsync(Queue<ViewCommand> viewCommands, CodeReference.Entity reference, CancellationToken ct)
    {
        var answer = await ports.LookUpApiReferencePageAsync(reference, ct);
        var page = answer.Match(static ok => ok.Value, static _ => new Lookup<string>.NotFound());
        page.Switch(
            found => viewCommands.Enqueue(new ViewCommand.OpenWebPage(found.Value)),
            _ => ports.ReportError(new AppProblem.NoApiReferencePage(reference.Name)),
            unreadable => ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module)));
    }
}
