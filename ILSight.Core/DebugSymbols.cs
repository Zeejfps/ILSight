// Adapted from ILSpy's PortableDebugInfoProvider and DebugInfoUtils (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Metadata;
using SequencePoint = ICSharpCode.Decompiler.DebugInfo.SequencePoint;

namespace ILSight.Core;

/// <summary>
/// The portable PDB that goes with an assembly: the one picked for it, else the one embedded in it,
/// else the one next to it whose id matches its CodeView entry; next to it in the package, for an
/// assembly read out of one. The decompiler takes local names from it, and the tree shows its
/// metadata beside the assembly's.
/// </summary>
public sealed class DebugSymbols : IDebugInfoProvider
{
    internal const string PdbExtension = ".pdb";
    private const int BitsPerByte = 8;

    /// <summary>The PDB's own metadata.</summary>
    public required MetadataFile File { get; init; }

    /// <summary>What the tree calls <see cref="File"/>.</summary>
    public required string Title { get; init; }

    public required string Description { get; init; }

    public string SourceFileName => File.FileName;

    public MetadataReader Metadata => File.Metadata;

    public sealed record PdbImage(string Path, byte[] Image);

    public IList<SequencePoint> GetSequencePoints(MethodDefinitionHandle method)
    {
        var points = new List<SequencePoint>();
        var debugInformation = Metadata.GetMethodDebugInformation(method);
        foreach (var point in debugInformation.GetSequencePoints())
        {
            points.Add(ToSequencePoint(point));
        }

        return points;
    }

    private SequencePoint ToSequencePoint(System.Reflection.Metadata.SequencePoint point)
    {
        return new SequencePoint
        {
            Offset = point.Offset,
            StartLine = point.StartLine,
            StartColumn = point.StartColumn,
            EndLine = point.EndLine,
            EndColumn = point.EndColumn,
            DocumentUrl = GetDocumentName(point.Document),
        };
    }

    private string GetDocumentName(DocumentHandle handle)
    {
        if (handle.IsNil)
        {
            return "";
        }

        var document = Metadata.GetDocument(handle);
        return Metadata.GetString(document.Name);
    }

    public IList<Variable> GetVariables(MethodDefinitionHandle method)
    {
        var variables = new List<Variable>();
        foreach (var handle in ListLocals(method))
        {
            var local = Metadata.GetLocalVariable(handle);
            variables.Add(new Variable(local.Index, Metadata.GetString(local.Name)));
        }
        return variables;
    }

    public bool TryGetName(MethodDefinitionHandle method, int index, [MaybeNullWhen(false)] out string name)
    {
        name = null;
        var handle = FindLocal(method, index);
        if (handle is null)
        {
            return false;
        }

        var local = Metadata.GetLocalVariable(handle.Value);
        name = Metadata.GetString(local.Name);
        return true;
    }

    /// <summary>The tuple element names and <c>dynamic</c> flags the compiler recorded for local
    /// <paramref name="index"/>, which its signature cannot carry.</summary>
    public bool TryGetExtraTypeInfo(MethodDefinitionHandle method, int index, out PdbExtraTypeInfo extraTypeInfo)
    {
        extraTypeInfo = default;
        var local = FindLocal(method, index);
        if (local is null)
        {
            return false;
        }

        foreach (var handle in Metadata.GetCustomDebugInformation(local.Value))
        {
            var information = Metadata.GetCustomDebugInformation(handle);
            var kind = Metadata.GetGuid(information.Kind);
            if (kind == KnownGuids.TupleElementNames)
            {
                extraTypeInfo.TupleElementNames = ReadTupleElementNames(information.Value);
            }
            else if (kind == KnownGuids.DynamicLocalVariables)
            {
                extraTypeInfo.DynamicFlags = ReadDynamicFlags(information.Value);
            }
        }
        if (extraTypeInfo.TupleElementNames is not null)
        {
            return true;
        }

        return extraTypeInfo.DynamicFlags is not null;
    }

    /// <summary>The local of <paramref name="method"/> at <paramref name="index"/>; null when the PDB names none there.</summary>
    private LocalVariableHandle? FindLocal(MethodDefinitionHandle method, int index)
    {
        foreach (var handle in ListLocals(method))
        {
            var local = Metadata.GetLocalVariable(handle);
            if (local.Index == index)
            {
                return handle;
            }
        }
        return null;
    }

    private IEnumerable<LocalVariableHandle> ListLocals(MethodDefinitionHandle method)
    {
        foreach (var scopeHandle in Metadata.GetLocalScopes(method))
        {
            var scope = Metadata.GetLocalScope(scopeHandle);
            foreach (var local in scope.GetLocalVariables())
            {
                yield return local;
            }
        }
    }

    private string?[] ReadTupleElementNames(BlobHandle blob)
    {
        var reader = Metadata.GetBlobReader(blob);
        var names = new List<string?>();
        while (reader.RemainingBytes > 0)
        {
            names.Add(ReadTupleElementName(ref reader));
        }

        return names.ToArray();
    }

    /// <summary>A null-terminated UTF-8 name; null for an element left unnamed.</summary>
    private static string? ReadTupleElementName(ref BlobReader reader)
    {
        var length = reader.IndexOf(0);
        var name = reader.ReadUTF8(length);
        reader.ReadByte();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return name;
    }

    /// <summary>One flag per bit, lowest bit of each byte first.</summary>
    private bool[] ReadDynamicFlags(BlobHandle blob)
    {
        var bytes = Metadata.GetBlobBytes(blob);
        var flags = new bool[bytes.Length * BitsPerByte];
        for (var i = 0; i < flags.Length; i++)
        {
            var bit = 1 << (i % BitsPerByte);
            flags[i] = (bytes[i / BitsPerByte] & bit) != 0;
        }
        return flags;
    }
}
