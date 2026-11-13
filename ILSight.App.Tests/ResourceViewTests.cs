using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.ExportArea;

namespace ILSight.App.Tests;

public sealed class ResourceViewTests
{
    private static async Task<(AppDriver App, DocumentTab Tab)> ShowResourceAsync(params string[] path)
    {
        var app = AppDriver.Opened(width: 1400f);
        app.Show(await app.NodeAsync(["Resources", .. path]), 3);
        var tab = app.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        return (app, tab);
    }

    private static string ImageOf(AppDriver app, DocumentTab tab)
    {
        return DocumentPanels.GetView(app.Ui.Documents, tab).ImageId;
    }

    [Fact]
    public async Task A_picture_shows_at_its_size_under_what_it_is()
    {
        var (app, tab) = await ShowResourceAsync("logo.png");

        var drawn = Assert.Single(app.Frames.Canvas.Images, i => i.Inputs.ImageId == ImageOf(app, tab));

        Assert.NotNull(tab.Image);
        Assert.Equal((2f, 2f), (drawn.Inputs.Position.Width, drawn.Inputs.Position.Height));
        app.TextRect("PNG, 2 × 2 pixels");
        var export = app.TextRect("Export");
        var icon = app.Frames.TextRect(BuiltInGlyphs.Text(BuiltInGlyphs.Icons.ResourceExport));
        Assert.True(icon.Right <= export.X);
        Assert.Equal(export.Y + export.Height / 2, icon.Y + icon.Height / 2, 2f);
    }

    [Fact]
    public async Task A_picture_in_a_resources_file_shows_too()
    {
        var (app, tab) = await ShowResourceAsync("Mixed.resources", "Picture.png");

        Assert.NotNull(tab.Image);
        Assert.Equal((2, 2), (tab.Image.Width, tab.Image.Height));
        Assert.Contains(app.Frames.Canvas.Images, i => i.Inputs.ImageId == ImageOf(app, tab));
    }

    private static (float Width, float Height) DrawnSize(AppDriver app, DocumentTab tab)
    {
        var drawn = Assert.Single(app.Frames.Canvas.Images, i => i.Inputs.ImageId == ImageOf(app, tab));
        return (drawn.Inputs.Position.Width, drawn.Inputs.Position.Height);
    }

    private static (int Width, int Height) UploadedSize(AppDriver app, DocumentTab tab)
    {
        return (app.Frames.Canvas.GetImageWidth(ImageOf(app, tab)), app.Frames.Canvas.GetImageHeight(ImageOf(app, tab)));
    }

    [Fact]
    public async Task Zoom_buttons_draw_the_picture_larger_and_back_at_its_own_size()
    {
        var (app, tab) = await ShowResourceAsync("logo.png");

        app.Frames.ClickOn(Selector.AtPath("dock/document-1/zoom-in")).Frame();
        app.Frames.ClickOn(Selector.AtPath("dock/document-1/zoom-in")).Frame();
        var zoomed = DrawnSize(app, tab);
        app.TextRect("400%");
        app.Frames.ClickOn(Selector.AtPath("dock/document-1/zoom-reset")).Frame();

        Assert.Equal((8f, 8f), zoomed);
        Assert.Equal((2f, 2f), DrawnSize(app, tab));
        Assert.Equal((2, 2), UploadedSize(app, tab));
    }

    [Fact]
    public async Task A_zoomed_svg_is_drawn_again_at_the_larger_size()
    {
        var (app, tab) = await ShowResourceAsync("shape.svg");

        app.Frames.ClickOn(Selector.AtPath("dock/document-1/zoom-in")).Frame();

        Assert.Equal((8f, 4f), DrawnSize(app, tab));
        Assert.Equal((8, 4), UploadedSize(app, tab));
    }

    [Fact]
    public async Task A_zoom_lasts_until_another_picture_arrives_in_the_tab()
    {
        var (app, tab) = await ShowResourceAsync("shape.svg");
        app.Frames.ClickOn(Selector.AtPath("dock/document-1/zoom-in")).Frame();
        var zoomedBefore = !DocumentPanels.GetView(app.Ui.Documents, tab).Picture.Zoom.IsOwnSize;

        app.Show(await app.NodeAsync(["Resources", "logo.png"]), 3);

        Assert.True(zoomedBefore);
        Assert.True(DocumentPanels.GetView(app.Ui.Documents, tab).Picture.Zoom.IsOwnSize);
        Assert.Equal((2f, 2f), DrawnSize(app, tab));
    }

