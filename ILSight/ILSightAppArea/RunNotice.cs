namespace ILSight.ILSightAppArea;

/// <summary>Where a run that opens no window of its own tells the user something: its standard error, where the run
/// has a console, or else a small dialog of its own that waits for a click.</summary>
public union RunNotice(RunNotice.OnConsole, RunNotice.InDialog)
{
    /// <summary>Written to <see cref="Error"/>, the run's standard error.</summary>
    public sealed record OnConsole(TextWriter Error);

    /// <summary>Shown by <see cref="Show"/>, handed the message and its button's label, which returns once the user
    /// clicked it or closed the dialog.</summary>
    public sealed record InDialog(Action<string, string> Show);

    /// <summary>The channel this run has: its standard error where it has a console, else a dialog
    /// (<see cref="RunNoticeDialog"/>).</summary>
    public static RunNotice ForThisRun()
    {
        if (HasConsole())
        {
            return new OnConsole(Console.Error);
        }

        // Decided by Claude (2026-11-13; the owner may override): a run with no console says it in a small dialog of
        // its own that waits for a click. ILSight is an interactive desktop product, so a dialog is its choice to make,
        // and it works on every platform ZGF runs on; a desktop notification would need per-OS code.
        return new InDialog(RunNoticeDialog.Show);
    }

    // On Windows the app is a GUI program: its standard error reaches anyone only where whoever ran it redirected it. On
    // macOS and Linux it has a console where its standard error is a terminal; a launcher's redirect into a log is read
    // as no console, since no one is watching it.
    private static bool HasConsole()
    {
        if (OperatingSystem.IsWindows())
        {
            return Console.IsErrorRedirected;
        }

        return !Console.IsErrorRedirected;
    }

    /// <summary>Says <paramref name="message"/> on this channel; <paramref name="ok"/> labels a dialog's button.</summary>
    public void Say(string message, string ok)
    {
        switch (this)
        {
            case OnConsole console:
                console.Error.WriteLine(message);
                break;
            case InDialog dialog:
                dialog.Show(message, ok);
                break;
        }
    }
}
