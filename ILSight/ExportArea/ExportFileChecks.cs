namespace ILSight.ExportArea;

/// <summary>What the disk says about the folder and key file the export dialog names: looked at again, off the frame,
/// each time either name changes, and taken only while it is of the names asked for last, so the dialog never decides
/// with what the disk said about a name no longer typed. A look is being made, made, or failed, saying why; a failed one
/// is asked again only when the user retries it (<see cref="Retry"/>) or a name changes.</summary>
public sealed class ExportFileChecks
{
    private ExportFileCheck? _asked;
    private ExportFileLook _look = new ExportFileLook.Checking();

    /// <summary>How far the look at <paramref name="folder"/> and <paramref name="keyFile"/>, as typed, has got; null
    /// before one was asked for.</summary>
    public ExportFileLook? FindLook(string folder, string keyFile)
    {
        if (!HasAsked(folder, keyFile))
        {
            return null;
        }

        return _look;
    }

    /// <summary>What the disk says about <paramref name="folder"/> and <paramref name="keyFile"/>, as typed; null while
    /// it is being looked at, when the look failed, or before it was asked for.</summary>
    public ExportFileFacts? FindFacts(string folder, string keyFile)
    {
        var look = FindLook(folder, keyFile);
        if (look is not ExportFileLook.Checked looked)
        {
            return null;
        }

        return looked.Facts;
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

        return Ask(folder, keyFile);
    }

    /// <summary>Asks again for the names whose look failed, as the user retries it; returns the look to make, or null
    /// when the last look did not fail.</summary>
    public ExportFileCheck? Retry()
    {
        var asked = _asked;
        if (asked is null || _look is not ExportFileLook.Failed)
        {
            return null;
        }

        return Ask(asked.Folder, asked.KeyFile);
    }

    private ExportFileCheck Ask(string folder, string keyFile)
    {
        var check = new ExportFileCheck { Folder = folder, KeyFile = keyFile };
        _asked = check;
        _look = new ExportFileLook.Checking();
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

        _look = new ExportFileLook.Checked { Facts = facts };
    }

    /// <summary>Takes that <paramref name="check"/> failed, because <paramref name="why"/>, unless a newer check was
    /// asked for meanwhile: the dialog says why, and the look is asked again only when retried or a name changes.</summary>
    public void Fail(ExportFileCheck check, string why)
    {
        if (check != _asked)
        {
            return;
        }

        _look = new ExportFileLook.Failed { Why = why };
    }
}
