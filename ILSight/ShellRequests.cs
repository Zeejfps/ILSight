using System.Diagnostics.CodeAnalysis;

namespace ILSight;

/// <summary>What the features ask of the shell while the frame runs: web pages, files and folders to open, and the
/// app to quit. The features only ask; the frame edge takes the requests once the frame is drawn, in the order
/// they came, and hands them to the OS.</summary>
public sealed class ShellRequests
{
    private readonly Queue<string> _webPages = new();
    private readonly Queue<string> _files = new();
    private readonly Queue<string> _folders = new();
    private bool _quit;

    /// <summary>Asks for <paramref name="url"/> to open in the browser.</summary>
    public void OpenWebPage(string url)
    {
        _webPages.Enqueue(url);
    }

    /// <summary>Asks for <paramref name="path"/> to open in the app the OS opens it with.</summary>
    public void OpenFile(string path)
    {
        _files.Enqueue(path);
    }

    /// <summary>Asks for <paramref name="path"/> to open in the OS's file manager.</summary>
    public void OpenFolder(string path)
    {
        _folders.Enqueue(path);
    }

    /// <summary>Asks for the app to close.</summary>
    public void Quit()
    {
        _quit = true;
    }

    /// <summary>Takes the next web page asked for, if any. The frame edge's.</summary>
    internal bool TryTakeWebPage([MaybeNullWhen(false)] out string url)
    {
        return _webPages.TryDequeue(out url);
    }

    /// <summary>Takes the next file asked for, if any. The frame edge's.</summary>
    internal bool TryTakeFile([MaybeNullWhen(false)] out string path)
    {
        return _files.TryDequeue(out path);
    }

    /// <summary>Takes the next folder asked for, if any. The frame edge's.</summary>
    internal bool TryTakeFolder([MaybeNullWhen(false)] out string path)
    {
        return _folders.TryDequeue(out path);
    }

    /// <summary>Takes the request to close, if one came since the last time. The frame edge's.</summary>
    internal bool TryTakeQuit()
    {
        var quit = _quit;
        _quit = false;
        return quit;
    }
}
