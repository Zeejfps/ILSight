namespace ILSight.ILSightAppArea;

/// <summary>Doing what the arguments of a run ask, this run's and those later runs hand the window already open
/// alike: opening the files they name; and what to tell of the ones ignored, which the edge that takes them
/// tells.</summary>
public static class RunArgumentsActions
{
    /// <summary>What to tell the user of the arguments of <paramref name="arguments"/> that were ignored, or null when
    /// none was: a refusal, said on the refusal line, as no place the user acted asked for them.</summary>
    public static Refusal? FindIgnored(RunArguments arguments)
    {
        if (arguments.Ignored.Count == 0)
        {
            return null;
        }

        return new Refusal.ArgumentsIgnored { Arguments = arguments.Ignored.ToList() };
    }

    /// <summary>Opens <paramref name="files"/>, then shows the namespace, type or member they
    /// ask to navigate to, or else the last file opened. Returns the load that started in the tab showing it.</summary>
    public static Task<DecompileTicket?> OpenRequestedFilesAsync(AppState appState, FilesToOpen files, CancellationToken ct)
    {
        if (files.NavigateTo is null)
        {
            return appState.AssemblyOpening.OpenAssembliesAsync(appState, files.Paths, ct);
        }
        return appState.OpenAndShowIdHandler.HandleAsync(appState, files.Paths, files.NavigateTo, ct);
    }
}
