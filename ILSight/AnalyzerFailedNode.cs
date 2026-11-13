using ILSight.Core;

namespace ILSight;

/// <summary>A root restored from the saved analyses whose symbol could not be found: kept as saved (it saves as the
/// analysis it was restored from), shown failed with why, and looked for again once its assemblies are read again (a
/// reload; the list opened again). It goes when the user removes it.</summary>
public sealed class AnalyzerFailedNode : AnalyzerNode
{
    public required CodeAnchor SavedAs { get; init; }

    public required RestoreFailure Failure { get; init; }
}
