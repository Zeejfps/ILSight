using ZGF.Gui.Immediate;

namespace ILSight;

internal sealed class DockSplitJson : DockNodeJson
{
    public Direction Direction { get; set; }
    public float Ratio { get; set; } = 0.5f;
    public DockNodeJson? First { get; set; } = new DockGroupJson();
    public DockNodeJson? Second { get; set; } = new DockGroupJson();
}
