using System.IO.Compression;
using ICSharpCode.Decompiler;

namespace ILSight.Core;

/// <summary>A file in a <see cref="AssemblyPackage"/>: its name, its path from the package's root,
/// with <c>/</c> between folders, and its size.</summary>
public sealed record PackageFile(string Name, string FullName, long Length)
{
    /// <summary>The path of the folder holding it, from the package's root; empty at the root.</summary>
    public string Folder
    {
        get
        {
            var at = FullName.LastIndexOf('/');
            if (at < 0)
                return "";
            return FullName.Substring(0, at);
        }
    }
}

/// <summary>A folder in a <see cref="AssemblyPackage"/>, with the folders and files right inside
/// it, each sorted by name.</summary>
public sealed class PackageFolder
{
    public required string Name { get; init; }

    /// <summary>The path from the package's root, with <c>/</c> between folders; empty for the root.</summary>
    public required string FullName { get; init; }

    public List<PackageFolder> Folders { get; } = new();

    public List<PackageFile> Files { get; } = new();
}

/// <summary>A file of a package that was not extracted, and why.</summary>
public abstract record ExtractFailure
{
    private ExtractFailure()
    {
    }

    public abstract PackageFile File { get; init; }

    /// <summary>Its path would have put it outside the folder extracted into.</summary>
    public sealed record OutsideFolder(PackageFile File) : ExtractFailure;

    public sealed record NotWritten(PackageFile File, ExportError Error) : ExtractFailure;
}

/// <summary>The paths extracting a package's folder wrote, and the files it could not.</summary>
public sealed record ExtractedFiles(IReadOnlyList<string> Written, IReadOnlyList<ExtractFailure> Failures);

/// <summary>
/// A file holding assemblies and other files: a zip file such as a NuGet package, or an app published
/// as a single-file bundle. Its folders and files are listed from the zip's directory or the bundle's
/// manifest; a file's bytes are read out only when asked for, and the package is not kept open.
/// </summary>
public sealed class AssemblyPackage
{
    public required Dictionary<string, PackageFile> Files { get; init; }

    /// <summary>Reads the bytes of a file out of the package.</summary>
    public required Func<PackageFile, byte[]> Read { get; init; }

    /// <summary>The absolute path of the package file.</summary>
    public required AssemblyPath FilePath { get; init; }

    /// <summary>Whether this is an app published as a single-file bundle, not a zip.</summary>
    public required bool IsBundle { get; init; }

    public required PackageFolder Root { get; init; }

    /// <summary>The folders and files of a package as they are listed, sorted into a tree once all are in.</summary>
    public sealed class FileTree
    {
        public required PackageFolder Root { get; init; }
        public required Dictionary<string, PackageFolder> Folders { get; init; }
        public readonly Dictionary<string, PackageFile> Files = new(StringComparer.Ordinal);

    }
}

/// <summary>Reading packages, and the files in them.</summary>
public static class AssemblyPackages
{
    private const char Separator = '/';

    /// <summary>The package at <paramref name="path"/>: a zip archive, else a single-file bundle; null
    /// when the file is neither.</summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static AssemblyPackage? TryRead(AssemblyPath path)
    {
        var zip = TryReadZip(path);
        if (zip != null)
            return zip;
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
            AddZipEntry(tree, entryNames, entry);
        return CreatePackage(tree, path, isBundle: false, file => ReadZipFile(path, file, entryNames[file.FullName]));
    }

