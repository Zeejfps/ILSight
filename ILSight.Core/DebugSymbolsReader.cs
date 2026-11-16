// Adapted from ILSpy's PortableDebugInfoProvider and DebugInfoUtils (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Text;
using ICSharpCode.Decompiler.DebugInfo;
using SequencePoint = ICSharpCode.Decompiler.DebugInfo.SequencePoint;

namespace ILSight.Core;

/// <summary>What the decompiler asks of an assembly's <see cref="DebugSymbols"/> during one read, made per read as the
/// resolver is: the app's code the library calls back, so its own work (the search for a local among a method's, the
/// decoding of the tuple names' and dynamic flags' blobs) runs through the read's <see cref="CallbackFailure"/> (CD4,
/// CJ11), and what it reads of the PDB's rows through System.Reflection.Metadata is the read's, outside the count (CF9).
/// A blob with bad data (a name with no terminator) is said by <see cref="InvalidDataException"/>, thrown outside the
/// count: the data's failure, which the read's catch takes.</summary>
internal sealed class DebugSymbolsReader(DebugSymbols symbols, CallbackFailure callbacks) : IDebugInfoProvider
{
    private const int BitsPerByte = 8;

    public string Description => symbols.Description;

    public string SourceFileName => symbols.File.FileName;

    private MetadataReader Metadata => symbols.Metadata;

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
            DocumentUrl = ReadDocumentName(point.Document),
        };
    }

    private string ReadDocumentName(DocumentHandle handle)
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
        foreach (var local in ReadLocals(method))
        {
            var row = Metadata.GetLocalVariable(local.Handle);
            variables.Add(new Variable(local.Index, Metadata.GetString(row.Name)));
        }
        return variables;
    }

    public bool TryGetName(MethodDefinitionHandle method, int index, [MaybeNullWhen(false)] out string name)
    {
        name = null;
        var locals = ReadLocals(method);
        var handle = callbacks.Run((locals, index), static s => FindLocal(s.locals, s.index));
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
        var locals = ReadLocals(method);
        var local = callbacks.Run((locals, index), static s => FindLocal(s.locals, s.index));
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
                var namesBlob = Metadata.GetBlobBytes(information.Value);
                var names = callbacks.Run(namesBlob, static blob => DecodeTupleElementNames(blob));
                extraTypeInfo.TupleElementNames = names ?? throw new InvalidDataException("A tuple element name in the PDB has no terminator.");
            }
            else if (kind == KnownGuids.DynamicLocalVariables)
            {
                var flagsBlob = Metadata.GetBlobBytes(information.Value);
                extraTypeInfo.DynamicFlags = callbacks.Run(flagsBlob, static blob => DecodeDynamicFlags(blob));
            }
        }
        if (extraTypeInfo.TupleElementNames is not null)
        {
            return true;
        }

        return extraTypeInfo.DynamicFlags is not null;
    }

    // A local of the method's, as its scopes list it: its row and its slot.
    private readonly record struct LocalRow
    {
        public required LocalVariableHandle Handle { get; init; }

        public required int Index { get; init; }
    }

    // The method's locals, read of the PDB's scope rows.
    private List<LocalRow> ReadLocals(MethodDefinitionHandle method)
    {
        var locals = new List<LocalRow>();
        foreach (var scopeHandle in Metadata.GetLocalScopes(method))
        {
            var scope = Metadata.GetLocalScope(scopeHandle);
            foreach (var handle in scope.GetLocalVariables())
            {
                var local = Metadata.GetLocalVariable(handle);
                locals.Add(new LocalRow { Handle = handle, Index = local.Index });
            }
        }
        return locals;
    }

    private static LocalVariableHandle? FindLocal(List<LocalRow> locals, int index)
    {
        foreach (var local in locals)
        {
            if (local.Index == index)
            {
                return local.Handle;
            }
        }
        return null;
    }

    // The PDB writes each name null-terminated in UTF-8, empty for an element left unnamed; null for a blob whose last
    // name has no terminator, bad data, which the caller says outside the count.
    internal static string?[]? DecodeTupleElementNames(byte[] blob)
    {
        var names = new List<string?>();
        var start = 0;
        while (start < blob.Length)
        {
            var end = Array.IndexOf(blob, (byte)0, start);
            if (end < 0)
            {
                return null;
            }

            var name = Encoding.UTF8.GetString(blob, start, end - start);
            names.Add(string.IsNullOrWhiteSpace(name) ? null : name);
            start = end + 1;
        }

        return names.ToArray();
    }

    // The PDB packs one flag per bit, lowest bit of each byte first.
    private static bool[] DecodeDynamicFlags(byte[] bytes)
    {
        var flags = new bool[bytes.Length * BitsPerByte];
        for (var i = 0; i < flags.Length; i++)
        {
            var bit = 1 << (i % BitsPerByte);
            flags[i] = (bytes[i / BitsPerByte] & bit) != 0;
        }
        return flags;
    }
}
