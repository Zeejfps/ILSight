namespace ILSight;

/// <summary>Given for one check by <see cref="AppUpdates.StartCheck"/>: whether the user asked for it.</summary>
public sealed class UpdateCheck
{
    public required bool Asked { get; init; }
}
