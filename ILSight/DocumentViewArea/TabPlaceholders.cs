using System.Diagnostics;
using ILSight.Core;

namespace ILSight.DocumentViewArea;

public static class TabPlaceholders
{
    /// <summary>The note for <paramref name="content"/>, written with <paramref name="t"/>.</summary>
    public static DecompiledDocument GetDocument(TabPlaceholder placeholder, TabContent content, Strings t)
    {
        if (placeholder.Written.TryGet((content, new ByReference<Strings>(t)), out var document))
        {
            return document;
        }

        document = Documents.CreateFromText("", $"// {Write(t, content)}");
        placeholder.Written = new Memo<(TabContent, ByReference<Strings>), DecompiledDocument>((content, new ByReference<Strings>(t)), document);
        return document;
    }

    /// <summary>What the note for <paramref name="content"/> says.</summary>
    internal static string Write(Strings t, TabContent content)
    {
        if (content is TabContent.Loading)
        {
            return t.DocumentDecompiling;
        }

        if (content is TabContent.Failed failed)
        {
            return t.DocumentDecompilationFailed(failed.Message);
        }

        if (content is TabContent.Reloading)
        {
            return t.DocumentReloading;
        }

        if (content is TabContent.Reopening)
        {
            return t.DocumentReloading;
        }

        if (content is TabContent.Gone gone)
        {
            return t.DocumentGone(gone.Location.Label);
        }

        throw new UnreachableException($"A tab showing {content.GetType().Name} has no placeholder note.");
    }
}
