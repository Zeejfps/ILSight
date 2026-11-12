using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ILSight.DiagramArea;

namespace ILSight;

/// <summary>An open tab as a settings file can hold it. Without a panel or what it shows it is none.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NodeDocumentJson), "node")]
[JsonDerivedType(typeof(DiagramDocumentJson), "diagram")]
[JsonDerivedType(typeof(ComparisonDocumentJson), "comparison")]
internal abstract record DocumentJson
{
    public string? Panel { get; init; }

    public string? Title { get; init; }
}
