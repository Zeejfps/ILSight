
namespace ILSight.DiagramArea;

internal sealed record DiagramDocumentJson : DocumentJson
{
    public List<TypeAnchorJson?>? Types { get; init; }

    public List<string?>? HomeAssemblies { get; init; }

    public DiagramViewJson? View { get; init; }
}
