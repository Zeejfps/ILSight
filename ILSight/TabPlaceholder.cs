using ILSight.Core;

namespace ILSight;

/// <summary>The note a tab's code view shows while the tab has no code of its own, in the interface's
/// language, written again only when what it says changes.</summary>
public sealed class TabPlaceholder
{
    /// <summary>The note written, by what it says and the language.</summary>
    public Memo<(TabContent, ByReference<Strings>), DecompiledDocument> Written;
}
