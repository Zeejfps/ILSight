namespace ILSight.Core;

/// <summary>What a module's headers say it runs on: the platform's name as the build tools and other tools name it
/// (<c>AnyCPU</c>, <c>x86</c>, a machine's name such as <c>Amd64</c>), the same in every language, and the bitness an
/// AnyCPU module prefers, 32 or 64; null when it states none.</summary>
public sealed record ModuleArchitecture
{
    public required string Platform { get; init; }

    public required int? PreferredBits { get; init; }
}
