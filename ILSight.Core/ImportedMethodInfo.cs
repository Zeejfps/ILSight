namespace ILSight.Core;

/// <summary>A method imported from a module: <see cref="Shown"/> is the method it is labelled and goes
/// as, which for a stub the compiler generates as a local function is the method declaring it.</summary>
public sealed class ImportedMethodInfo
{
    public required DefinedMember Shown { get; init; }

    /// <summary>The XML documentation id of the method that calls into the module, the stub itself for a
    /// local function.</summary>
    public required string Id { get; init; }

    /// <summary>The method as the tree labels it, with the parameters the module is called with.</summary>
    public required string Label { get; init; }

    public required string DeclaringTypeName { get; init; }

    /// <summary>The name of the function the method calls in the module.</summary>
    public required string EntryPoint { get; init; }

    public required string ModuleName { get; init; }
}