    /// <summary>Adds <paramref name="entry"/> to <paramref name="tree"/> under its name with <c>/</c>
    /// between folders, remembering the name the zip gives it to read it by; leaves out one whose name
    /// leads out of the package.</summary>
    private static void AddZipEntry(AssemblyPackage.FileTree tree, Dictionary<string, string> entryNames, ZipArchiveEntry entry)
    {
        var fullName = FindNameInside(entry.FullName);
        if (string.IsNullOrEmpty(fullName))
            return;
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
        if (entries == null)
            return null;
        var tree = CreateTree();
        var byName = new Dictionary<string, SingleFileBundle.Entry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var fullName = FindNameInside(entry.RelativePath);
            if (string.IsNullOrEmpty(fullName))
                continue;
            AddFile(tree, fullName, entry.Size);
            byName.TryAdd(fullName, entry);
        }
        return CreatePackage(tree, path, isBundle: true, file => BundleContents.Read(path.Text, byName[file.FullName]));
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
            return null;
        var parts = new List<string>();
        foreach (var part in name.Split(Separator, '\\'))
        {
            if (part == "..")
                return null;
            if (part is "" or ".")
                continue;
            parts.Add(part);
        }
        return string.Join(Separator, parts);
    }

    private static (string Folder, string Name) SplitName(string fullName)
    {
        var at = fullName.LastIndexOf(Separator);
        if (at < 0)
            return ("", fullName);
        return (fullName.Substring(0, at), fullName.Substring(at + 1));
    }

    private static AssemblyPackage.FileTree CreateTree()
    {
        var root = new PackageFolder { Name = "", FullName = "" };
        return new AssemblyPackage.FileTree { Root = root, Folders = new Dictionary<string, PackageFolder>(StringComparer.Ordinal) { [""] = root } };
    }

    private static void AddFile(AssemblyPackage.FileTree tree, string fullName, long length)
    {
        var (folderName, name) = SplitName(fullName);
        if (name.Length == 0)
            return;
        var file = new PackageFile(name, fullName, length);
        AddFolder(tree, folderName).Files.Add(file);
        tree.Files.TryAdd(fullName, file);
    }

    private static PackageFolder AddFolder(AssemblyPackage.FileTree tree, string fullName)
    {
        if (tree.Folders.TryGetValue(fullName, out var found))
            return found;
        var (parentName, name) = SplitName(fullName);
        var parent = AddFolder(tree, parentName);
        var folder = new PackageFolder { Name = name, FullName = fullName };
        parent.Folders.Add(folder);
        tree.Folders.Add(fullName, folder);
        return folder;
    }

    private static AssemblyPackage CreatePackage(AssemblyPackage.FileTree tree, AssemblyPath path, bool isBundle, Func<PackageFile, byte[]> read)
    {
        SortFolder(tree.Root);
        return new AssemblyPackage { FilePath = path, IsBundle = isBundle, Root = tree.Root, Files = tree.Files, Read = read };
    }

    private static void SortFolder(PackageFolder folder)
    {
        folder.Folders.Sort(static (a, b) => NaturalStringComparer.Instance.Compare(a.Name, b.Name));
        folder.Files.Sort(static (a, b) => NaturalStringComparer.Instance.Compare(a.Name, b.Name));
        foreach (var child in folder.Folders)
            SortFolder(child);
    }

    /// <summary>Every file of the package, folder by folder in the order <see cref="Root"/> lists them:
    /// a folder's folders, then its files.</summary>
    public static List<PackageFile> ListFiles(AssemblyPackage package)
    {
        var files = new List<PackageFile>();
        CollectFiles(package.Root, files);
        return files;
    }

    private static void CollectFiles(PackageFolder folder, List<PackageFile> files)
    {
        foreach (var child in folder.Folders)
            CollectFiles(child, files);
        files.AddRange(folder.Files);
    }

    /// <summary>The file at <paramref name="fullName"/>, a path from the package's root; matched
    /// exactly, else ignoring case. Null when the package has none.</summary>
    public static PackageFile? FindFile(AssemblyPackage package, string fullName)
    {
        var name = fullName.Replace('\\', Separator);
        if (package.Files.TryGetValue(name, out var exact))
            return exact;
        foreach (var (key, file) in package.Files)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return file;
        }
        return null;
    }

    /// <summary>The file at <paramref name="path"/>, a path made by <see cref="PathOf"/>; null when
    /// the package has none there.</summary>
    internal static PackageFile? FindFileAt(AssemblyPackage package, AssemblyPath path)
    {
        var relative = GetRelativeName(package.FilePath, path);
        return FindFile(package, relative);
    }

    /// <summary>Writes every file in <paramref name="folder"/> and the folders in it into
    /// <paramref name="target"/>, at its path from <paramref name="folder"/>. A file that cannot be
    /// read or written, or whose path leads out of <paramref name="target"/>, is left out and listed as
    /// failed.</summary>
    public static ExtractedFiles ExtractFolder(AssemblyPackage package, PackageFolder folder, string target, IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = new List<PackageFile>();
        CollectFiles(folder, files);
        var root = Path.GetFullPath(target);
        var written = new List<string>();
        var failures = new List<ExtractFailure>();
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ExportProgress(folder.Name, 1, 1, (float)i / files.Count));
            var failure = TryExtract(package, files[i], folder, root, written);
            if (failure != null)
                failures.Add(failure);
        }
        return new ExtractedFiles(written, failures);
    }

    private static ExtractFailure? TryExtract(AssemblyPackage package, PackageFile file, PackageFolder folder, string root, List<string> written)
    {
        try
        {
            var path = FindTargetPath(file, folder, root);
            if (path == null)
                return new ExtractFailure.OutsideFolder(file);
            WriteFile(package, file, path);
            written.Add(path);
            return null;
        }
        catch (Exception e) when (IsExtractError(e))
        {
            return new ExtractFailure.NotWritten(file, new ExportError(e.Message, e.ToString()));
        }
    }

    /// <summary>Where <paramref name="file"/> goes under <paramref name="root"/>, at its path from
    /// <paramref name="folder"/>; null when that path leads out of <paramref name="root"/>.</summary>
    private static string? FindTargetPath(PackageFile file, PackageFolder folder, string root)
    {
        var relative = GetRelativePath(folder, file).Replace(Separator, Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!IsInside(path, root))
            return null;
        return path;
    }

    private static string GetRelativePath(PackageFolder folder, PackageFile file)
    {
        if (folder.FullName.Length == 0)
            return file.FullName;
        return file.FullName.Substring(folder.FullName.Length + 1);
    }

    private static bool IsInside(string path, string root)
    {
        var prefix = root;
        if (!Path.EndsInDirectorySeparator(prefix))
            prefix += Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.StartsWith(prefix, comparison);
    }

    private static void WriteFile(AssemblyPackage package, PackageFile file, string path)
    {
        var bytes = ReadFile(package, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static bool IsExtractError(Exception e)
    {
        if (e is IOException)
            return true;
        if (e is UnauthorizedAccessException)
            return true;
        if (e is InvalidDataException)
            return true;
        return e is ArgumentException;
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

    /// <summary>The package file a path made by <see cref="PathOf"/> runs through, and the path of the
    /// file inside it; null when no file on disk lies along <paramref name="path"/>.</summary>
    internal static (AssemblyPath Package, string FullName)? SplitPath(AssemblyPath path)
    {
        var folder = Path.GetDirectoryName(path.Text);
        while (folder != null)
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
