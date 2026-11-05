using ILSight.Core;

namespace ILSight.Tests;

/// <summary>A test of what holds only where <see cref="AssemblyPath"/> ignores case; skipped elsewhere.</summary>
public sealed class CaseInsensitivePathsFactAttribute : FactAttribute
{
    public CaseInsensitivePathsFactAttribute() => Skip = CaseInsensitivePaths.SkipReason;
}

/// <summary>A theory of what holds only where <see cref="AssemblyPath"/> ignores case; skipped elsewhere.</summary>
public sealed class CaseInsensitivePathsTheoryAttribute : TheoryAttribute
{
    public CaseInsensitivePathsTheoryAttribute() => Skip = CaseInsensitivePaths.SkipReason;
}

internal static class CaseInsensitivePaths
{
    /// <summary>Why a test of paths that ignore case cannot run here, or null when it can.</summary>
    public static string? SkipReason =>
        AssemblyPath.Comparison == StringComparison.OrdinalIgnoreCase ? null : "Paths on this OS are compared by case.";
}
