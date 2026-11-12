using System.Diagnostics;

namespace ILSight.Core;

/// <summary>A file of a package that was not extracted, and why.</summary>
public abstract record ExtractFailure
{
    private ExtractFailure()
    {
    }

    public required PackageFile File { get; init; }

    /// <summary>Its path would have put it outside the folder extracted into.</summary>
    public sealed record OutsideFolder : ExtractFailure;

    public sealed record NotWritten : ExtractFailure
    {
        public required ExportError Error { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<OutsideFolder, TResult> outsideFolder, Func<NotWritten, TResult> notWritten)
    {
        return this switch
        {
            OutsideFolder c => outsideFolder(c),
            NotWritten c => notWritten(c),
            _ => throw new UnreachableException($"ExtractFailure.Match does not handle {GetType().Name}."),
        };
    }
}
