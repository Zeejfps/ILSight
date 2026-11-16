using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class OneLineTextTests
{
    [Fact]
    public void A_name_holding_a_line_break_and_one_holding_a_backslash_and_n_read_differently_each_on_one_line()
    {
        var broken = OneLineText.Name("Shop\nCart");
        var escaped = OneLineText.Name(@"Shop\nCart");

        Assert.Equal(@"""Shop\nCart""", broken);
        Assert.Equal(@"Shop\nCart", escaped);
        Assert.NotEqual(broken, escaped);
        Assert.Equal(@"""Shop\\n\nCart""", OneLineText.Name("Shop\\n\nCart"));
    }

    [Fact]
    public void A_windows_path_reads_as_it_is_and_a_name_starting_with_a_quote_is_quoted_so_it_reads_as_no_escaped_name()
    {
        Assert.Equal(@"C:\Users\me\bin\Debug\net8.0\Shop.dll", OneLineText.Name(@"C:\Users\me\bin\Debug\net8.0\Shop.dll"));
        Assert.Equal(@"""\""Shop\\nCart\""""", OneLineText.Name(@"""Shop\nCart"""));
        Assert.Equal(@"""\u000B \u2028 \t \r""", OneLineText.Name("\v \u2028 \t \r"));
    }

    [Fact]
    public void A_name_holding_a_bidi_override_is_quoted_with_it_escaped_and_a_message_shows_it_escaped_so_nothing_after_it_reads_reversed()
    {
        var name = OneLineText.Name("Shop\u202Etxt.exe");
        var message = OneLineText.Message("Cannot read Shop\u202Etxt.exe.");

        Assert.Equal(@"""Shop\u202Etxt.exe""", name);
        Assert.Equal(@"Cannot read Shop\u202Etxt.exe.", message);
        Assert.DoesNotContain('\u202E', name + message);
        Assert.Equal(@"""a\u200Bb""", OneLineText.Name("a\u200Bb"));
        Assert.Equal("Shop.Cart", OneLineText.Name("Shop.Cart"));
        Assert.Equal("Café Straße", OneLineText.Name("Café Straße"));
    }

    [Fact]
    public void A_name_holding_a_line_separator_is_quoted_with_it_escaped_and_a_message_runs_on_past_it()
    {
        var name = OneLineText.Name("Shop\u2028Cart");
        var message = OneLineText.Message("Cannot read\u2028Shop.dll.");

        Assert.Equal(@"""Shop\u2028Cart""", name);
        Assert.Equal("Cannot read Shop.dll.", message);
        Assert.DoesNotContain('\u2028', name + message);
    }

    [Fact]
    public void A_message_runs_on_one_line_its_backslashes_as_they_are()
    {
        Assert.Equal(@"Cannot read C:\temp\Shop.dll: the file ends early. \u000B", OneLineText.Message("Cannot read C:\\temp\\Shop.dll:\r\nthe file ends early. \v"));
    }

    [Theory]
    [MemberData(nameof(LocalizationTests.AllLocales), MemberType = typeof(LocalizationTests))]
    public void A_diagrams_tab_title_and_a_refusals_status_line_keep_a_name_on_their_line_and_tell_a_line_break_from_a_backslash_and_n(Locale locale)
    {
        var t = Strings.For(locale);

        var brokenTitle = new TabTitle.Diagram("Shop\nCart").Words(t);
        var escapedTitle = new TabTitle.Diagram(@"Shop\nCart").Words(t);
        var brokenRefusal = new Refusal.IdNotFound { Id = "T:Shop\nCart" }.Words(t).What;
        var escapedRefusal = new Refusal.IdNotFound { Id = @"T:Shop\nCart" }.Words(t).What;

        foreach (var line in new[] { brokenTitle, escapedTitle, brokenRefusal, escapedRefusal })
        {
            Assert.DoesNotMatch("[\r\n]", line);
        }

        Assert.NotEqual(brokenTitle, escapedTitle);
        Assert.NotEqual(brokenRefusal, escapedRefusal);
        Assert.Contains(@"""Shop\nCart""", brokenTitle);
    }
}
