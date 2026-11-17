using ILSight.Core;

namespace ILSight;

/// <summary>What a diagram's or a comparison's tab is titled: the name it was made for, worded in the interface's
/// language when drawn, so a language switch reaches it as it does every label; or a title saved by a version that
/// kept it worded, shown as it was saved.</summary>
public union TabTitle(TabTitle.Diagram, TabTitle.Comparison, TabTitle.Saved)
{
    /// <summary>The title in <paramref name="t"/>'s language.</summary>
    public string Words(Strings t)
    {
        return this switch
        {
            Diagram d => t.DiagramTitle(OneLineText.Name(d.Type)),
            Comparison c => t.ComparisonTitle(OneLineText.Name(c.Assembly)),
            Saved s => OneLineText.Name(s.Text),
        };
    }

    /// <summary>A diagram made for the type named <see cref="Type"/>.</summary>
    public sealed record Diagram(string Type);

    /// <summary>A comparison of the API of the assembly named <see cref="Assembly"/> with another.</summary>
    public sealed record Comparison(string Assembly);

    /// <summary>A title the saved documents held already worded, by a version that saved no name: kept as it was
    /// saved, in the language it was made in, until its tab is closed.</summary>
    public sealed record Saved(string Text);
}