    [Fact]
    public async Task An_svg_switches_to_its_markup_and_back()
    {
        var (app, tab) = await ShowResourceAsync("shape.svg");

        app.Frames.ClickOn(Selector.WithText("Source")).Frame(2);
        var showsSource = DocumentPanels.GetView(app.Ui.Documents, tab).Picture.ShowsSource;
        var drewPicture = app.Frames.Canvas.Images.Any(i => i.Inputs.ImageId == ImageOf(app, tab));
        app.Frames.ClickOn(Selector.WithText("Picture")).Frame(2);

        Assert.True(showsSource);
        Assert.False(drewPicture);
        Assert.Contains("<svg", Documents.GetText(tab.Document));
        Assert.False(DocumentPanels.GetView(app.Ui.Documents, tab).Picture.ShowsSource);
        Assert.Equal((4f, 2f), DrawnSize(app, tab));
    }

    [Fact]
    public async Task A_raster_picture_offers_no_markup()
    {
        var (app, _) = await ShowResourceAsync("logo.png");

        Assert.False(app.Frames.Drew("Source"));
    }

    [Fact]
    public async Task Closing_its_tab_frees_the_picture()
    {
        var (app, tab) = await ShowResourceAsync("logo.png");
        var image = ImageOf(app, tab);
        Assert.True(app.Frames.Canvas.HasImage(image));

        app.AppState.Navigation.CloseTab(tab);
        app.Run(2);

        Assert.False(app.Frames.Canvas.HasImage(image));
    }

    [Fact]
    public async Task A_picture_that_cannot_be_read_shows_why_and_its_bytes()
    {
        var (_, tab) = await ShowResourceAsync("broken.png");

        Assert.Null(tab.Image);
        var text = Documents.GetText(tab.Document);
        Assert.StartsWith("// This is no PNG, JPEG, BMP, GIF, ICO, CUR or SVG picture.", text);
        Assert.Contains("not a picture", text);
    }

    [Fact]
    public async Task A_resources_file_shows_its_entries_as_a_grid()
    {
        var (app, tab) = await ShowResourceAsync("Mixed.resources");

        Assert.NotNull(tab.Grid);
        Assert.Equal(["Name", "Value", "Type"], tab.Grid.Columns.Select(c => c.Name));
        app.TextRect("Greeting");
        app.TextRect("Hello");
    }

    [Fact]
    public async Task A_multi_line_string_shows_its_first_line_and_how_many_more_follow()
    {
        var (app, _) = await ShowResourceAsync("Mixed.resources");
        var first = app.TextRect("first line");
        var more = app.TextRect("+1 lines");

        Assert.False(app.Frames.Drew("second line"));
        Assert.True(more.X > first.X + first.Width);
        Assert.Equal(first.Y, more.Y, 4f);
    }

    private static void Click(AppDriver app, UiRect at)
    {
        app.Frames.Wait(1).Click(at).Frame(2);
    }

    [Fact]
    public async Task Clicking_a_multi_line_string_grows_its_cell_to_show_every_line_as_the_cell_shows_its_first()
    {
        var (app, _) = await ShowResourceAsync("Mixed.resources");
        var theme = app.Theme;
        var before = app.TextRect("first line");
        var color = app.Frames.TextColor("first line");

        Click(app, before);

        var first = app.TextRect("first line");
        var second = app.TextRect("second line");
        Assert.Equal((before.X, before.Y), (first.X, first.Y));
        Assert.Equal(first.X, second.X);
        Assert.Equal(theme.RowHeight, MathF.Abs(second.Y - first.Y), 0.5f);
        Assert.Equal(color, app.Frames.TextColor("second line"));
        Assert.Equal(2 * theme.RowHeight, MathF.Abs(app.TextRect("Notes.xml").Y - app.TextRect("Multiline").Y), 0.5f);
        Assert.False(app.Frames.Drew("+1 lines"));
    }

