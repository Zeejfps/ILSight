using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ILSight.Core;
using static ILSight.ScreenLayout;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class SearchPanelTests : IDisposable
{
    private static readonly string SampleAppPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleApp", "SampleApp.dll");
    private static readonly string KeyboardPath = typeof(ZGF.KeyboardModule.KeyboardKey).Assembly.Location;

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void A_search_shows_the_hits_of_the_assemblies_around_one_the_library_fails_on_and_a_line_saying_why()
    {
        var broken = _temp.Write("Broken.dll", WithUnreadableTypeName(File.ReadAllBytes(SampleAppPath)));
        var app = AppDriver.Opened();
        app.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(broken), AssemblyPath.Of(KeyboardPath)], ct));
        app.Frames.WaitUntil(() => app.AppState.Assemblies.Assemblies.Count == 3, AppStateDriver.Patience);
        var (fixtures, middle, keyboard) = (app.AppState.Assemblies.Assemblies[0], app.AppState.Assemblies.Assemblies[1],
            app.AppState.Assemblies.Assemblies[2]);
        Assert.Equal(broken, middle.FilePath.Text);
        OpenSearch(app.Ui);
        app.Run();

        app.Frames.Type("t:e").Frame();
        app.RunOut();

        var search = app.Ui.Search;
        Assert.False(search.Results.IsBusy);
        Assert.Contains(search.Results.Rows, r => r.Node.Assembly == fixtures);
        Assert.Contains(search.Results.Rows, r => r.Node.Assembly == keyboard);
        Assert.Same(middle, Assert.Single(search.Results.Unsearched).Assembly);
        Assert.Contains(app.Frames.TextRects(), t => t.Text.StartsWith("Broken.dll: couldn't be searched (", StringComparison.Ordinal));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    /// <summary><paramref name="image"/> with the name of its first type after <c>&lt;Module&gt;</c> pointing past the
    /// string heap: the file opens, and reading that name throws.</summary>
    private static byte[] WithUnreadableTypeName(byte[] image)
    {
        using var pe = new PEReader(new MemoryStream(image));
        var reader = pe.GetMetadataReader();
        var rowSize = reader.GetTableRowSize(TableIndex.TypeDef);
        var stringHeapSize = reader.GetHeapSize(HeapIndex.String);
        var stringIndexSize = stringHeapSize < 0x10000 ? 2 : 4;
        const int flagsSize = 4;
        const int secondRow = 1;
        var offset = pe.PEHeaders.MetadataStartOffset + reader.GetTableMetadataOffset(TableIndex.TypeDef) + rowSize * secondRow + flagsSize;
        image.AsSpan(offset, stringIndexSize).Fill(0xFF);
        return image;
    }
}
