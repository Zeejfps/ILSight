using System.Text;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight.Tests;

public sealed class ResourceTests
{
    private static async Task<BrowseNode> ResourcesAsync(LoadedAssembly assembly)
    {
        return await assembly.ReadRootAsync().GetChildAsync("Resources");
    }

    private static byte[] Fixture(string name)
    {
        using var stream = typeof(ResourceTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"No resource is embedded as '{name}'.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>The pixels the fixture pictures hold, top-down: red, green / blue, half-transparent white.</summary>
    private static readonly byte[] Expected = [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 128];

    [Theory]
    [InlineData("logo.png", "PNG")]
    [InlineData("logo.ico", "ICO")]
    [InlineData("pointer.cur", "CUR")]
    public void Pictures_decode_to_their_pixels(string name, string format)
    {
        var image = Assert.IsType<ResourceImageRead.Decoded>(ResourceImages.DecodeImage(Fixture(name))).Image;

        Assert.Equal((2, 2, format), (image.Width, image.Height, image.Format));
        Assert.Equal(Expected, image.Rgba);
    }

    [Fact]
    public void A_bitmap_without_alpha_is_opaque()
    {
        var image = Assert.IsType<ResourceImageRead.Decoded>(ResourceImages.DecodeImage(Fixture("logo.bmp"))).Image;

        Assert.Equal((2, 2, "BMP"), (image.Width, image.Height, image.Format));
        Assert.Equal(Expected[..12], image.Rgba[..12]);
        Assert.Equal([255, 255, 255, 255], image.Rgba[12..]);
    }

    [Fact]
    public void A_gif_decodes_its_first_frame()
    {
        var image = Assert.IsType<ResourceImageRead.Decoded>(ResourceImages.DecodeImage(Fixture("red.gif"))).Image;

        Assert.Equal((1, 1, "GIF"), (image.Width, image.Height, image.Format));
        Assert.Equal([255, 0, 0, 255], image.Rgba);
    }

    [Fact]
    public void A_jpeg_decodes_through_JpegSharp()
    {
        var pixels = Enumerable.Repeat(unchecked((int)0x2040A0FF), 16 * 16).ToArray();
        var data = JpegSharp.Api.Jpeg.Encode(JpegSharp.Api.JpegImage.CreateFromRgba8888(16, 16, pixels));

        var image = Assert.IsType<ResourceImageRead.Decoded>(ResourceImages.DecodeImage(data)).Image;

        Assert.Equal((16, 16, "JPEG"), (image.Width, image.Height, image.Format));
        Assert.InRange(image.Rgba[2], 0x90, 0xB0);
        Assert.Equal(255, image.Rgba[3]);
    }

    [Fact]
    public void An_svg_draws_at_its_own_size()
    {
        var image = Assert.IsType<ResourceImageRead.Decoded>(ResourceImages.DecodeImage(Fixture("shape.svg"))).Image;

        Assert.Equal((4, 2, "SVG"), (image.Width, image.Height, image.Format));
        Assert.Equal([255, 0, 0, 255], image.Rgba[..4]);
        Assert.Equal([0, 0, 255, 255], image.Rgba[^4..]);
    }

    [Fact]
    public async Task An_svg_resource_shows_as_a_picture()
    {
        var svg = await ResourcesAsync(Trees.TestAssembly).GetChildAsync("shape.svg");

        Assert.True(svg.IsImage);
        Assert.Equal("SVG", Assert.IsType<NodeContent.Picture>(await svg.ReadContentAsync()).Image.Format);
    }

    [Fact]
    public void Xml_on_one_line_is_indented_with_its_declaration_and_comments_kept()
    {
        var text = ResourceText.IndentOneLine("<?xml version=\"1.0\"?><root><!-- note --><item a=\"1\" /></root>", ResourceSyntax.Xml);

        Assert.Equal("<?xml version=\"1.0\"?>\n<root>\n    <!-- note -->\n    <item a=\"1\" />\n</root>", text);
    }

    [Fact]
    public void Json_on_one_line_is_indented_with_its_characters_unescaped()
    {
        var text = ResourceText.IndentOneLine("{\"name\":\"café\",\"list\":[1,2]}", ResourceSyntax.Json);

        Assert.Equal("{\n    \"name\": \"café\",\n    \"list\": [\n        1,\n        2\n    ]\n}", text);
    }

    [Theory]
    [InlineData("<root>\n<item/></root>")]
    [InlineData("<root><unclosed></root>")]
    public void Xml_on_several_lines_or_that_does_not_parse_stays_as_it_is(string text)
    {
        Assert.Equal(text, ResourceText.IndentOneLine(text, ResourceSyntax.Xml));
    }

    [Fact]
    public void Anything_else_is_no_picture()
    {
        var blob = Fixture("blob.bin");

        Assert.IsType<ResourceImageRead.Unreadable>(ResourceImages.DecodeImage(blob));
        Assert.IsType<ResourceImageRead.Unreadable>(ResourceImages.DecodeImage([0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2]));
    }

    [Fact]
    public async Task Pictures_show_as_pictures_and_the_rest_as_documents()
    {
        var assembly = Trees.TestAssembly;
        var resources = await ResourcesAsync(assembly);

        Assert.True((await resources.GetChildAsync("logo.png")).IsImage);
        Assert.IsType<NodeContent.Picture>(await resources.GetChildAsync("red.gif").ReadContentAsync());
        Assert.False((await resources.GetChildAsync("red.gif")).IsCode);
        Assert.IsType<NodeContent.Code>(await resources.GetChildAsync("sample.xml").ReadContentAsync());
        Assert.True((await resources.GetChildAsync("sample.xml")).IsCode);
        Assert.IsType<NodeSubject.ResourceContainer>((await resources.GetChildAsync("Mixed.resources")).Subject);
        Assert.All(await resources.ListChildrenAsync(), c => Assert.True(c.HasResource));

        var image = Assert.IsType<NodeContent.Picture>(await resources.GetChildAsync("logo.png").ReadContentAsync()).Image;
        Assert.Equal(Expected, image.Rgba);
        Assert.Equal(Fixture("logo.png"), BrowseNodes.ReadResource(await resources.GetChildAsync("logo.png")));
    }

    [Fact]
    public async Task A_picture_that_cannot_be_read_says_so()
    {
        var assembly = Trees.TestAssembly;
        var broken = await ResourcesAsync(assembly).GetChildAsync("broken.png");

        var document = Assert.IsType<NodeContent.Code>(await broken.ReadContentAsync(DecompilationLanguage.IL)).Document;

        Assert.False(broken.IsCode);
        Assert.Equal("// This is no PNG, JPEG, BMP, GIF, ICO, CUR or SVG picture.", document.Lines[0].Text);
        Assert.Equal(SpanKind.Comment, document.Lines[0].Spans[0].Kind);
        Assert.Contains("not a picture", Documents.GetText(document));
    }

    [Fact]
    public async Task Xml_is_colored_as_markup()
    {
        var assembly = Trees.TestAssembly;

        var document = await ResourcesAsync(assembly).GetChildAsync("sample.xml").DecompileAsync(DecompilationLanguage.CSharp);
        var spans = document.SpansOf().ToList();

        Assert.StartsWith("<?xml version=\"1.0\"?>", Documents.GetText(document));
        Assert.Contains(("<?xml version=\"1.0\"?>", SpanKind.Preprocessor), spans.Select(s => (s.Text, s.Span.Kind)));
        Assert.Contains(("<!-- a sample -->", SpanKind.Comment), spans.Select(s => (s.Text, s.Span.Kind)));
        Assert.Contains(("root", SpanKind.Keyword), spans.Select(s => (s.Text, s.Span.Kind)));
        Assert.Contains(("kind", SpanKind.Field), spans.Select(s => (s.Text, s.Span.Kind)));
        Assert.Contains(("\"sample\"", SpanKind.String), spans.Select(s => (s.Text, s.Span.Kind)));
    }

    [Fact]
    public async Task Json_is_colored_by_what_each_token_is()
    {
        var assembly = Trees.TestAssembly;

        var spans = (await ResourcesAsync(assembly).GetChildAsync("data.json").DecompileAsync(DecompilationLanguage.CSharp)).SpansOf().Select(s => (s.Text, s.Span.Kind)).ToList();

        Assert.Contains(("\"name\"", SpanKind.Field), spans);
        Assert.Contains(("\"fixture\"", SpanKind.String), spans);
        Assert.Contains(("2", SpanKind.Number), spans);
        Assert.Contains(("true", SpanKind.Keyword), spans);
    }

    [Fact]
    public async Task Binary_resources_show_as_a_hex_dump()
    {
        var assembly = Trees.TestAssembly;

        var text = Documents.GetText(await ResourcesAsync(assembly).GetChildAsync("blob.bin").DecompileAsync(DecompilationLanguage.CSharp));

        Assert.Contains("blob.bin (Embedded, 6 bytes)", text);
        Assert.Contains("00 01 02 03 FF FE", text);
    }

    [Fact]
    public async Task A_resources_file_lists_its_entries_in_a_grid_and_its_byte_entries_as_nodes()
    {
        var assembly = Trees.TestAssembly;
        var file = await ResourcesAsync(assembly).GetChildAsync("Mixed.resources");

        var grid = Assert.IsType<NodeContent.Table>(await file.ReadContentAsync()).Grid;
        var rows = Enumerable.Range(0, grid.RowCount).Select(r => (MetadataGrids.GetText(grid, r, 0), MetadataGrids.GetText(grid, r, 1), MetadataGrids.GetText(grid, r, 2))).ToList();

        Assert.Equal(["Name", "Value", "Type"], grid.Columns.Select(c => c.Name));
        Assert.Equal(
            [
                ("Count", "42", "System.Int32"), ("Greeting", "Hello", "System.String"), ("Multiline", "first line\nsecond line", "System.String"),
                ("Notes.xml", "95 bytes", "System.Byte[]"), ("Picture.png", "76 bytes", "System.Byte[]"),
                ("Poem", "roses\nviolets\nsugar", "System.String"),
            ],
            rows);
        Assert.Equal("first line\nsecond line", MetadataGrids.FindTooltip(grid, 2, 1)?.Text);
        Assert.Equal(("first line", 1), MetadataGrids.GetLines(grid, 2, 1));
        Assert.Equal(("Hello", 0), MetadataGrids.GetLines(grid, 1, 1));
        Assert.False(grid.HasDetails);
        Assert.Null(MetadataGrids.FindTooltip(grid, 1, 1));
        Assert.Equal([("Notes.xml", false), ("Picture.png", true)], (await file.ListChildrenAsync()).Select(static c => (c.Text, c.IsImage)));
        Assert.Equal(Expected, Assert.IsType<NodeContent.Picture>(await file.GetChildAsync("Picture.png").ReadContentAsync()).Image.Rgba);
        Assert.Contains(("root", SpanKind.Keyword), (await file.GetChildAsync("Notes.xml").DecompileAsync(DecompilationLanguage.CSharp)).SpansOf().Select(s => (s.Text, s.Span.Kind)));
    }

    [Fact]
    public async Task A_resources_file_is_its_bytes_its_entries_and_a_grid_at_once()
    {
        var file = await ResourcesAsync(Trees.TestAssembly).GetChildAsync("Mixed.resources");

        Assert.True(file.HasResource);
        Assert.Equal(Fixture("Mixed.resources"), BrowseNodes.ReadResource(file));
        Assert.Equal("Mixed.resources", file.ResourceFileName);
        Assert.True(file.HasChildren);
        Assert.IsType<NodeContent.Table>(await file.ReadContentAsync(DecompilationLanguage.IL));
        Assert.False(file.IsCode);
        Assert.Null(file.FolderInPackage);
    }

    [Fact]
    public async Task Baml_shows_as_xaml_whatever_the_language()
    {
        var baml = await MainWindowBamlAsync();

        var document = Assert.IsType<NodeContent.Code>(await baml.ReadContentAsync(DecompilationLanguage.IL)).Document;

        Assert.False(baml.IsImage);
        Assert.True(baml.IsCode);
        Assert.Contains("x:Class=\"SampleApp.MainWindow\"", Documents.GetText(document));
    }

    private static async Task<BrowseNode> MainWindowBamlAsync()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.SampleAppPath);
        return await ResourcesAsync(assembly).GetChildAsync("SampleApp.g.resources").GetChildAsync("mainwindow.baml");
    }

