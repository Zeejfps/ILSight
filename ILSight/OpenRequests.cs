namespace ILSight;

/// <summary>The files being opened in the background. Cancelling them all, as clearing the list does,
/// keeps what they open out of the list that comes next.</summary>
public sealed record OpenRequests
{
    public CancellationTokenSource Work { get; set; } = new();

    /// <summary>The token of the opens starting now, cancelled by the next <see cref="Reloads.CancelOpens"/>.</summary>
    public CancellationToken Token => Work.Token;
}
