using System.Diagnostics;
using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>The note a tab's code view shows while the tab has no code of its own, in the interface's
/// language, written again only when what it says changes.</summary>
public sealed class TabPlaceholder
{
    public TabContent? Content { get; set; }

    public Strings? Strings { get; set; }

    public DecompiledDocument Document { get; set; } = DecompiledDocument.Empty;
}

public static class TabPlaceholders
{
    /// <summary>The note for <paramref name="content"/>, written with <paramref name="t"/>.</summary>
    public static DecompiledDocument GetDocument(TabPlaceholder placeholder, TabContent content, Strings t)
    {
        if (IsWritten(placeholder, content, t))
            return placeholder.Document;
        placeholder.Content = content;
        placeholder.Strings = t;
        placeholder.Document = DecompiledDocument.FromText("", $"// {Write(t, content)}");
        return placeholder.Document;
    }

    private static bool IsWritten(TabPlaceholder placeholder, TabContent content, Strings t)
    {
        if (placeholder.Strings != t)
            return false;
        return Equals(placeholder.Content, content);
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
