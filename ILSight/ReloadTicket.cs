namespace ILSight;

/// <summary>Given for one reload by <see cref="Reload.Start"/>.</summary>
[MustUse]
public sealed class ReloadTicket
{
    public required int Id { get; init; }

    public required CancellationTokenSource Work { get; init; }

    public CancellationToken Token => Work.Token;

    /// <summary>Whether another reload or clearing the list stopped this one.</summary>
    public bool IsStopped => Work.IsCancellationRequested;
}
