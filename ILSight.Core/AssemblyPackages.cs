using System.IO.Compression;
using ICSharpCode.Decompiler;

namespace ILSight.Core;

/// <summary>Reading packages, and the files in them.</summary>
public static class AssemblyPackages
{
    private const char Separator = '/';

    /// <summary>The folders and files of a package as they are listed, sorted into a tree once all are in.</summary>
    private sealed class FileTree
    {
        public required PackageFolder Root { get; init; }
        public required Dictionary<string, PackageFolder> Folders { get; init; }
        public readonly Dictionary<string, PackageFile> Files = new(StringComparer.Ordinal);
    }

    /// <summary>The package at <paramref name="path"/>: a zip archive, else a single-file bundle; null
    /// when the file is neither.</summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static AssemblyPackage? TryRead(AssemblyPath path)
    {
        var zip = TryReadZip(path);
        if (zip is not null)
        {
            return zip;
        }

        return TryReadBundle(path);
    }

    private static AssemblyPackage? TryReadZip(AssemblyPath path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path.Text);
            return ReadZip(path, archive);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static AssemblyPackage ReadZip(AssemblyPath path, ZipArchive archive)
    {
        var tree = CreateTree();
        var entryNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            AddZipEntry(tree, entryNames, entry);
        }

        return CreatePackage(tree, path, isBundle: false, file => ReadZipFile(path, file, entryNames[file.FullName]));
    }

    /// <summary>Adds <paramref name="entry"/> to <paramref name="tree"/> under its name with <c>/</c>
    /// between folders, remembering the name the zip gives it to read it by; leaves out one whose name
    /// leads out of the package.</summary>
    private static void AddZipEntry(FileTree tree, Dictionary<string, string> entryNames, ZipArchiveEntry entry)
    {
        var fullName = FindNameInside(entry.FullName);
        if (string.IsNullOrEmpty(fullName))
        {
            return;
        }

        if (IsFolderName(entry.FullName))
        {
            AddFolder(tree, fullName);
            return;
        }
        AddFile(tree, fullName, entry.Length);
        entryNames.TryAdd(fullName, entry.FullName);
    }

    private static byte[] ReadZipFile(AssemblyPath path, PackageFile file, string entryName)
    {
        using var archive = ZipFile.OpenRead(path.Text);
        var entry = archive.GetEntry(entryName) ?? throw new IOException($"'{file.FullName}' is no longer in '{path}'.");
        using var stream = entry.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static AssemblyPackage? TryReadBundle(AssemblyPath path)
    {
        var entries = BundleContents.TryReadManifest(path.Text);
        if (entries is null)
        {
            return null;
        }

        var tree = CreateTree();
        var byName = AddBundleFiles(tree, entries);
        return CreatePackage(tree, path, isBundle: true, file => BundleContents.Read(path.Text, byName[file.FullName]));
    }

    /// <summary>Adds a file to <paramref name="tree"/> for each of a bundle's <paramref name="entries"/> that names one;
    /// answers the entries by the name each file has inside, the first one kept where names repeat.</summary>
    private static Dictionary<string, SingleFileBundle.Entry> AddBundleFiles(FileTree tree, IEnumerable<SingleFileBundle.Entry> entries)
    {
        var byName = new Dictionary<string, SingleFileBundle.Entry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var fullName = FindNameInside(entry.RelativePath);
            if (string.IsNullOrEmpty(fullName))
            {
                continue;
            }

            AddFile(tree, fullName, entry.Size);
            byName.TryAdd(fullName, entry);
        }
        return byName;
    }

    private static bool IsFolderName(string name)
    {
        return name.EndsWith(Separator) || name.EndsWith('\\');
    }

    /// <summary><paramref name="name"/>, as a package entry gives it, with <c>/</c> between folders
    /// and no empty or <c>.</c> folders; null when it leads out of the package: rooted, on a drive,
    /// or through <c>..</c>.</summary>
    private static string? FindNameInside(string name)
    {
        if (name.StartsWith(Separator) || name.StartsWith('\\') || name.Contains(':'))
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var part in name.Split(Separator, '\\'))
        {
            if (part == "..")
            {
                return null;
            }

            if (part is "" or ".")
            {
                continue;
            }

            parts.Add(part);
        }
        return string.Join(Separator, parts);
    }

    private static (string Folder, string Name) SplitName(string fullName)
    {
        var at = fullName.LastIndexOf(Separator);
        if (at < 0)
        {
            return ("", fullName);
        }

        return (fullName.Substring(0, at), fullName.Substring(at + 1));
    }

    private static FileTree CreateTree()
    {
        var root = new PackageFolder { Name = "", FullName = "" };
        return new FileTree { Root = root, Folders = new Dictionary<string, PackageFolder>(StringComparer.Ordinal) { [""] = root } };
    }

    private static void AddFile(FileTree tree, string fullName, long length)
    {
        var (folderName, name) = SplitName(fullName);
        if (name.Length == 0)
        {
            return;
        }

        var file = new PackageFile { Name = name, FullName = fullName, Length = length };
        AddFolder(tree, folderName).Files.Add(file);
        tree.Files.TryAdd(fullName, file);
    }

    private static PackageFolder AddFolder(FileTree tree, string fullName)
    {
        if (tree.Folders.TryGetValue(fullName, out var found))
        {
            return found;
        }

        var (parentName, name) = SplitName(fullName);
        var parent = AddFolder(tree, parentName);
        var folder = new PackageFolder { Name = name, FullName = fullName };
        parent.Folders.Add(folder);
        tree.Folders.Add(fullName, folder);
        return folder;
    }

    private static AssemblyPackage CreatePackage(FileTree tree, AssemblyPath path, bool isBundle, Func<PackageFile, byte[]> read)
    {
        SortFolder(tree.Root);
        return new AssemblyPackage { FilePath = path, IsBundle = isBundle, Root = tree.Root, Files = tree.Files, Read = read };
    }

    private static void SortFolder(PackageFolder folder)
    {
        folder.Folders.Sort(static (a, b) => NaturalStringComparer.Instance.Compare(a.Name, b.Name));
        folder.Files.Sort(static (a, b) => NaturalStringComparer.Instance.Compare(a.Name, b.Name));
        foreach (var child in folder.Folders)
        {
            SortFolder(child);
        }
    }

    /// <summary>Every file of the package, folder by folder in the order <see cref="AssemblyPackage.Root"/> lists them:
    /// a folder's folders, then its files.</summary>
    public static List<PackageFile> ListFiles(AssemblyPackage package)
    {
        var files = new List<PackageFile>();
        CollectFiles(package.Root, files);
        return files;
    }

    internal static void CollectFiles(PackageFolder folder, List<PackageFile> files)
    {
        foreach (var child in folder.Folders)
        {
            CollectFiles(child, files);
        }

        files.AddRange(folder.Files);
    }

    /// <summary>The file at <paramref name="fullName"/>, a path from the package's root; matched
    /// exactly, else ignoring case. Null when the package has none.</summary>
    public static PackageFile? FindFile(AssemblyPackage package, string fullName)
    {
        var name = fullName.Replace('\\', Separator);
        if (package.Files.TryGetValue(name, out var exact))
        {
            return exact;
        }

        foreach (var (key, file) in package.Files)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }
        return null;
    }

    /// <summary>The file at <paramref name="path"/>, a path made by <see cref="GetFilePath"/>; null when
    /// the package has none there.</summary>
    internal static PackageFile? FindFileAt(AssemblyPackage package, AssemblyPath path)
    {
        var relative = GetRelativeName(package.FilePath, path);
        return FindFile(package, relative);
    }

    /// <summary>The bytes of <paramref name="file"/>, read out of the package.</summary>
    /// <exception cref="IOException">The package or the file in it cannot be read.</exception>
    /// <exception cref="InvalidDataException">The package says something about the file that cannot be true.</exception>
    public static byte[] ReadFile(AssemblyPackage package, PackageFile file)
    {
        return package.Read(file);
    }

    /// <summary>The path <paramref name="file"/> is known by, as if the package were a folder:
    /// <c>C:\packages\Foo.nupkg\lib\net8.0\Foo.dll</c>.</summary>
    public static AssemblyPath GetFilePath(AssemblyPackage package, PackageFile file)
    {
        var relative = file.FullName.Replace(Separator, Path.DirectorySeparatorChar);
        return package.FilePath.Combine(relative);
    }

    /// <summary>The package file a path made by <see cref="GetFilePath"/> runs through, and the path of the
    /// file inside it; null when no file on disk lies along <paramref name="path"/>.</summary>
    internal static (AssemblyPath Package, string FullName)? SplitPath(AssemblyPath path)
    {
        var folder = Path.GetDirectoryName(path.Text);
        while (folder is not null)
        {
            if (File.Exists(folder))
            {
                var package = AssemblyPath.Of(folder);
                return (package, GetRelativeName(package, path));
            }
            folder = Path.GetDirectoryName(folder);
        }
        return null;
    }

    private static string GetRelativeName(AssemblyPath package, AssemblyPath path)
    {
        var relative = Path.GetRelativePath(package.Text, path.Text);
        return relative.Replace(Path.DirectorySeparatorChar, Separator);
    }
}
