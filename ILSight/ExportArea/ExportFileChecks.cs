namespace ILSight.ExportArea;

/// <summary>What the disk says about the folder and key file the export dialog names: looked at again, off the frame,
/// each time either name changes, and taken only while it is of the names asked for last, so the dialog never decides
/// with what the disk said about a name no longer typed.</summary>
public sealed class ExportFileChecks
{
    private ExportFileCheck? _asked;
    private ExportFileFacts? _facts;

    /// <summary>What the disk says about <paramref name="folder"/> and <paramref name="keyFile"/>, as typed; null while
    /// it is being looked at, or before it was asked for.</summary>
    public ExportFileFacts? FindFacts(string folder, string keyFile)
    {
        var asked = _asked;
        if (asked is null)
        {
            return null;
        }

        if (!asked.IsOf(folder, keyFile))
        {
            return null;
        }

        return _facts;
    }

    /// <summary>Whether the last look asked for is of <paramref name="folder"/> and <paramref name="keyFile"/>, as
    /// typed, whether or not it was made yet.</summary>
    public bool HasAsked(string folder, string keyFile)
    {
        var asked = _asked;
        if (asked is null)
        {
            return false;
        }

        return asked.IsOf(folder, keyFile);
    }

    /// <summary>Asks for <paramref name="folder"/> and <paramref name="keyFile"/>, as typed, to be looked at, unless
    /// the last asked for was of them; returns the look to make, or null. What the disk said before is not taken from
    /// then on.</summary>
    public ExportFileCheck? StartCheck(string folder, string keyFile)
    {
        if (HasAsked(folder, keyFile))
        {
            return null;
        }

        var check = new ExportFileCheck { Folder = folder, KeyFile = keyFile };
        _asked = check;
        _facts = null;
        return check;
    }

    /// <summary>Takes what the disk says, <paramref name="facts"/>, as <paramref name="check"/> found it, unless a
    /// newer check was asked for meanwhile.</summary>
    public void Finish(ExportFileCheck check, ExportFileFacts facts)
    {
        if (check != _asked)
        {
            return;
        }

        _facts = facts;
    }
}
