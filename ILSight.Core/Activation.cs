namespace ILSight.Core;

/// <summary>What activating a node does: open what it stands for, or go where it points, as a reference,
/// a forwarded type, an imported method or a base or derived type does.</summary>
public abstract record Activation
{
    private Activation()
    {
    }

    public sealed record OpenSelf : Activation;

    public sealed record Navigate(CodeReference.Navigable Target) : Activation;
}
