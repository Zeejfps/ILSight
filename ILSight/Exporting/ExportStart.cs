using ILSight.Core;

namespace ILSight.Exporting;

/// <summary>What an export works with once its turn comes: the token that cancels it and where it
/// reports how far it has got. It ends its own turn with <see cref="EndTurn"/>.</summary>
public sealed record ExportStart : ExportTurn
{
    private readonly Action _endTurn;

    internal ExportStart(ExportRun run, IProgress<ExportProgress> progress, Action endTurn, CancellationToken token)
    {
        Run = run;
        Token = token;
        Progress = progress;
        _endTurn = endTurn;
    }

    public ExportRun Run { get; }

    public CancellationToken Token { get; }

    public IProgress<ExportProgress> Progress { get; }

    /// <summary>Ends this export's turn, when it is still the running one, and starts the next
    /// waiting export. Ending it again does nothing.</summary>
    public void EndTurn()
    {
        _endTurn();
    }
}
