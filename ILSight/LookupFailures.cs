using ILSight.Core;

namespace ILSight;

/// <summary>The problem a lookup's failure is reported as: the lookup that failed, by its cause where what the user can
/// do differs. A file the lookup needed that is there but could not be opened (access denied, another program holding
/// it) is readable again once the user sees to it; one that is no assembly or package that can be read stays so until
/// it is replaced; any other failure is the app's, told as it came.</summary>
public static class LookupFailures
{
    /// <summary>Looking up <paramref name="place"/> failed with <paramref name="error"/>.</summary>
    public static AppProblem OfPlace(NodePath place, Exception error)
    {
        var unopened = FindUnopenedFile(error);
        if (unopened is null)
        {
            return new AppProblem.PlaceLookupFailed { Place = place, Error = error };
        }

        if (IsDenied(unopened))
        {
            return new AppProblem.PlaceLookupFileDenied { Place = place, File = unopened.FilePath, Error = error };
        }

        return new AppProblem.PlaceLookupFileDamaged { Place = place, File = unopened.FilePath, Error = error };
    }

    /// <summary>Finding what <paramref name="reference"/> points at failed with <paramref name="error"/>.</summary>
    public static AppProblem OfReference(CodeReference reference, Exception error)
    {
        var unopened = FindUnopenedFile(error);
        if (unopened is null)
        {
            return new AppProblem.Unresolved { Reference = reference, Error = error };
        }

        if (IsDenied(unopened))
        {
            return new AppProblem.UnresolvedFileDenied { Reference = reference, File = unopened.FilePath, Error = error };
        }

        return new AppProblem.UnresolvedFileDamaged { Reference = reference, File = unopened.FilePath, Error = error };
    }

    // The file the lookup could not open, wherever in the chain of causes the loader said so.
    private static AssemblyLoadException? FindUnopenedFile(Exception error)
    {
        var cause = error;
        while (cause is not AssemblyLoadException)
        {
            var inner = cause.InnerException;
            if (inner is null)
            {
                return null;
            }

            cause = inner;
        }

        return (AssemblyLoadException)cause;
    }

    // Opening the file was refused or it was held; a file not there is no failure (the loader answers not found).
    private static bool IsDenied(AssemblyLoadException unopened)
    {
        return unopened.InnerException is UnauthorizedAccessException
            or IOException and not FileNotFoundException and not DirectoryNotFoundException;
    }
}
