using System.Diagnostics;
using ILSight.Core;

namespace ILSight.DocumentViewArea;

/// <summary>What a tab's placeholder note says (<see cref="TabPlaceholder"/> keeps it written).</summary>
public static class TabPlaceholders
{
    /// <summary>What the note for <paramref name="content"/> says.</summary>
    internal static string Describe(Strings t, TabContent content)
    {
        if (content is TabContent.Loading)
        {
            return t.DocumentDecompiling;
        }

        if (content is TabContent.Failed failed)
        {
            return t.DocumentDecompilationFailed(OneLineText.Message(failed.Message));
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
            return t.DocumentGone(OneLineText.Name(gone.Location.Label));
        }

        if (content is TabContent.NotReloaded notReloaded)
        {
            return notReloaded.End == ReloadEnd.AssemblyClosed
                ? t.DocumentClosed(OneLineText.Name(notReloaded.Location.FilePath.FileName))
                : t.DocumentNotReloaded(OneLineText.Name(notReloaded.Location.Label));
        }

        if (content is TabContent.Unreadable unreadable)
        {
            return t.CauseModuleUnreadableSentence(name: OneLineText.Name(unreadable.Module), why: OneLineText.Message(unreadable.Why));
        }

        throw new UnreachableException($"A tab showing {content.GetType().Name} has no placeholder note.");
    }
}
