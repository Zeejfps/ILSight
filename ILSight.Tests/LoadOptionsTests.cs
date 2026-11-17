using System.Reflection.Metadata;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class LoadOptionsTests
{
    private static DecompilerOptions Off(DecompilerToggle toggle)
    {
        return DecompilerToggles.WithToggle(DecompilerOptions.Default, toggle, false);
    }

    private static (AssemblyList List, LoadedModule Assembly) OpenWith(DecompilerOptions options)
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        AssemblyListing.TryChangeLoadOptions(list, options);
        var module = list.OpenModule(Trees.TestAssemblyPath);
        return (list, module);
    }

    private static async Task<BrowseNode> RuntimeReferenceAsync(LoadedAssembly assembly)
    {
        var root = await assembly.ReadRootAsync();
        return await root.GetChildAsync("References", "System.Runtime");
    }

    [Fact]
    public void The_options_that_apply_as_assemblies_are_read_are_offered_among_the_other_decompiler_options()
    {
        var groups = new[] { DecompilerToggle.AutoLoadAssemblyReferences, DecompilerToggle.ApplyWindowsRuntimeProjections }
            .Select(t => t.Group);

        Assert.All(groups, g => Assert.IsType<DecompilerToggleGroup.Other>(g));
    }

    [Fact]
    public void Load_options_report_a_change_only_when_they_differ_from_those_in_use()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var off = Off(DecompilerToggle.AutoLoadAssemblyReferences);

        var unchanged = AssemblyListing.TryChangeLoadOptions(list, DecompilerOptions.Default);
        var changed = AssemblyListing.TryChangeLoadOptions(list, off);
        var again = AssemblyListing.TryChangeLoadOptions(list, off);

        Assert.False(unchanged);
        Assert.True(changed);
        Assert.False(again);
    }

    [Fact]
    public void Each_end_of_the_lists_generation_cancels_its_work_once_the_generation_has_changed()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var unchangedWork = list.Work.Ending;
        AssemblyListing.TryChangeLoadOptions(list, DecompilerOptions.Default);
        Assert.False(unchangedWork.IsCancellationRequested);
        var beforeOptions = list.Generation;
        var optionsWork = list.Work.Ending;
        int? optionsCancelledIn = null;
        using var optionsSeen = optionsWork.Register(() => optionsCancelledIn = list.Generation);

        AssemblyListing.TryChangeLoadOptions(list, Off(DecompilerToggle.AutoLoadAssemblyReferences));
        var beforeClear = list.Generation;
        var clearWork = list.Work.Ending;
        int? clearCancelledIn = null;
        using var clearSeen = clearWork.Register(() => clearCancelledIn = list.Generation);
        var removed = list.CloseAll();
        LoadedAssemblies.Release(removed);

        Assert.Equal(beforeOptions + 1, optionsCancelledIn);
        Assert.Equal(beforeClear + 1, clearCancelledIn);
        Assert.NotEqual(beforeClear, list.Generation);
        Assert.False(list.Work.Ending.IsCancellationRequested);
    }

    [Fact]
    public void An_open_whose_token_links_a_generation_ended_ends_as_cancelled()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var linked = list.Work.Ending;
        var generation = list.Generation;
        var removed = list.CloseAll();
        LoadedAssemblies.Release(removed);

        var cancelled = Assert.ThrowsAny<OperationCanceledException>(() => AssemblyListing.Open(list, AssemblyPath.Of(Trees.TestAssemblyPath), linked));

        Assert.Equal(linked, cancelled.CancellationToken);
        Assert.NotEqual(generation, list.Generation);
    }

    // The instant between the end's change under the list's lock and its cancel after it, which no seam of the list
    // reaches, built as the state it leaves: the generation ended, its ending not yet cancelled.
    [Fact]
    public void An_open_meeting_its_generation_ended_before_the_end_cancelled_its_ending_cancels_it_and_throws_its_cancellation()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var work = list.Work;
        var removed = list.CloseAll();
        LoadedAssemblies.Release(removed);
        using var ending = new CancellationTokenSource();
        var endedBeforeItsCancel = new GenerationWork(work.Generation, ending);

        var cancelled = Assert.ThrowsAny<OperationCanceledException>(() => AssemblyListing.Open(list, AssemblyPath.Of(Trees.TestAssemblyPath), endedBeforeItsCancel));

        Assert.Equal(ending.Token, cancelled.CancellationToken);
        Assert.True(ending.IsCancellationRequested);
        Assert.Empty(list.Assemblies);
    }

    [Fact]
    public void An_assembly_read_before_the_load_options_changed_is_not_kept()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var generation = list.Generation;
        AssemblyListing.TryChangeLoadOptions(list, Off(DecompilerToggle.ApplyWindowsRuntimeProjections));

        var loaded = AssemblyListing.TryLoad(list, AssemblyPath.Of(Trees.TestAssemblyPath), generation);
        var readBefore = loaded.GetValue().FoundOrNull;
        var opened = list.OpenModule(Trees.TestAssemblyPath);

        Assert.NotSame(readBefore, opened);
        Assert.Equal(MetadataReaderOptions.None, LoadedAssemblies.GetFile(opened).Metadata.Options);
    }

    [Fact]
    public void Load_options_left_as_they_are_keep_what_was_read()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var generation = list.Generation;

        AssemblyListing.TryChangeLoadOptions(list, DecompilerOptions.Default);

        Assert.Equal(generation, list.Generation);
    }

    [Fact]
    public async Task Without_loading_references_a_reference_to_an_assembly_not_listed_resolves_to_none()
    {
        var (_, assembly) = OpenWith(Off(DecompilerToggle.AutoLoadAssemblyReferences));

        var runtimeReference = await RuntimeReferenceAsync(assembly);
        var children = await runtimeReference.ListChildrenAsync();

        Assert.All(children, c => Assert.IsType<NodeSubject.ReferencedTypes>(c.Subject));
    }

    [Fact]
    public async Task Without_loading_references_a_reference_resolves_to_the_listed_assembly()
    {
        var (list, assembly) = OpenWith(Off(DecompilerToggle.AutoLoadAssemblyReferences));
        var runtimePath = RuntimeAssemblies.Find(TestRuntime.Installed, "System.Runtime");
        Assert.NotNull(runtimePath);
        list.Open(runtimePath);

        var runtimeReference = await RuntimeReferenceAsync(assembly);
        var children = await runtimeReference.ListChildrenAsync();

        Assert.Contains(children, c => c.Text == "System.Private.CoreLib");
    }

    [Fact]
    public void Loading_the_dependencies_loads_them_even_without_loading_references()
    {
        var (list, assembly) = OpenWith(Off(DecompilerToggle.AutoLoadAssemblyReferences));

        var listed = AssemblyListing.ListReferences(list, assembly).GetValue();

        Assert.Contains(listed, a => a.Name == "System.Runtime");
    }

    [Fact]
    public void Metadata_is_read_with_Windows_Runtime_projections_only_while_they_are_on()
    {
        var (_, projected) = OpenWith(DecompilerOptions.Default);
        var (_, plain) = OpenWith(Off(DecompilerToggle.ApplyWindowsRuntimeProjections));

        Assert.Equal(MetadataReaderOptions.ApplyWindowsRuntimeProjections, LoadedAssemblies.GetFile(projected).Metadata.Options);
        Assert.Equal(MetadataReaderOptions.None, LoadedAssemblies.GetFile(plain).Metadata.Options);
    }
}
