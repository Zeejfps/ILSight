using ILSight.ILSightAppArea;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class WindowCloserTests
{
    private static readonly SettingsWriteResult Written = new SettingsWriteResult.Written();
    private static readonly SettingsWriteResult Failed = new SettingsWriteResult.Failed("settings.json\nThe file is in use.");

    [Fact]
    public void A_close_whose_settings_were_saved_closes()
    {
        var closing = new WindowCloser();

        Assert.True(closing.TryClose(Written));
    }

    [Fact]
    public void A_close_whose_settings_were_not_saved_is_refused_and_the_next_closes_saved_or_not()
    {
        var unsavedThenUnsaved = new WindowCloser();
        var unsavedThenSaved = new WindowCloser();

        Assert.False(unsavedThenUnsaved.TryClose(Failed));
        Assert.True(unsavedThenUnsaved.TryClose(Failed));
        Assert.False(unsavedThenSaved.TryClose(Failed));
        Assert.True(unsavedThenSaved.TryClose(Written));
    }

    [Fact]
    public void A_failed_save_after_a_close_was_let_through_is_refused_again()
    {
        var closing = new WindowCloser();
        Assert.False(closing.TryClose(Failed));
        Assert.True(closing.TryClose(Failed));

        Assert.False(closing.TryClose(Failed));
    }
}
