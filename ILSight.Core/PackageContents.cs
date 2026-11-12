namespace ILSight.Core;

internal static class PackageContents
{
    private static readonly string[] AssemblyExtensions = [".dll", ".exe", ".winmd"];

    public static bool IsAssemblyName(string name)
    {
        foreach (var extension in AssemblyExtensions)
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static byte[] ReadFileData(PackageFileInfo info)
    {
        return AssemblyPackages.ReadFile(info.Package.Package, info.File);
    }
}
