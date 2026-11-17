using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ILSight.Core;

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
        app.Open([AssemblyPath.Of(broken), AssemblyPath.Of(KeyboardPath)]);
        app.Frames.WaitUntil(() => app.AppState.ListSession.Assemblies.Assemblies.Count == 3, Waits.Patience);
        var (fixtures, middle, keyboard) = (app.AppState.ListSession.Assemblies.Assemblies[0], app.AppState.ListSession.Assemblies.Assemblies[1],
            app.AppState.ListSession.Assemblies.Assemblies[2]);
        Assert.Equal(broken, middle.FilePath.Text);
        app.Ui.Panels.OpenSearch();
        app.Run();

        app.Frames.Type("t:e");
        app.Frames.Frame();
        app.RunOut();

        var search = app.Ui.Search;
        Assert.False(search.Results.IsBusy);
        Assert.Contains(search.Results.Rows, r => r.Node.Assembly == fixtures);
        Assert.Contains(search.Results.Rows, r => r.Node.Assembly == keyboard);
        Assert.Same(middle, Assert.Single(search.Results.Unsearched).Assembly);
        Assert.Contains(app.Frames.TextRects(), t => t.Text.StartsWith("Broken.dll: couldn't be searched (", StringComparison.Ordinal));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public void A_search_of_a_module_read_in_part_shows_its_hit_and_says_the_module_partly_searched_and_the_match_not_shown()
    {
        var unreadable = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, nameof(NumberLoads), nameof(NumberLoads.Unreadable));
        var unplaced = BrokenAssemblies.GetMethod(AppStateDriver.FixturePath, nameof(Unplaced.UnplacedNumberLoad), nameof(Unplaced.UnplacedNumberLoad.Unplaced));
        var partly = BrokenAssemblies.WritePartlyReadable(_temp, AppStateDriver.FixturePath, unreadable, unplaced);
        var app = new AppDriver();
        app.Open([AssemblyPath.Of(partly)]);
        app.Frames.WaitUntil(() => app.AppState.ListSession.Assemblies.Assemblies.Count == 1, Waits.Patience);
        app.Ui.Panels.OpenSearch();
        app.Run();

        app.Frames.Type("c:7340033");
        app.Frames.Frame();
        app.RunOut();

        var results = app.Ui.Search.Results;
        Assert.False(results.IsBusy);
        Assert.StartsWith(nameof(NumberLoads.Shown), Assert.Single(results.Rows).Text, StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(results.PartlySearched).Count);
        Assert.Single(results.NotShown);
        Assert.Empty(results.Unsearched);
        var texts = app.Frames.TextRects();
        Assert.Contains(texts, t => t.Text.StartsWith("PartlyReadable.dll: partly searched, method bodies not read: 1 (", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Text.StartsWith($"0x{MetadataTokens.GetToken(unplaced):X8} in PartlyReadable.dll: found, couldn't be shown (",
            StringComparison.Ordinal));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    // The name of the first type after <Module> points past the string heap: the file opens, and reading that name
    // throws.
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
