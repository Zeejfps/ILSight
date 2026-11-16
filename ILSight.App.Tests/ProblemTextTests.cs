using System.Diagnostics;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ProblemTextTests
{
    private const string Frame = "   at ";

    [Fact]
    public void A_failed_comparison_says_its_cause_by_its_message_and_the_report_the_stack_trace_kept_as_technical_details()
    {
        var error = Thrown("The metadata of Shop.dll ends early.");
        var problem = new AppProblem.ComparisonFailed { Left = "Shop.dll", Right = "Shop2.dll", Error = error };

        var text = ProblemTexts.Describe(Strings.En, problem);

        Assert.Equal("Cannot compare Shop.dll with Shop2.dll", text.Title);
        Assert.Equal("The metadata of Shop.dll ends early. Copy this error, technical details included, and report it as a bug.", text.Details);
        Assert.Equal("Cannot compare Shop.dll with Shop2.dll — The metadata of Shop.dll ends early. Copy this error, technical details included, and report it as a bug.", text.Summary);
        Assert.Equal(error.ToString(), text.TechnicalDetails);
        Assert.Contains(Frame, text.TechnicalDetails);
    }

    [Theory]
    [MemberData(nameof(LocalizationTests.AllLocales), MemberType = typeof(LocalizationTests))]
    public void A_failed_decompilation_shows_no_exception_type_or_stack_frame_but_copies_them_under_their_label(Locale locale)
    {
        var t = Strings.For(locale);
        var error = Thrown("Unexpected IL at offset 0x12.");
        var problem = new AppProblem.DecompilationFailed { Node = "Shop.Cart.Sum()", Error = error };

        var text = ProblemTexts.Describe(t, problem);

        Assert.Equal(t.ErrorCauseAndRecovery(cause: "Unexpected IL at offset 0x12.", recovery: t.ErrorRecoveryReportBug), text.Details);
        foreach (var shown in new[] { text.Title, text.Details, text.Summary })
        {
            Assert.DoesNotContain(nameof(InvalidOperationException), shown);
            Assert.DoesNotContain(nameof(FormatException), shown);
            Assert.DoesNotContain(Frame, shown);
        }

        Assert.Contains(nameof(InvalidOperationException), text.TechnicalDetails);
        Assert.Contains(nameof(FormatException), text.TechnicalDetails);
        Assert.Contains(Frame, text.TechnicalDetails);
        var nl = Environment.NewLine;
        Assert.Equal($"{text.Title}{nl}{nl}{text.Details}{nl}{nl}{t.ErrorsTechnicalDetails}{nl}{error}", text.FullText);
    }

    [Theory]
    [MemberData(nameof(LocalizationTests.AllLocales), MemberType = typeof(LocalizationTests))]
    public void A_reload_failing_by_a_cause_known_only_by_its_message_says_the_reload_and_to_ask_again(Locale locale)
    {
        var t = Strings.For(locale);
        var error = new IOException("The specified network name is no longer available.");
        var problem = new AppProblem.AssemblyNotReloaded { Assembly = "Shop.dll", Error = error };

        var text = ProblemTexts.Describe(t, problem);

        Assert.Equal(t.ErrorReloadAssembly("Shop.dll"), text.Title);
        Assert.Equal(t.ErrorCauseAndRecovery(cause: error.Message, recovery: t.ErrorRecoveryTryAgain), text.Details);
    }

    [Theory]
    [MemberData(nameof(LocalizationTests.AllLocales), MemberType = typeof(LocalizationTests))]
    public void Opening_references_failing_by_any_cause_says_to_ask_again_and_to_report_it_if_it_fails_again(Locale locale)
    {
        var t = Strings.For(locale);
        var error = Thrown("Object reference not set to an instance of an object.");

        var text = ProblemTexts.Describe(t, new AppProblem.OpenFailed(error));

        Assert.Equal(t.ErrorOpenAssemblies, text.Title);
        Assert.Equal(t.ErrorCauseAndRecovery(cause: error.Message, recovery: t.ErrorRecoveryTryAgainOrReport), text.Details);
        Assert.Equal(error.ToString(), text.TechnicalDetails);
    }

    [Theory]
    [MemberData(nameof(LocalizationTests.AllLocales), MemberType = typeof(LocalizationTests))]
    public void A_layout_failing_on_the_same_diagram_says_to_report_it_never_to_try_again(Locale locale)
    {
        var t = Strings.For(locale);
        var error = Thrown("Index was outside the bounds of the array.");

        var text = ProblemTexts.Describe(t, new AppProblem.DiagramNotLaidOut(error));

        Assert.Equal(t.ErrorLayOutDiagram, text.Title);
        Assert.Equal(t.ErrorCauseAndRecovery(cause: error.Message, recovery: t.ErrorRecoveryReportBug), text.Details);
        Assert.DoesNotContain(t.ErrorRecoveryTryAgain, text.Details);
        Assert.Equal(error.ToString(), text.TechnicalDetails);
    }

    [Theory]
    [MemberData(nameof(LocalizationTests.AllLocales), MemberType = typeof(LocalizationTests))]
    public void A_nuget_config_failing_to_read_says_a_restart_reads_it_again_and_to_report_it_never_to_try_again(Locale locale)
    {
        var t = Strings.For(locale);
        var error = Thrown("Object reference not set to an instance of an object.");

        var text = ProblemTexts.Describe(t, new AppProblem.NuGetConfigUnreadable(error));

        Assert.Equal(t.CauseNugetConfigUnreadableTitle, text.Title);
        Assert.Equal(t.ErrorCauseAndRecovery(cause: error.Message, recovery: t.ErrorRecoveryFixNugetConfigOrReport), text.Details);
        Assert.DoesNotContain(t.ErrorRecoveryTryAgain, text.Details);
        Assert.Contains("ILSight", t.ErrorRecoveryFixNugetConfigOrReport);
        Assert.Equal(error.ToString(), text.TechnicalDetails);
    }

    [Fact]
    public void A_nuget_config_that_cannot_be_read_says_in_the_dialog_that_a_restart_reads_it_again()
    {
        var notice = Strings.En.CauseNugetConfigUnreadableNotice("Data at the root level is invalid.");

        Assert.Equal("Couldn’t read your NuGet.Config (Data at the root level is invalid.), so nuget.org is searched instead. "
            + "Fix it, then restart ILSight to read it again.", notice);
    }

    [Fact]
    public void A_problem_with_no_exception_has_no_technical_details_and_copies_as_before()
    {
        var text = ProblemTexts.Describe(Strings.En, new AppProblem.ResourceNotSaved { Resource = "a.png", Error = new IOException("The disk is full.") });

        Assert.Equal("", text.TechnicalDetails);
        Assert.Equal($"Cannot save a.png{Environment.NewLine}{Environment.NewLine}The disk is full.", text.FullText);
    }

    [Fact]
    public void A_bundle_file_that_cannot_be_read_is_said_from_its_cause_in_the_apps_language()
    {
        var error = new BundleEntryException(new BundleEntryFault.Outside { Entry = "lib/Shop.dll" }, "The bundle's entry 'lib/Shop.dll' cannot be read: it starts at -1.");
        var problem = new AppProblem.PackageFileNotExtracted { File = "Shop.dll", Error = error };

        var english = ProblemTexts.Describe(Strings.En, problem);
        var japanese = ProblemTexts.Describe(Strings.For(Locale.Ja), problem);

        Assert.Equal("The bundle places its file lib/Shop.dll outside the bundle.", english.Details);
        Assert.Contains("バンドルがファイル lib/Shop.dll をバンドルの外に配置しています。", japanese.Details);
    }

    [Fact]
    public void A_feed_answer_ILSight_cannot_use_is_said_from_its_cause_in_the_apps_language()
    {
        var error = new FeedFaultException(new FeedFault.ResourceMissing("SearchQueryService"), "The service index of https://example.test/v3/index.json names no SearchQueryService.");
        var problem = new AppProblem.NuGetSearchFailed { Feed = "example", Error = error };

        var english = ProblemTexts.Describe(Strings.En, problem);
        var japanese = ProblemTexts.Describe(Strings.For(Locale.Ja), problem);

        Assert.StartsWith("The feed's service index names no SearchQueryService.", english.Details);
        Assert.Contains("フィードのサービス インデックスに SearchQueryService がありません。", japanese.Details);
        Assert.DoesNotContain("example.test", english.Details);
    }

    [Fact]
    public void A_request_that_timed_out_is_said_from_its_cause_in_the_apps_language()
    {
        var error = new TimeoutException("The request timed out.", new TaskCanceledException());
        var problem = new AppProblem.NuGetSearchFailed { Feed = "example", Error = error };

        var english = ProblemTexts.Describe(Strings.En, problem);
        var japanese = ProblemTexts.Describe(Strings.For(Locale.Ja), problem);

        Assert.StartsWith("The server didn't answer in time.", english.Details);
        Assert.Contains("サーバーが時間内に応答しませんでした。", japanese.Details);
    }

    [Fact]
    public void A_file_that_cannot_be_opened_is_said_from_its_cause_and_a_bundles_why_in_the_apps_language()
    {
        var file = AssemblyPath.Of(Path.Combine(Path.GetTempPath(), "Shop", "Shop.dll"));
        var bundle = AssemblyPath.Of(Path.Combine(Path.GetTempPath(), "Shop", "Shop.exe"));
        var inner = new BundleEntryException(new BundleEntryFault.Outside { Entry = "Shop.dll" }, "The bundle's entry 'Shop.dll' cannot be read: it starts at -1.");
        var cannotOpen = new AssemblyLoadException(file, new AssemblyLoadFault.CannotOpen(), $"Cannot open '{file}': {inner.Message}", inner);
        var bySystem = new AssemblyLoadException(file, new AssemblyLoadFault.CannotOpen(), $"Cannot open '{file}': In use.", new IOException("In use."));
        var notAssembly = new AssemblyLoadException(file, new AssemblyLoadFault.NotAssembly(), $"'{file}' is not a .NET assembly.");
        var notPackage = new AssemblyLoadException(file, new AssemblyLoadFault.NotPackage { Container = bundle }, $"'{bundle}' is not a package.");
        var notInPackage = new AssemblyLoadException(file, new AssemblyLoadFault.NotInPackage { Package = bundle, FullName = "Shop.dll" }, $"'{bundle}' holds no 'Shop.dll'.");
        var japanese = Strings.For(Locale.Ja);

        Assert.Equal($"{file.Text} couldn't be opened: The bundle places its file Shop.dll outside the bundle.", FailureTexts.DescribeCause(Strings.En, cannotOpen));
        Assert.Equal($"{file.Text} を開けませんでした: バンドルがファイル Shop.dll をバンドルの外に配置しています。", FailureTexts.DescribeCause(japanese, cannotOpen));
        Assert.Equal($"{file.Text} couldn't be opened: In use.", FailureTexts.DescribeCause(Strings.En, bySystem));
        Assert.Equal($"{file.Text} は .NET アセンブリではありません。", FailureTexts.DescribeCause(japanese, notAssembly));
        Assert.Equal($"{bundle.Text} isn't a package.", FailureTexts.DescribeCause(Strings.En, notPackage));
        Assert.Equal($"{bundle.Text} holds no file Shop.dll.", FailureTexts.DescribeCause(Strings.En, notInPackage));
    }

    // An exception as thrown, with its stack trace and an inner exception.
    private static InvalidOperationException Thrown(string message)
    {
        try
        {
            ThrowNested(message);
            throw new UnreachableException();
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
    }

    private static void ThrowNested(string message)
    {
        try
        {
            throw new FormatException("Bad header.");
        }
        catch (FormatException inner)
        {
            throw new InvalidOperationException(message, inner);
        }
    }
}
