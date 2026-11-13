using System.Diagnostics;

namespace ILSight.ExportArea;

/// <summary>How far the look at the disk for the folder and key file the export dialog names has got: being looked at,
/// looked at with what the disk said, or failed, saying why (the folder could not be listed), until it is asked
/// again.</summary>
public abstract record ExportFileLook
{
    private ExportFileLook()
    {
    }

    public sealed record Checking : ExportFileLook;

    public sealed record Checked : ExportFileLook
    {
        public required ExportFileFacts Facts { get; init; }
    }

    public sealed record Failed : ExportFileLook
    {
        public required string Why { get; init; }
    }

    public TResult Match<TResult>(Func<Checking, TResult> checking, Func<Checked, TResult> isChecked, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Checking c => checking(c),
            Checked c => isChecked(c),
            Failed f => failed(f),
            _ => throw new UnreachableException($"ExportFileLook.Match does not handle {GetType().Name}."),
        };
    }
}
