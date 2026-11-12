using System.Text.Json.Serialization;

namespace ILSight;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DockSplitJson), "split")]
[JsonDerivedType(typeof(DockGroupJson), "group")]
internal abstract class DockNodeJson
{
    public int Id { get; set; }
}
