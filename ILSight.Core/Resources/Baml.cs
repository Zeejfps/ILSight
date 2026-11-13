using ICSharpCode.BamlDecompiler;

namespace ILSight.Core.Resources;

/// <summary>WPF's compiled XAML, which ILSpy's BAML decompiler turns back into XAML.</summary>
internal static class Baml
{
    private const string Extension = ".baml";
    public const string XamlExtension = ".xaml";

    public static bool IsBamlName(string name)
    {
        return name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><paramref name="name"/> with the extension of the XAML it decompiles to.</summary>
    public static string XamlName(string name)
    {
        return Path.ChangeExtension(name, XamlExtension);
    }

    /// <summary>The BAML in <paramref name="data"/> as XAML, its types resolved against
    /// <paramref name="typeSystem"/>.</summary>
    public static BamlDecompilationResult Decompile(BamlDecompilerTypeSystem typeSystem, Stream data, CancellationToken cancellationToken)
    {
        var settings = new BamlDecompilerSettings { ThrowOnAssemblyResolveErrors = false };
        var decompiler = new XamlDecompiler(typeSystem, settings) { CancellationToken = cancellationToken };
        return decompiler.Decompile(data);
    }

    /// <summary>The BAML in <paramref name="data"/>, of <paramref name="assembly"/>, as XAML text, its types resolved in
    /// the unit of <paramref name="reading"/>.</summary>
    public static string DecompileToText(ModuleReading reading, LoadedModule assembly, byte[] data, CancellationToken cancellationToken)
    {
        var typeSystem = new BamlDecompilerTypeSystem(LoadedAssemblies.GetFile(assembly), LoadedAssemblies.CreateResolver(reading, assembly));
        using var stream = new MemoryStream(data);
        var result = Decompile(typeSystem, stream, cancellationToken);
        return result.Xaml.ToString();
    }
}