    [Fact]
    public async Task Baml_shows_as_the_xaml_it_was_compiled_from()
    {
        var document = await MainWindowBamlAsync().DecompileAsync(DecompilationLanguage.CSharp);
        var text = Documents.GetText(document);

        Assert.Contains("x:Class=\"SampleApp.MainWindow\"", text);
        Assert.Contains("Text=\"Hello from BAML\"", text);
        Assert.Contains(("StackPanel", SpanKind.Keyword), document.SpansOf().Select(s => (s.Text, s.Span.Kind)));
    }

    [Fact]
    public async Task Baml_saves_as_xaml()
    {
        var baml = await MainWindowBamlAsync();

        var saved = Encoding.UTF8.GetString(BrowseNodes.ReadResource(baml));

        Assert.Equal("mainwindow.xaml", baml.ResourceFileName);
        Assert.Contains("x:Class=\"SampleApp.MainWindow\"", saved);
    }

    private static async Task<(int Width, int Height, string Format)> PictureAsync(BrowseNode node)
    {
        var image = Assert.IsType<NodeContent.Picture>(await node.ReadContentAsync()).Image;
        return (image.Width, image.Height, image.Format);
    }

    [Fact]
    public async Task Avalonia_assets_unpack_into_the_tree_under_their_paths()
    {
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.AvaloniaSamplePath);
        var container = await ResourcesAsync(assembly).GetChildAsync("!AvaloniaResources");

