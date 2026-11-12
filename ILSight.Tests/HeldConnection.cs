namespace ILSight.Tests;

/// <summary>A runtime's connection while it writes a rundown: a read waits until the rundown ends, and fails
/// when the connection is closed under it, as a pipe's does.</summary>
internal sealed class HeldConnection : Stream
{
    public const string ClosedUnderTheRead = "The connection was closed under the read.";

    private readonly TaskCompletionSource<byte[]> _rundown = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _ended;

    public bool IsClosed { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>The runtime writes <paramref name="bytes"/> and ends the stream.</summary>
    public void EndRundown(byte[] bytes)
    {
        _rundown.TrySetResult(bytes);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_ended)
        {
            return 0;
        }

        var bytes = await _rundown.Task;
        bytes.CopyTo(buffer);
        _ended = true;
        return bytes.Length;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        IsClosed = true;
        _rundown.TrySetException(new IOException(ClosedUnderTheRead));
        base.Dispose(disposing);
    }
}
