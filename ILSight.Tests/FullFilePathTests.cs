using ILSight.Core;

namespace ILSight.Tests;

public sealed class FullFilePathTests
{
    private static readonly string Root = Path.GetPathRoot(Environment.CurrentDirectory) ?? "";

    [Fact]
    public void An_absolute_file_path_is_kept_with_its_folder()
    {
        var text = Path.Combine(Root, "work", "settings.json");

        var path = FullFilePath.Parse(text);

        Assert.NotNull(path);
        Assert.Equal(text, path.Text);
        Assert.Equal(Path.Combine(Root, "work"), path.Folder);
    }

    [Fact]
    public void A_file_at_the_root_has_the_root_as_its_folder()
    {
        var path = FullFilePath.Parse(Path.Combine(Root, "settings.json"));

        Assert.NotNull(path);
        Assert.Equal(Root, path.Folder);
    }

    [Fact]
    public void Steps_up_and_over_are_taken_out()
    {
        var path = FullFilePath.Parse(Path.Combine(Root, "work", "sub", "..", ".", "settings.json"));

        Assert.NotNull(path);
        Assert.Equal(Path.Combine(Root, "work", "settings.json"), path.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("settings.json")]
    [InlineData("work/settings.json")]
    public void A_path_that_is_not_fully_qualified_is_none(string text)
    {
        Assert.Null(FullFilePath.Parse(text));
    }

    [Fact]
    public void A_root_or_a_folder_ending_in_a_separator_is_no_file()
    {
        Assert.Null(FullFilePath.Parse(Root));
        Assert.Null(FullFilePath.Parse(Path.Combine(Root, "work") + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void A_path_holding_a_null_character_is_none()
    {
        Assert.Null(FullFilePath.Parse(Path.Combine(Root, "a\0.json")));
    }

    [Fact]
    public void Of_throws_on_what_is_no_path_of_a_file()
    {
        Assert.Throws<ArgumentException>(() => FullFilePath.Of("settings.json"));
        Assert.Equal(Path.Combine(Root, "settings.json"), FullFilePath.Of(Path.Combine(Root, "settings.json")).Text);
    }
}