        var entries = (await container.ListChildrenAsync()).Select(static c => (c.Text, c.IsImage)).ToList();

        Assert.Equal(
            [
                ("/!AvaloniaResourceXamlInfo", false), ("/Assets/icon.svg", true),
                ("/Assets/logo.png", true), ("/Assets/notes.json", false),
            ],
            entries);
        Assert.Equal((64, 64, "SVG"), await PictureAsync(await container.GetChildAsync("/Assets/icon.svg")));
        Assert.Equal(Expected, Assert.IsType<NodeContent.Picture>(await container.GetChildAsync("/Assets/logo.png").ReadContentAsync()).Image.Rgba);
        Assert.Contains("\"sample\"", Documents.GetText(await container.GetChildAsync("/Assets/notes.json").DecompileAsync(DecompilationLanguage.CSharp)));
        var xamlInfo = await container.GetChildAsync("/!AvaloniaResourceXamlInfo").DecompileAsync(DecompilationLanguage.CSharp);
        Assert.Contains("/MainWindow.axaml", Documents.GetText(xamlInfo));
        Assert.True(xamlInfo.Lines.Length > 5);
    }

    [Fact]
    public async Task Avalonia_0_10_assets_unpack_from_their_xml_index()
    {
        var container = await ResourcesAsync(Trees.TestAssembly).GetChildAsync("!AvaloniaResources");

        Assert.Equal(["/!AvaloniaResourceXamlInfo", "/Assets/logo.png", "/Assets/notes.json"], (await container.ListChildrenAsync()).Select(c => c.Text));
        Assert.Equal(Expected, Assert.IsType<NodeContent.Picture>(await container.GetChildAsync("/Assets/logo.png").ReadContentAsync()).Image.Rgba);
    }

    [Fact]
    public async Task An_avalonia_container_lists_its_assets()
    {
        var container = await ResourcesAsync(Trees.TestAssembly).GetChildAsync("!AvaloniaResources");

        var text = Documents.GetText(await container.DecompileAsync(DecompilationLanguage.CSharp));

        Assert.Contains("// /Assets/logo.png (76 bytes)", text);
        Assert.Contains("// /Assets/notes.json (", text);
    }

    [Fact]
    public void An_avalonia_container_of_an_unknown_version_cannot_be_read()
    {
        byte[] container = [8, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 0];

        var error = Assert.Throws<InvalidDataException>(() => AvaloniaResources.ReadEntries(container));

        Assert.Contains("unknown version 9", error.Message);
    }

    [Fact]
    public async Task The_resources_folder_describes_each_resource()
    {
        var assembly = Trees.TestAssembly;

        var text = Documents.GetText(await ResourcesAsync(assembly).DecompileAsync(DecompilationLanguage.CSharp));

        Assert.Contains("// logo.png (Embedded, 76 bytes)", text);
        Assert.Contains("// Mixed.resources (Embedded, ", text);
    }
}
