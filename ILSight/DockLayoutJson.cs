namespace ILSight;

/// <summary>A dock layout as a settings file can hold it. A part the file leaves out is the part a new
/// layout starts with; a part it holds as null, or a panel it names as null, is missing, and a layout
/// missing a part is none. The parts have setters, not init: the source-generated reader gives an init
/// property the file leaves out its type's default, not the one it starts with here.</summary>
internal sealed class DockLayoutJson
{
    public DockNodeJson? Root { get; set; } = new DockGroupJson();
    public List<DockWindowJson?>? Windows { get; set; } = new();
    public int NextId { get; set; } = 1;
}
