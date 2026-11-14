using ILSight.Core;
using ILSight.DocumentViewArea;

namespace ILSight;

/// <summary>The note a tab's code view shows while the tab has no code of its own, in the interface's
/// language, written again only when what it says changes.</summary>
public sealed class TabPlaceholder
{
    private Memo<(TabContent, ByReference<Strings>), DecompiledDocument> _written;

    /// <summary>The note for <paramref name="content"/>, written with <paramref name="t"/>.</summary>
    public DecompiledDocument GetDocument(TabContent content, Strings t)
    {
        var key = (content, new ByReference<Strings>(t));
        if (_written.TryGet(key, out var document))
        {
            return document;
        }

        document = Documents.CreateFromText("", $"// {TabPlaceholders.Write(t, content)}");
        _written = new Memo<(TabContent, ByReference<Strings>), DecompiledDocument>(key, document);
        return document;
    }
}
