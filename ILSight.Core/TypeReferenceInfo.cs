using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>A type <see cref="Module"/> uses from one of its references: labelled as <c>typeof</c> names
/// an open generic type, and fully named after the type it is nested in, else after its namespace.</summary>
public sealed class TypeReferenceInfo
{
    /// <summary>The name with its namespace, the types it is nested in and how many type parameters it
    /// has, as metadata writes it: <c>System.Collections.Generic.List`1</c>.</summary>
    public required string ReflectionName { get; init; }

    public required LoadedModule Module { get; init; }

    public required TypeReferenceHandle Handle { get; init; }

    public required string Namespace { get; init; }

    public required string Label { get; init; }

    public required string FullName { get; init; }
}
