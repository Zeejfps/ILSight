using ILSight.Core;

namespace ILSight.FrameworkArea;

/// <summary>The "open from framework" dialog as its widgets bind it: whether it is open and the filter typed;
/// and <see cref="Browser"/>, the runtime's assemblies it offers, which keeps its own rules.</summary>
public sealed class FrameworkPicker
{
    public bool IsOpen;
    public string Filter = "";

    /// <summary>The runtime's assemblies, scanned once, those the filter shows and those picked out.</summary>
    public required FrameworkBrowser Browser { get; init; }

    /// <summary>Opens the dialog, its filter taking focus, and starts scanning the runtime unless it was scanned or
    /// is being scanned: the first time the dialog opens, and again after a scan failed. The dialog draws it
    /// scanning from this frame on. Returns the work <see cref="FrameworkBrowser.ScanAsync"/> scans as, or null when
    /// there is nothing to scan.</summary>
    public WorkTicket? Open()
    {
        IsOpen = true;
        return Browser.Open();
    }

    /// <summary>While open, takes in the filter typed and the assemblies open; see
    /// <see cref="FrameworkBrowser.Update"/>.</summary>
    public void Update(AssemblyList assemblies)
    {
        if (!IsOpen)
        {
            return;
        }

        Browser.Update(assemblies, Filter);
    }
}
