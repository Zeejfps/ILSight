using System.Diagnostics;
using ILSight.Core;

namespace ILSight.DocumentViewArea;

/// <summary>What a tab's placeholder note says (<see cref="TabPlaceholder"/> keeps it written).</summary>
public static class TabPlaceholders
{
    /// <summary>What the note for <paramref name="content"/> says.</summary>
    internal static string Describe(Strings t, TabContent content)
    {
        return content switch
        {
            TabContent.Loading => t.DocumentDecompiling,
            TabContent.Failed failed => t.DocumentDecompilationFailed(OneLineText.Message(FailureTexts.DescribeCause(t, failed.Error))),
            TabContent.Reloading or TabContent.Reopening => t.DocumentReloading,
            TabContent.Gone gone => t.DocumentGone(OneLineText.Name(gone.Location.Label)),
            TabContent.NotReloaded notReloaded => DescribeNotReloaded(t, notReloaded),
            TabContent.Unreadable unreadable => t.CauseModuleUnreadableSentence(name: OneLineText.Name(unreadable.Module),
                why: OneLineText.Message(FailureTexts.DescribeCause(t, unreadable.Why))),
            TabContent.Empty or TabContent.Code or TabContent.Table or TabContent.Picture or TabContent.Report
                or TabContent.Diagram or TabContent.Comparison or TabContent.Stopped
                => throw new UnreachableException("A tab showing a document or nothing has no placeholder note."),
        };
    }

    private static string DescribeNotReloaded(Strings t, TabContent.NotReloaded notReloaded)
    {
        return notReloaded.End == ReloadEnd.AssemblyClosed
            ? t.DocumentClosed(OneLineText.Name(notReloaded.Location.FilePath.FileName))
            : t.DocumentNotReloaded(OneLineText.Name(notReloaded.Location.Label));
    }
}
