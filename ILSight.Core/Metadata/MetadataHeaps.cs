// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using static ILSight.Core.Metadata.GridSchemas;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>The four heaps (#Strings, #US, #GUID, #Blob) as grids of their entries.</summary>
internal static class MetadataHeaps
{
    /// <summary>Each heap of <paramref name="file"/>, titled with how many entries it holds.</summary>
    public static IEnumerable<(string Title, GridSchema Schema)> Create(MetadataFile file)
    {
        var m = file.Metadata;

        IEnumerable<StringHandle> Strings()
        {
            return Walk(MetadataTokens.StringHandle(0), m.GetNextHandle, static h => h.IsNil);
        }

        yield return ($"String Heap ({Strings().Count()})", AddColumns(CreateGrid<StringHandle>("String Heap", file, Strings), columns =>
        {
            AddValue(columns, "Offset", h => m.GetHeapOffset(h), "X8");
            AddValue(columns, "Length", h => m.GetString(h).Length);
            AddValue(columns, "Value", h => m.GetString(h));
        }));

        IEnumerable<UserStringHandle> UserStrings()
        {
            return Walk(MetadataTokens.UserStringHandle(0), m.GetNextHandle, static h => h.IsNil);
        }

        yield return ($"UserString Heap ({UserStrings().Count()})", AddColumns(CreateGrid<UserStringHandle>("UserString Heap", file, UserStrings), columns =>
        {
            AddValue(columns, "Offset", h => m.GetHeapOffset(h), "X8");
            AddValue(columns, "Length", h => m.GetUserString(h).Length);
            AddValue(columns, "Value", h => m.GetUserString(h));
        }));

        IEnumerable<GuidHandle> Guids()
        {
            return Enumerable.Range(1, m.GetHeapSize(HeapIndex.Guid) >> 4).Select(MetadataTokens.GuidHandle);
        }

        yield return ($"Guid Heap ({Guids().Count()})", AddColumns(CreateGrid<GuidHandle>("Guid Heap", file, Guids), columns =>
        {
            AddValue(columns, "Index", h => m.GetHeapOffset(h));
            AddValue(columns, "Length", _ => 16);
            AddValue(columns, "Value", h => m.GetGuid(h).ToString());
        }));

        IEnumerable<BlobHandle> Blobs()
        {
            return Walk(MetadataTokens.BlobHandle(0), m.GetNextHandle, static h => h.IsNil);
        }

        yield return ($"Blob Heap ({Blobs().Count()})", AddColumns(CreateGrid<BlobHandle>("Blob Heap", file, Blobs), columns =>
        {
            AddValue(columns, "Offset", h => m.GetHeapOffset(h), "X8");
            AddValue(columns, "Length", h => m.GetBlobReader(h).Length);
            AddValue(columns, "Value", h => m.GetBlobReader(h).ToHexString());
        }));
    }

    /// <summary>
    /// The entries of an offset-chained heap (#Strings, #US, #Blob). Handle 0 is a real first entry
    /// (the heap's empty slot) even though it is the nil handle, so each entry is added before
    /// <paramref name="next"/> is consulted; the walk ends at the next nil handle.
    /// </summary>
    private static IEnumerable<T> Walk<T>(T first, Func<T, T> next, Func<T, bool> isNil)
    {
        var handle = first;
        do
        {
            yield return handle;
            handle = next(handle);
        } while (!isNil(handle));
    }
}
