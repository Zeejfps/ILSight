namespace ILSight;

/// <summary>What a diagram's or a comparison's tab is titled: the name it was made for, worded in the interface's
/// language when drawn, so a language switch reaches it as it does every label; or a title saved by a version that
/// kept it worded, shown as it was saved.</summary>
public abstract record TabTitle
{
    private TabTitle()
    {
    }

    /// <summary>The title in <paramref name="t"/>'s language.</summary>
    public abstract string Words(Strings t);

    /// <summary>A diagram made for the type named <see cref="Type"/>.</summary>
    public sealed record Diagram(string Type) : TabTitle
    {
        public override string Words(Strings t)
        {
            return t.DiagramTitle(Type);
        }
    }

    /// <summary>A comparison of the API of the assembly named <see cref="Assembly"/> with another.</summary>
    public sealed record Comparison(string Assembly) : TabTitle
    {
        public override string Words(Strings t)
        {
            return t.ComparisonTitle(Assembly);
        }
    }

    /// <summary>A title the saved documents held already worded, by a version that saved no name: kept as it was
    /// saved, in the language it was made in, until its tab is closed.</summary>
    public sealed record Saved(string Text) : TabTitle
    {
        public override string Words(Strings t)
        {
            return Text;
        }
    }
}
