
namespace ILSight.DiagramArea;

internal sealed record DiagramDocumentJson(
    string? Panel,
    string? Title,
    List<TypeAnchorJson?>? Types,
    List<string?>? HomeAssemblies,
    DiagramViewJson? View) : DocumentJson(Panel, Title);
