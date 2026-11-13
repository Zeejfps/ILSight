using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class ModuleReleaseTests
{
    // A release that should come comes well within this; past it, it never came.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static (AssemblyList List, LoadedModule Left, LoadedModule Right) OpenEachReferencingTheOther(TempDir temp)
    {
        var leftPath = WrittenAssemblies.WriteDerivingFrom(temp, "Left", "Right");
        var rightPath = WrittenAssemblies.WriteDerivingFrom(temp, "Right", "Left");
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        return (list, list.OpenModule(leftPath), list.OpenModule(rightPath));
    }

    [Fact]
    public async Task Two_assemblies_referencing_each_other_both_decompiled_and_closed_are_both_released()
    {
        using var temp = new TempDir();
        var (list, left, right) = OpenEachReferencingTheOther(temp);
        var leftCode = await left.ReadRootAsync().GetChildAsync("Left", "Derived").DecompileAsync(DecompilationLanguage.CSharp);
        var rightCode = await right.ReadRootAsync().GetChildAsync("Right", "Derived").DecompileAsync(DecompilationLanguage.CSharp);

        LoadedAssemblies.Release(list.Close(left));
        LoadedAssemblies.Release(list.Close(right));
        await LoadedAssemblies.WaitForReleaseAsync(left, CancellationToken.None).WaitAsync(Patience);
        await LoadedAssemblies.WaitForReleaseAsync(right, CancellationToken.None).WaitAsync(Patience);

        Assert.Null(leftCode.Error);
        Assert.Null(rightCode.Error);
    }

    [Fact]
    public async Task An_assembly_closed_while_one_referencing_it_stays_open_is_released_once_its_reads_end_and_the_open_ones_next_decompile_reads_it_no_more()
    {
        using var temp = new TempDir();
        var (list, left, right) = OpenEachReferencingTheOther(temp);
        var derived = await left.ReadRootAsync().GetChildAsync("Left", "Derived");
        await derived.DecompileAsync(DecompilationLanguage.CSharp);
        var before = await ReadTypeSystemAsync(left);
        await right.ReadRootAsync().GetChildAsync("Right", "Derived").DecompileAsync(DecompilationLanguage.CSharp);

        LoadedAssemblies.Release(list.Close(right));
        await LoadedAssemblies.WaitForReleaseAsync(right, CancellationToken.None).WaitAsync(Patience);
        var again = await derived.DecompileAsync(DecompilationLanguage.CSharp);
        var after = await ReadTypeSystemAsync(left);

        Assert.Contains(before.ReferencedModules, m => IsReadFrom(right, m));
        Assert.Null(again.Error);
        Assert.DoesNotContain(after.ReferencedModules, m => IsReadFrom(right, m));
    }

    [Fact]
    public async Task A_module_closed_while_a_unit_reads_it_refuses_a_new_reader_and_is_released_once_that_unit_ends()
    {
        using var temp = new TempDir();
        var (list, left, _) = OpenEachReferencingTheOther(temp);
        var derived = await left.ReadRootAsync().GetChildAsync("Left", "Derived");
        var underWay = LoadedAssemblies.TryBeginReading(left) ?? throw new InvalidOperationException("Left was released before it was closed.");

        LoadedAssemblies.Release(list.Close(left));
        var released = LoadedAssemblies.WaitForReleaseAsync(left, CancellationToken.None);
        var newReader = LoadedAssemblies.TryBeginReading(left);
        var decompiled = (await NodeContentLoader.DecompileAsync(TestSession.Shared, derived, DecompilationLanguage.CSharp, DecompilerOptions.Default)).GetValue();
        var isReleasedWhileRead = released.IsCompleted;
        underWay.Dispose();
        await released.WaitAsync(Patience);

        Assert.Null(newReader);
        Assert.IsType<ModuleRead<DecompiledDocument>.Closed>(decompiled);
        Assert.False(isReleasedWhileRead);
    }

    private static bool IsReadFrom(LoadedModule module, IModule read)
    {
        var file = read.MetadataFile;
        return file is not null && LoadedAssemblies.IsReadFrom(module, file);
    }

    private static async Task<DecompilerTypeSystem> ReadTypeSystemAsync(LoadedModule module)
    {
        var read = await LoadedAssemblies.ReadTypeSystemAheadAsync(module, CancellationToken.None);
        return Assert.IsType<TypeSystemRead.Read>(read).TypeSystem;
    }
}
