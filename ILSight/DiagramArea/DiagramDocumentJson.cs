
namespace ILSight.DiagramArea;

internal sealed record DiagramDocumentJson : DocumentJson
{
    /// <summary>The name of the type its title is worded around; null in settings saved before, which held the title
    /// worded (<see cref="DocumentJson.Title"/>).</summary>
    public string? TitleName { get; init; }

    public List<TypeAnchorJson?>? Types { get; init; }

    public List<string?>? HomeAssemblies { get; init; }

    public DiagramViewJson? View { get; init; }
}
