namespace ILSight;

internal sealed class DockWindowJson
{
    public int Id { get; set; }
    public DockNodeJson? Root { get; set; } = new DockGroupJson();
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}