    [Fact]
    public async Task Escape_stops_editing_an_open_string_then_closes_it()
    {
        var (app, _) = await ShowResourceAsync("Mixed.resources");

        Click(app, app.TextRect("first line"));
        app.Frames.Key(KeyboardKey.F2).Frame(2);
        app.Frames.Key(KeyboardKey.Escape).Frame(2);
        Assert.True(app.Frames.Drew("second line"));
        app.Frames.Key(KeyboardKey.Escape).Frame(2);
        Assert.False(app.Frames.Drew("second line"));
    }

    [Fact]
    public async Task Several_strings_stay_open_while_other_cells_are_selected_and_no_row_moves_under_the_pointer()
    {
        var (app, _) = await ShowResourceAsync("Mixed.resources");

        Click(app, app.TextRect("first line"));
        var roses = app.TextRect("roses");
        Click(app, roses);
        Assert.Equal(roses.Y, app.TextRect("roses").Y, 0.5f);
        var greeting = app.TextRect("Greeting");
        Click(app, greeting);

        Assert.Equal(greeting.Y, app.TextRect("Greeting").Y, 0.5f);
        Assert.True(app.Frames.Drew("second line"));
        Assert.True(app.Frames.Drew("violets"));
    }

    [Fact]
    public async Task A_click_selects_a_whole_multi_line_string_for_Ctrl_C_and_a_double_click_a_word_of_a_name()
    {
        var (app, _) = await ShowResourceAsync("Mixed.resources");

        Click(app, app.TextRect("first line"));
        app.Frames.Key(Shortcuts.Copy).Frame();
        Assert.Equal("first line\nsecond line", app.Frames.Clipboard.Text);

        var name = app.TextRect("Notes.xml");
        app.Frames.Wait(1).DoubleClick(name with { Width = name.Width / 3 }).Frame(2);
        app.Frames.Key(Shortcuts.Copy).Frame();
        Assert.Equal("Notes", app.Frames.Clipboard.Text);
    }

    [Fact]
    public async Task Saving_a_resource_writes_its_bytes()
    {
        var (app, _) = await ShowResourceAsync("data.json");
        var node = await app.NodeAsync("Resources", "data.json");
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.Path);
        var path = temp.PathOf("data.json");

        app.Driver.InTurn(() => ExportCommands.SaveResource(app.AppState, node, path));
        app.Settle();

        Assert.Equal(BrowseNodes.ReadResource(node), await File.ReadAllBytesAsync(path));
        Assert.Empty(app.AppState.Errors.Entries);
    }

    [Fact]
    public async Task Saving_a_resource_waits_off_the_frame_counted_as_work_under_way_until_written()
    {
        var (app, _) = await ShowResourceAsync("data.json");
        var node = await app.NodeAsync("Resources", "data.json");
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.Path);
        var path = temp.PathOf("data.json");

        app.Driver.InTurn(() => ExportCommands.SaveResource(app.AppState, node, path));
        var underWay = app.AppState.Work.Outstanding.ToList();
        var writtenInTheTurn = File.Exists(path);
        app.Settle();

        Assert.Contains(nameof(SaveResourceHandler), underWay);
        Assert.False(writtenInTheTurn);
        Assert.Equal(BrowseNodes.ReadResource(node), await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Saving_a_resource_whose_assembly_closed_before_it_was_read_says_why_in_the_language_shown_and_reports_no_error()
    {
        var (app, _) = await ShowResourceAsync("data.json");
        var node = await app.NodeAsync("Resources", "data.json");
        var module = Assert.IsType<LoadedModule>(node.Assembly);
        using var temp = new TempDir();
        Directory.CreateDirectory(temp.Path);
        var path = temp.PathOf("data.json");
        app.AppState.Locale = Locale.De;
        var t = Strings.For(Locale.De);

        app.Driver.InTurn(() => CommandArea.Commands.CloseAssemblies(app.AppState, [module]));
        app.Driver.InTurn(() => ExportCommands.SaveResource(app.AppState, node, path));
        app.Settle();
        app.Run(2);

        Assert.True(app.Frames.Drew(t.ErrorSaveResource("data.json")));
        Assert.True(app.Frames.Drew(t.DocumentClosed(module.FilePath.FileName)));
        Assert.Empty(app.AppState.Errors.Entries);
        Assert.False(File.Exists(path));
    }
}
