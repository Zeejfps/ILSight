namespace ILSight.Core;

/// <summary>Extracting a package's folder to the disk: I/O, which only the app's adapters call (listed in
/// <c>rulelab.ambient_types</c>). Reading the package is <see cref="AssemblyPackages"/>'s.</summary>
public static class PackageExtraction
{
    private const char Separator = '/';

    /// <summary>Writes every file in <paramref name="folder"/> and the folders in it into
    /// <paramref name="target"/>, at its path from <paramref name="folder"/>. A file that cannot be
    /// read or written, or whose path leads out of <paramref name="target"/>, is left out and listed as
    /// failed.</summary>
    public static ExtractedFiles ExtractFolder(AssemblyPackage package, PackageFolder folder, string target, IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = new List<PackageFile>();
        AssemblyPackages.CollectFiles(folder, files);
        var root = Path.GetFullPath(target);
        var written = new List<string>();
        var failures = new List<ExtractFailure>();
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ExportProgress(folder.Name, 1, 1, (float)i / files.Count));
            var failure = TryExtract(package, files[i], folder, root, written);
            if (failure is not null)
            {
                failures.Add(failure);
            }
        }
        return new ExtractedFiles(written, failures);
    }

    private static ExtractFailure? TryExtract(AssemblyPackage package, PackageFile file, PackageFolder folder, string root, List<string> written)
    {
        try
        {
            var path = FindTargetPath(file, folder, root);
            if (path is null)
            {
                return new ExtractFailure.OutsideFolder(file);
            }

            WriteFile(package, file, path);
            written.Add(path.Text);
            return null;
        }
        catch (IOException e)
        {
            return NotWritten(file, e);
        }
        catch (UnauthorizedAccessException e)
        {
            return NotWritten(file, e);
        }
        catch (InvalidDataException e)
        {
            return NotWritten(file, e);
        }
        catch (ArgumentException e)
        {
            return NotWritten(file, e);
        }
    }

    /// <summary>Where <paramref name="file"/> goes under <paramref name="root"/>, at its path from
    /// <paramref name="folder"/>; null when that path leads out of <paramref name="root"/>, or names no file.</summary>
    private static FullFilePath? FindTargetPath(PackageFile file, PackageFolder folder, string root)
    {
        var relative = GetRelativePath(folder, file).Replace(Separator, Path.DirectorySeparatorChar);
        var path = FullFilePath.Parse(Path.GetFullPath(Path.Combine(root, relative)));
        if (path is null)
        {
            return null;
        }

        if (!IsInside(path.Text, root))
        {
            return null;
        }

        return path;
    }

    private static string GetRelativePath(PackageFolder folder, PackageFile file)
    {
        if (folder.FullName.Length == 0)
        {
            return file.FullName;
        }

        return file.FullName.Substring(folder.FullName.Length + 1);
    }

    private static bool IsInside(string path, string root)
    {
        var prefix = root;
        if (!Path.EndsInDirectorySeparator(prefix))
        {
            prefix += Path.DirectorySeparatorChar;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.StartsWith(prefix, comparison);
    }

    private static void WriteFile(AssemblyPackage package, PackageFile file, FullFilePath path)
    {
        var bytes = AssemblyPackages.ReadFile(package, file);
        Directory.CreateDirectory(path.Folder);
        File.WriteAllBytes(path.Text, bytes);
    }

    private static ExtractFailure NotWritten(PackageFile file, Exception e)
    {
        return new ExtractFailure.NotWritten(file, new ExportError(e.Message, e.ToString()));
    }
}
