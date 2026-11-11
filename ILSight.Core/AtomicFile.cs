namespace ILSight.Core;

/// <summary>Writes a file whole or not at all: the content goes to a temporary file beside it, which then
/// replaces the file, so a write that fails or is cancelled leaves what was there and nothing else.</summary>
public static class AtomicFile
{
    /// <summary>Writes the file at <paramref name="path"/> with what <paramref name="write"/> puts in the
    /// stream it is given. The file is replaced only once <paramref name="write"/> returns.</summary>
    public static void Write(string path, Action<Stream> write)
    {
        var temporary = path + ".tmp";
        try
        {
            using (var file = CreateTemporary(temporary))
            {
                write(file);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>Writes the file at <paramref name="path"/> with what <paramref name="writeAsync"/> puts in the
    /// stream it is given. The file is replaced only once <paramref name="writeAsync"/> completes.</summary>
    public static async Task WriteAsync(string path, Func<Stream, CancellationToken, Task> writeAsync, CancellationToken cancellationToken)
    {
        var temporary = path + ".tmp";
        try
        {
            await using (var file = CreateTemporary(temporary))
            {
                await writeAsync(file, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static FileStream CreateTemporary(string temporary)
    {
        return new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
    }
}
