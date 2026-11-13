using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>Checking a PDB picked for an assembly: it reads the file, I/O which only the app's adapters call (listed
/// in <c>rulelab.ambient_types</c>).</summary>
public static class PdbFiles
{
    private const string WindowsPdbSignature = "Microsoft C/C++ MSF 7.00";

    /// <summary>Whether the PDB at <paramref name="pdbPath"/> can be read for <paramref name="module"/>. Reads the
    /// file.</summary>
    public static PdbCheck Check(LoadedModule module, string pdbPath)
    {
        var pe = LoadedAssemblies.FindPEFile(module);
        if (pe is null)
        {
            return PdbCheck.Unreadable;
        }

        return Check(pe, pdbPath);
    }

    private static PdbCheck Check(PEFile assembly, string pdbPath)
    {
        try
        {
            var image = System.IO.File.ReadAllBytes(pdbPath);
            if (IsWindowsPdb(image))
            {
                return PdbCheck.WindowsPdb;
            }

            var provider = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(image));
            var pdbReader = provider.GetMetadataReader();
            if (pdbReader.DebugMetadataHeader is null)
            {
                return PdbCheck.Unreadable;
            }

            return CheckBuild(assembly, provider);
        }
        catch (BadImageFormatException)
        {
            return PdbCheck.Unreadable;
        }
        catch (IOException)
        {
            return PdbCheck.Unreadable;
        }
        catch (UnauthorizedAccessException)
        {
            return PdbCheck.Unreadable;
        }
        catch (InvalidOperationException)
        {
            return PdbCheck.Unreadable;
        }
        catch (InvalidDataException)
        {
            return PdbCheck.Unreadable;
        }
    }

    private static bool IsWindowsPdb(byte[] image)
    {
        var signature = System.Text.Encoding.ASCII.GetBytes(WindowsPdbSignature);
        return image.AsSpan().StartsWith(signature);
    }

    private static PdbCheck CheckBuild(PEFile assembly, MetadataReaderProvider provider)
    {
        var namesBuild = false;
        foreach (var entry in assembly.Reader.ReadDebugDirectory())
        {
            if (entry.Type != DebugDirectoryEntryType.CodeView)
            {
                continue;
            }

            namesBuild = true;
            var codeView = assembly.Reader.ReadCodeViewDebugDirectoryData(entry);
            if (DebugSymbolFiles.IsBuiltWith(provider, codeView, entry))
            {
                return PdbCheck.Matches;
            }
        }
        if (namesBuild)
        {
            return PdbCheck.OtherBuild;
        }

        return PdbCheck.Matches;
    }
}
