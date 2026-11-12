namespace ILSight;

internal sealed class DockGroupJson : DockNodeJson
{
    public List<string?>? Panels { get; set; } = new();
    public string? Active { get; set; }
}
