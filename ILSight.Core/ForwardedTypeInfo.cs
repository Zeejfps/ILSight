using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>A type <see cref="Module"/> forwards to the assembly <see cref="Destination"/> names.</summary>
public sealed class ForwardedTypeInfo
{
    public required LoadedModule Module { get; init; }

    public required ExportedTypeHandle Handle { get; init; }

    public required string Namespace { get; init; }

    /// <summary>The type as <c>typeof</c> names an open generic type, e.g. <c>Dictionary&lt;,&gt;</c>.</summary>
    public required string Label { get; init; }

    /// <summary>The name without type parameters, which it sorts by, then by <see cref="Arity"/>.</summary>
    public required string SortName { get; init; }

    /// <summary>How many type parameters it has.</summary>
    public required int Arity { get; init; }

    public required string Destination { get; init; }

    public string FullName => Namespace.Length == 0 ? Label : $"{Namespace}.{Label}";

    /// <summary>The name with its namespace and how many type parameters it has, as metadata writes it:
    /// <c>System.Collections.Generic.Dictionary`2</c>.</summary>
    public string ReflectionName
    {
        get
        {
            var name = Arity == 0 ? SortName : $"{SortName}`{Arity}";
            return Namespace.Length == 0 ? name : $"{Namespace}.{name}";
        }
    }
}
