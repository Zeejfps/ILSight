using System.Text;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class AtomicFileTests : IDisposable
{
    private static readonly byte[] Old = Encoding.UTF8.GetBytes("old content");

    private static readonly byte[] New = Encoding.UTF8.GetBytes("new content");

    private readonly TempDir _temp = new();

    public AtomicFileTests()
    {
        Directory.CreateDirectory(_temp.Path);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void Write_NoFileYet_WritesTheContentAndNothingElse()
    {
        var path = _temp.PathOf("file.bin");

        AtomicFile.Write(path, stream => stream.Write(New));

        Assert.Equal(New, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public void Write_FileExists_ReplacesItsContent()
    {
        var path = _temp.Write("file.bin", Old);

        AtomicFile.Write(path, stream => stream.Write(New));

        Assert.Equal(New, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public void Write_WriterFails_LeavesTheFileUnchangedAndNoTemporaryFile()
    {
        var path = _temp.Write("file.bin", Old);

        var thrown = Assert.Throws<InvalidOperationException>(() => AtomicFile.Write(path, stream =>
        {
            stream.Write(New);
            throw new InvalidOperationException("The writer failed.");
        }));

        Assert.Equal("The writer failed.", thrown.Message);
        Assert.Equal(Old, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public async Task WriteAsync_FileExists_ReplacesItsContent()
    {
        var path = _temp.Write("file.bin", Old);

        await AtomicFile.WriteAsync(path, (stream, ct) => stream.WriteAsync(New, ct).AsTask(), CancellationToken.None);

        Assert.Equal(New, await File.ReadAllBytesAsync(path));
        Assert.Equal([path], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public async Task WriteAsync_WriterFails_LeavesTheFileUnchangedAndNoTemporaryFile()
    {
        var path = _temp.Write("file.bin", Old);

        await Assert.ThrowsAsync<InvalidOperationException>(() => AtomicFile.WriteAsync(path, async (stream, ct) =>
        {
            await stream.WriteAsync(New, ct);
            throw new InvalidOperationException("The writer failed.");
        }, CancellationToken.None));

        Assert.Equal(Old, await File.ReadAllBytesAsync(path));
        Assert.Equal([path], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public async Task WriteAsync_Cancelled_LeavesTheFileUnchangedAndNoTemporaryFile()
    {
        var path = _temp.Write("file.bin", Old);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AtomicFile.WriteAsync(path, (stream, ct) => stream.WriteAsync(New, ct).AsTask(), cancelled.Token));

        Assert.Equal(Old, await File.ReadAllBytesAsync(path));
        Assert.Equal([path], Directory.GetFiles(_temp.Path));
    }
}
