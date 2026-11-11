using ILSight.Core;
using static ILSight.Assemblies;
using static ILSight.App.Tests.TestPaths;

namespace ILSight.App.Tests;

public sealed class ComparisonPickerTests
{
    private static readonly string OlderPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Older", "ApiDiffSample.dll");
    private static readonly string NewerPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiDiff", "Newer", "ApiDiffSample.dll");

    /// <summary>A driver that opened the fixture assembly, then both versions of the sample library.</summary>
    private static AppStateDriver OpenThree()
    {
        var d = AppStateDriver.Opened();
        d.Start((a, ct) => OpenAssembliesAsync(a, [PathOf(OlderPath), PathOf(NewerPath)], a.AssemblyPorts, ct));
        d.RunUntil(() => d.AppState.Assemblies.Assemblies.Count == 3);
        d.Settle();
        return d;
    }

    private static LoadedModule Opened(AppStateDriver d, string path)
    {
        return d.AppState.Assemblies.Assemblies.Single(a => a.FilePath.Text == path);
    }

    private static ComparisonPicker OpenPicker(AppStateDriver d, string path)
    {
        var picker = new ComparisonPicker();
        picker.Open(Opened(d, path));
        picker.Update(d.AppState.Assemblies);
        return picker;
    }

    private static List<string> ShownPaths(ComparisonPicker picker)
    {
        return picker.Filtered
            .Select(static a => a.FilePath.Text)
            .ToList();
    }

    [Fact]
    public void It_offers_the_other_open_assemblies_those_of_the_same_name_first_and_selects_the_first()
    {
        var d = OpenThree();

        var picker = OpenPicker(d, OlderPath);

        Assert.Equal([NewerPath, AppStateDriver.FixturePath], ShownPaths(picker));
        Assert.Equal(NewerPath, picker.Selected!.FilePath.Text);
    }

    [Fact]
    public void The_filter_matches_the_name_with_its_version_or_the_path()
    {
        var d = OpenThree();
        var picker = OpenPicker(d, OlderPath);

        picker.Filter = "2.0";
        picker.Update(d.AppState.Assemblies);
        Assert.Equal([NewerPath], ShownPaths(picker));

        picker.Filter = "Newer";
        picker.Update(d.AppState.Assemblies);
        Assert.Equal([NewerPath], ShownPaths(picker));

        picker.Filter = "nothing like it";
        picker.Update(d.AppState.Assemblies);
        Assert.Empty(picker.Filtered);
        Assert.Null(picker.Selected);
    }

    [Fact]
    public void Narrowing_the_filter_keeps_the_selected_assembly_while_it_still_shows()
    {
        var d = OpenThree();
        var picker = OpenPicker(d, OlderPath);
        ItemSelections.Select(picker.Selection, picker.Filtered[1], picker.Filtered);

        picker.Filter = "Tests";
        picker.Update(d.AppState.Assemblies);

        Assert.Equal(AppStateDriver.FixturePath, picker.Selected!.FilePath.Text);
    }

    [Fact]
    public void The_selection_holds_the_assembly_whatever_happens_to_the_rows_around_it()
    {
        var d = OpenThree();
        var picker = OpenPicker(d, OlderPath);
        ItemSelections.Select(picker.Selection, picker.Filtered[1], picker.Filtered);
        var selected = picker.Selected;

        picker.Filtered.Clear();

        Assert.Same(selected, picker.Selected);
    }

    [Fact]
    public void Closing_the_assembly_to_compare_closes_the_dialog()
    {
        var d = OpenThree();
        var picker = OpenPicker(d, OlderPath);

        AssemblyListing.CloseAssembly(d.AppState.Assemblies, Opened(d, OlderPath));
        picker.Update(d.AppState.Assemblies);

        Assert.False(picker.IsOpen);
        Assert.Null(picker.Compared);
    }
}
