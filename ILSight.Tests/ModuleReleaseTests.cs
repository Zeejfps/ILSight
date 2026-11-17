using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class ModuleReleaseTests
{
    private static (AssemblyList List, LoadedModule Left, LoadedModule Right) OpenEachReferencingTheOther(TempDir temp)
    {
        var leftPath = WrittenAssemblies.WriteDerivingFrom(temp, "Left", "Right");
        var rightPath = WrittenAssemblies.WriteDerivingFrom(temp, "Right", "Left");
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var left = list.OpenModule(leftPath);
        var right = list.OpenModule(rightPath);
        return (list, left, right);
    }

    [Fact]
    public async Task Two_assemblies_referencing_each_other_both_decompiled_and_closed_are_both_released()
    {
        using var temp = new TempDir();
        var (list, left, right) = OpenEachReferencingTheOther(temp);
        var leftRoot = await left.ReadRootAsync();
        var derived = await leftRoot.GetChildAsync("Left", "Derived");
        var leftCode = await derived.DecompileAsync(DecompilationLanguage.CSharp);
        var rightRoot = await right.ReadRootAsync();
        var derivedNode = await rightRoot.GetChildAsync("Right", "Derived");
        var rightCode = await derivedNode.DecompileAsync(DecompilationLanguage.CSharp);

        var leftRemoved = list.Close(left);
        LoadedAssemblies.Release(leftRemoved);
        var rightRemoved = list.Close(right);
        LoadedAssemblies.Release(rightRemoved);
        await LoadedAssemblies.WaitForReleaseAsync(left, CancellationToken.None).WithinPatienceAsync();
        await LoadedAssemblies.WaitForReleaseAsync(right, CancellationToken.None).WithinPatienceAsync();

        Assert.Null(leftCode.Error);
        Assert.Null(rightCode.Error);
    }

    [Fact]
    public async Task An_assembly_closed_while_one_referencing_it_stays_open_is_released_once_its_reads_end_and_the_open_ones_next_decompile_reads_it_no_more()
    {
        using var temp = new TempDir();
        var (list, left, right) = OpenEachReferencingTheOther(temp);
        var leftRoot = await left.ReadRootAsync();
        var derived = await leftRoot.GetChildAsync("Left", "Derived");
        await derived.DecompileAsync(DecompilationLanguage.CSharp);
        var before = await ReadTypeSystemAsync(left);
        var rightRoot = await right.ReadRootAsync();
        var derivedNode = await rightRoot.GetChildAsync("Right", "Derived");
        await derivedNode.DecompileAsync(DecompilationLanguage.CSharp);

        var removed = list.Close(right);
        LoadedAssemblies.Release(removed);
        await LoadedAssemblies.WaitForReleaseAsync(right, CancellationToken.None).WithinPatienceAsync();
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
        var leftRoot = await left.ReadRootAsync();
        var derived = await leftRoot.GetChildAsync("Left", "Derived");
        var underWay = LoadedAssemblies.TryBeginReading(left) ?? throw new InvalidOperationException("Left was released before it was closed.");

        var removed = list.Close(left);
        LoadedAssemblies.Release(removed);
        var released = LoadedAssemblies.WaitForReleaseAsync(left, CancellationToken.None);
        var newReader = LoadedAssemblies.TryBeginReading(left);
        var decompiled = (await NodeContentLoader.DecompileAsync(TestSession.Shared, derived, DecompilationLanguage.CSharp, DecompilerOptions.Default, EnglishNotes.Instance)).GetValue();
        var isReleasedWhileRead = released.IsCompleted;
        underWay.Dispose();
        await released.WithinPatienceAsync();

        Assert.Null(newReader);
        UnionAsserts.GetCase<ModuleRead<DecompiledDocument>.Closed>(decompiled);
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
        return UnionAsserts.GetCase<TypeSystemRead.Read>(read).TypeSystem;
    }
}
