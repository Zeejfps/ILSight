using System.Diagnostics;
using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>The note a tab's code view shows while the tab has no code of its own, in the interface's
/// language, written again only when what it says changes.</summary>
public sealed class TabPlaceholder
{
    /// <summary>The note written, by what it says and the language.</summary>
    public Memo<(TabContent, ByReference<Strings>), DecompiledDocument> Written;
}

public static class TabPlaceholders
{
    /// <summary>The note for <paramref name="content"/>, written with <paramref name="t"/>.</summary>
    public static DecompiledDocument GetDocument(TabPlaceholder placeholder, TabContent content, Strings t)
    {
        if (placeholder.Written.TryGet((content, new(t)), out var document))
        {
            return document;
        }

        document = Documents.CreateFromText("", $"// {Write(t, content)}");
        placeholder.Written = new((content, new(t)), document);
        return document;
    }

    /// <summary>What the note for <paramref name="content"/> says.</summary>
    internal static string Write(Strings t, TabContent content)
    {
        return content switch
        {
            TabContent.Loading => t.DocumentDecompiling,
            TabContent.Failed failed => t.DocumentDecompilationFailed(failed.Message),
            TabContent.Reloading or TabContent.Reopening => t.DocumentReloading,
            TabContent.Gone gone => t.DocumentGone(gone.Location.Label),
            _ => throw new UnreachableException(),
        };
    }
}
