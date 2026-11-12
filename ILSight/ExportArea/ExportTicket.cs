namespace ILSight.ExportArea;

/// <summary>An export <see cref="ExportQueue.Enter"/> queued: which it is, what it is, and its turn, which comes when
/// every export queued before it has ended (at once when none runs), or comes withdrawn when it is taken out of the
/// queue first. Dropped, a started export never ends its turn and the exports queued after it wait for good.</summary>
[MustUse]
public sealed class ExportTicket
{
    public required ExportRun Run { get; init; }

    public required ExportJob Job { get; init; }

    /// <summary>Completes when the export's turn comes: <see cref="ExportStart"/>, which it ends with
    /// <see cref="ExportStart.EndTurn"/>, or <see cref="ExportWithdrawn"/>.</summary>
    public required Task<ExportTurn> Turn { get; init; }
}
