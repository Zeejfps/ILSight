using ILSight.Core;

namespace ILSight;

/// <summary>Opens the page of the .NET API reference a type or member has.</summary>
public sealed class ShowInApiReferenceHandler(ShowInApiReferenceHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the type or member is in a module that could not be read, or looking it up
        /// failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user what they asked for was refused, and why, where they acted.</summary>
        void ShowRefusal(Refusal refusal);


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
        switch (answer)
        {
            case Outcome<Lookup<string>>.Ok ok:
                Show(viewCommands, reference, ok.Value);
                break;
            case Outcome<Lookup<string>>.Failed failed:
                ports.ReportError(LookupFailures.OfReference(reference, failed.Error));
                break;
        }
    }

    private void Show(Queue<ViewCommand> viewCommands, CodeReference.Entity reference, Lookup<string> page)
    {
        switch (page)
        {
            case Lookup<string>.Found found:
                viewCommands.Enqueue(new ViewCommand.OpenWebPage(found.Value));
                break;
            case Lookup<string>.NotFound:
                ports.ShowRefusal(new Refusal.NoApiReferencePage { Name = reference.Name });
                break;
            case Lookup<string>.Unreadable unreadable:
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
                break;
        }
    }
}
