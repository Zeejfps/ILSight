using ILSight.Core;

namespace ILSight;

/// <summary>The reload of the assemblies from disk that counts. Starting or stopping one cancels the
/// one before, whose result is then ignored.</summary>
public sealed class Reload
{
    public int Current { get; set; }

    public CancellationTokenSource? Work { get; set; }

    public bool IsRunning => Work != null;
}

/// <summary>Keeping track of what is being read from disk, so that what was overtaken is ignored.</summary>
public static class Reloads
{
    public static ReloadTicket Start(Reload reload)
    {
        Stop(reload);
        var work = new CancellationTokenSource();
        reload.Work = work;
        return new ReloadTicket { Id = reload.Current, Work = work };
    }

    public static void Stop(Reload reload)
    {
        reload.Current++;
        reload.Work?.Cancel();
        reload.Work = null;
    }

    /// <summary>Ends the reload <paramref name="ticket"/> was given for. False when another reload
    /// started or it was stopped since, and its result should be ignored.</summary>
    public static bool Finish(Reload reload, ReloadTicket ticket)
    {
        ticket.Work.Dispose();
        if (ticket.Id != reload.Current)
        {
            return false;
        }

        reload.Work = null;
        return true;
    }

    /// <summary>Starts a reopen of the file at <paramref name="path"/> and returns its ticket.</summary>
    public static int StartReopen(Reopens reopens, AssemblyPath path)
    {
        reopens.Last++;
        reopens.Current[path] = reopens.Last;
        return reopens.Last;
    }

    /// <summary>Ends the reopen of the file at <paramref name="path"/> given <paramref name="ticket"/>.
    /// False when another reopen of it started since, and its result should be ignored.</summary>
    public static bool FinishReopen(Reopens reopens, AssemblyPath path, int ticket)
    {
        if (!reopens.Current.TryGetValue(path, out var current))
        {
            return false;
        }

        if (current != ticket)
        {
            return false;
        }

        reopens.Current.Remove(path);
        return true;
    }

    /// <summary>Cancels the opens under way, and starts over for the ones to come.</summary>
    public static void CancelOpens(OpenRequests opens)
    {
        opens.Work.Cancel();
        opens.Work = new CancellationTokenSource();
    }
}

/// <summary>Given for one reload by <see cref="Reload.Start"/>.</summary>
public sealed class ReloadTicket
{
    public required int Id { get; init; }

    public required CancellationTokenSource Work { get; init; }

    public CancellationToken Token => Work.Token;
}
