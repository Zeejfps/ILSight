using System.Collections.Concurrent;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Analysis;

/// <summary>The parts of the modules an analysis read that the library couldn't read (a method body, an instruction, a
/// token an instruction names, a method's locals, an attribute's value), so the analysis couldn't look in them: how
/// many in each module, and why the first couldn't be read. Thread-safe: an analysis may read modules in
/// parallel.</summary>
internal sealed class PassedOver
{
    private readonly ConcurrentDictionary<MetadataFile, (int Count, string Why)> _modules = new();

    public void Note(MetadataFile module, Exception why)
    {
        _modules.AddOrUpdate(module, (1, why.Message), static (_, noted) => (noted.Count + 1, noted.Why));
    }

    /// <summary>Each module with a part passed over: its file name, how many parts, and why the first; by file
    /// name.</summary>
    public IReadOnlyList<(string Name, int Count, string Why)> List()
    {
        return _modules
            .Select(static m => (Name: Path.GetFileName(m.Key.FileName), m.Value.Count, m.Value.Why))
            .OrderBy(static m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
