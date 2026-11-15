namespace ILSight.MainArea;

/// <summary>The decompiler options panel's own state: what it is searched for. One per place the panel is shown.</summary>
public sealed class DecompilerOptionsPanel
{
    /// <summary>What the panel is searched for, which its search box is bound to; empty shows every option.</summary>
    public string Query = "";
}
