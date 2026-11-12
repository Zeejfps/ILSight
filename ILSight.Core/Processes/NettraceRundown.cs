// Adapted from ILSpy's Processes/NettraceRundownReader
// (MIT License, Copyright (c) 2026 Christoph Wille): see LICENSE-ILSpy.txt for the terms.

using System.Text;

namespace ILSight.Core.Processes;

/// <summary>
/// Reads the modules out of the nettrace stream of an EventPipe rundown (the container format is
/// microsoft/perfview's, src/TraceEvent/EventPipe). No general nettrace reader: it walks the
/// FastSerialization object stream far enough to find the loader's events and decodes those, and steps over
/// the rest, as stacks, sequence points and other providers' payloads, by their sizes.
/// </summary>
internal static class NettraceRundown
{
    // FastSerialization's tags.
    private const byte TagNullReference = 1;
    private const byte TagBeginObject = 4;
    private const byte TagBeginPrivateObject = 5;
    private const byte TagEndObject = 6;

    /// <summary>The newest container this reads. Version 5 only adds tags a version 4 reader may ignore;
    /// past that, the layouts below are not known to hold.</summary>
    private const int MaxSupportedTraceVersion = 5;

    private const int CompressedFlagMetadataId = 0x01;
    private const int CompressedFlagCaptureThreadAndSequence = 0x02;
    private const int CompressedFlagThreadId = 0x04;
    private const int CompressedFlagStackId = 0x08;
    private const int CompressedFlagActivityId = 0x10;
    private const int CompressedFlagRelatedActivityId = 0x20;
    private const int CompressedFlagDataLength = 0x80;

    // The CLR's providers are manifest-based, so their events come with no name and are known by provider
    // and id together, as the ids overlap and mean different things in each. The rundown sends the "DC"
    // (data collection) events as the session stops; the runtime provider reports what loads while it's open.
    private const string RundownProviderName = "Microsoft-Windows-DotNETRuntimeRundown";
    private const string RuntimeProviderName = "Microsoft-Windows-DotNETRuntime";

    private const int RundownModuleDCStart = 153;
    private const int RundownModuleDCStop = 154;
    private const int RundownAssemblyDCStart = 155;
    private const int RundownAssemblyDCStop = 156;
    private const int RuntimeModuleLoad = 152;
    private const int RuntimeAssemblyLoad = 154;

    private const int MaxNameLength = 1024;

    private sealed record EventMetadata
    {
        public required string ProviderName { get; init; }

        public required int EventId { get; init; }

        public required int Version { get; init; }
    }

    private sealed record ModuleEvent
    {
        public required long AssemblyId { get; init; }

        public required string IlPath { get; init; }

        public required string NativePath { get; init; }
    }

    /// <summary>What the events being read have told so far.</summary>
    private sealed class Reading
    {
        public readonly Dictionary<int, EventMetadata> Metadata = new();
        public readonly List<ModuleEvent> Modules = new();
        public readonly Dictionary<long, string> AssemblyNames = new();
    }

    /// <summary>The modules the trace in <paramref name="stream"/>, which must be seekable, reports, in the order it reports them,
    /// once each may have been reported more than once; or why it could not be read.</summary>
    public static RundownRead Read(Stream stream)
    {
        try
        {
            return new RundownRead.Read(ReadModules(stream));
        }
        catch (InvalidDataException e)
        {
            return new RundownRead.Corrupt(e.Message);
        }
        catch (EndOfStreamException e)
        {
            return new RundownRead.Corrupt(e.Message);
        }
        catch (IOException e)
        {
            return new RundownRead.Corrupt(e.Message);
        }
        catch (OverflowException e)
        {
            return new RundownRead.Corrupt(e.Message);
        }
    }

    private static IReadOnlyList<RundownModule> ReadModules(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        ReadStreamHeader(reader);

        var reading = new Reading();
        while (true)
        {
            var tag = reader.ReadByte();
            if (tag == TagNullReference)
            {
                break; // the end of the object stream
            }

            if (tag != TagBeginObject && tag != TagBeginPrivateObject)
            {
                throw new InvalidDataException($"Unexpected FastSerialization tag 0x{tag:X2} in the nettrace stream.");
            }

            var typeName = ReadTypeName(reader, out var version);
            switch (typeName)
            {
                case "Trace":
                    if (version > MaxSupportedTraceVersion)
                    {
                        throw new InvalidDataException($"Nettrace version {version} is newer than this reads.");
                    }

                    SkipTraceObject(reader);
                    break;
                case "MetadataBlock":
                    ReadBlock(reader, (payload, id, end) => ReadMetadataEvent(payload, id, end, reading));
                    break;
                case "EventBlock":
                    ReadBlock(reader, (payload, id, end) => ReadEvent(payload, id, end, reading));
                    break;
                default:
                    // StackBlock, SPBlock, and any kind of block added later: a block gives its length,
                    // so it can be stepped over whole.
                    SkipBlock(reader);
                    break;
            }
            ExpectTag(reader, TagEndObject);
        }

        return reading.Modules
            .Select(module => new RundownModule
            {
                IlPath = module.IlPath,
                NativePath = module.NativePath,
                AssemblyName = reading.AssemblyNames.GetValueOrDefault(module.AssemblyId),
            })
            .ToList();
    }

    private static void ReadStreamHeader(BinaryReader reader)
    {
        var magic = reader.ReadBytes(8);
        var text = Encoding.ASCII.GetString(magic);
        if (magic.Length < 8 || text != "Nettrace")
        {
            throw new InvalidDataException("The stream does not start with the Nettrace magic.");
        }

        var serializer = ReadLengthPrefixedAsciiString(reader);
        if (!serializer.StartsWith("!FastSerialization", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected nettrace serializer '{serializer}'.");
        }
    }

    /// <summary>The name of the type of the object begun. The type is an object itself: a begin tag, a null
    /// type of its own, the version and the oldest version that reads it, the name, and an end tag.</summary>
    private static string ReadTypeName(BinaryReader reader, out int version)
    {
        ExpectTag(reader, TagBeginObject, TagBeginPrivateObject);
        ExpectTag(reader, TagNullReference);
        version = reader.ReadInt32();
        reader.ReadInt32(); // the oldest reader's version
        var name = ReadLengthPrefixedAsciiString(reader);
        ExpectTag(reader, TagEndObject);
        return name;
    }

    private static string ReadLengthPrefixedAsciiString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length is < 0 or > MaxNameLength)
        {
            throw new InvalidDataException($"Implausible nettrace string length {length}.");
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length < length)
        {
            throw new EndOfStreamException("The nettrace stream ends inside a string.");
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static void ExpectTag(BinaryReader reader, byte expected)
    {
        var tag = reader.ReadByte();
        if (tag != expected)
        {
            throw new InvalidDataException($"Expected FastSerialization tag 0x{expected:X2}, found 0x{tag:X2}.");
        }
    }

    private static void ExpectTag(BinaryReader reader, byte expected, byte alternative)
    {
        var tag = reader.ReadByte();
        if (tag != expected && tag != alternative)
        {
            throw new InvalidDataException($"Expected FastSerialization tag 0x{expected:X2}, found 0x{tag:X2}.");
        }
    }

    /// <summary>Steps over the trace object: the sync time in UTC (8 shorts), the sync time and frequency of
    /// the performance counter, and four ints: the pointer size, process id, processor count and sampling rate.</summary>
    private static void SkipTraceObject(BinaryReader reader)
    {
        Skip(reader, 8 * sizeof(short) + 2 * sizeof(long) + 4 * sizeof(int));
    }

    /// <summary>Walks the events of a metadata or event block, handing each one's payload to
    /// <paramref name="onEvent"/> with its metadata id and the position where the payload ends, which no
    /// field read out of it may pass.</summary>
    private static void ReadBlock(BinaryReader reader, Action<BinaryReader, int, long> onEvent)
    {
        var blockEnd = BeginBlock(reader, out var compressed);

        // The compressed layout leaves out a field that repeats, so the last event's value carries over.
        // Blocks decode on their own, so this starts afresh in each.
        var metadataId = 0;
        var payloadSize = 0;

        while (reader.BaseStream.Position < blockEnd)
        {
            if (compressed)
            {
                ReadCompressedEventHeader(reader, ref metadataId, ref payloadSize);
            }
            else
            {
                ReadUncompressedEventHeader(reader, out metadataId, out payloadSize);
            }

            if (payloadSize < 0)
            {
                throw new InvalidDataException($"Implausible nettrace payload size {payloadSize}.");
            }

            var payloadEnd = reader.BaseStream.Position + payloadSize;
            if (payloadEnd > blockEnd)
            {
                throw new InvalidDataException("A nettrace event's payload runs past the end of its block.");
            }

            onEvent(reader, metadataId, payloadEnd);
            SeekTo(reader, payloadEnd);

            if (!compressed)
            {
                AlignTo4(reader);
            }
        }
        SeekTo(reader, blockEnd);
    }

    /// <summary>Steps over a block by its size alone. Only event and metadata blocks begin with the header
    /// <see cref="BeginBlock"/> reads: a stack block begins with its first id and count, a sequence point
    /// block with a timestamp, so reading theirs as that header would take the wrong bytes for its size.</summary>
    private static void SkipBlock(BinaryReader reader)
    {
        SeekTo(reader, ReadBlockEnd(reader));
    }

    /// <summary>Reads a block's size, and gives the position where the block ends. Blocks are aligned to 4
    /// bytes from the start of the stream.</summary>
    private static long ReadBlockEnd(BinaryReader reader)
    {
        var blockSize = reader.ReadInt32();
        if (blockSize < 0)
        {
            throw new InvalidDataException($"Implausible nettrace block size {blockSize}.");
        }

        AlignTo4(reader);
        var blockEnd = reader.BaseStream.Position + blockSize;
        if (blockEnd > reader.BaseStream.Length)
        {
            throw new EndOfStreamException("The nettrace stream ends inside a block.");
        }

        return blockEnd;
    }

    /// <summary>Reads an event or metadata block's size and header, and gives the position where the block
    /// ends.</summary>
    private static long BeginBlock(BinaryReader reader, out bool compressed)
    {
        var blockEnd = ReadBlockEnd(reader);

        var headerSize = reader.ReadInt16();
        var flags = reader.ReadInt16();
        reader.ReadInt64(); // the earliest timestamp
        reader.ReadInt64(); // the latest timestamp
        const int readHeaderBytes = sizeof(short) * 2 + sizeof(long) * 2;
        if (headerSize > readHeaderBytes)
        {
            Skip(reader, headerSize - readHeaderBytes);
        }

        if (reader.BaseStream.Position > blockEnd)
        {
            throw new InvalidDataException("A nettrace block's header runs past the end of the block.");
        }

        compressed = (flags & 1) != 0;
        return blockEnd;
    }

    private static void AlignTo4(BinaryReader reader)
    {
        var padding = (4 - reader.BaseStream.Position % 4) % 4;
        if (padding != 0)
        {
            Skip(reader, padding);
        }
    }

    /// <summary>Moves past <paramref name="count"/> bytes, which must be there: a seek alone would go past the
    /// end without a word, and the next read would fail far from the cause.</summary>
    private static void Skip(BinaryReader reader, long count)
    {
        SeekTo(reader, reader.BaseStream.Position + count);
    }

    private static void SeekTo(BinaryReader reader, long position)
    {
        if (position > reader.BaseStream.Length)
        {
            throw new EndOfStreamException("The nettrace stream ends inside an object.");
        }

        reader.BaseStream.Seek(position, SeekOrigin.Begin);
    }

    private static void ReadCompressedEventHeader(BinaryReader reader, ref int metadataId, ref int payloadSize)
    {
        var flags = reader.ReadByte();
        if ((flags & CompressedFlagMetadataId) != 0)
        {
            metadataId = checked((int)ReadVarUInt(reader));
        }

        if ((flags & CompressedFlagCaptureThreadAndSequence) != 0)
        {
            ReadVarUInt(reader); // the change in sequence number
            ReadVarUInt(reader); // the capturing thread
            ReadVarUInt(reader); // the capturing processor
        }
        if ((flags & CompressedFlagThreadId) != 0)
        {
            ReadVarUInt(reader);
        }

        if ((flags & CompressedFlagStackId) != 0)
        {
            ReadVarUInt(reader);
        }

        ReadVarUInt(reader); // the change in timestamp
        if ((flags & CompressedFlagActivityId) != 0)
        {
            Skip(reader, 16);
        }

        if ((flags & CompressedFlagRelatedActivityId) != 0)
        {
            Skip(reader, 16);
        }

        if ((flags & CompressedFlagDataLength) != 0)
        {
            payloadSize = checked((int)ReadVarUInt(reader));
        }
    }

    private static void ReadUncompressedEventHeader(BinaryReader reader, out int metadataId, out int payloadSize)
    {
        reader.ReadInt32(); // the event's size
        metadataId = reader.ReadInt32();
        reader.ReadInt32(); // the sequence number
        reader.ReadInt64(); // the thread
        reader.ReadInt64(); // the capturing thread
        reader.ReadInt32(); // the processor
        reader.ReadInt32(); // the stack
        reader.ReadInt64(); // the timestamp
        Skip(reader, 32); // the activity and related activity
        payloadSize = reader.ReadInt32();
    }

    private static ulong ReadVarUInt(BinaryReader reader)
    {
        ulong value = 0;
        var shift = 0;
        while (true)
        {
            var b = reader.ReadByte();
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return value;
            }

            shift += 7;
            if (shift > 63)
            {
                throw new InvalidDataException("A malformed variable-length integer in the nettrace stream.");
            }
        }
    }

    /// <summary>An event of a metadata block describes a kind of event: which provider and event the
    /// metadata id given stands for in the event blocks after it.</summary>
    private static void ReadMetadataEvent(BinaryReader reader, int metadataId, long payloadEnd, Reading reading)
    {
        if (metadataId != 0)
        {
            return; // only the metadata records themselves matter here
        }

        var id = reader.ReadInt32();
        var providerName = ReadUtf16NullTerminated(reader, payloadEnd);
        var eventId = reader.ReadInt32();
        ReadUtf16NullTerminated(reader, payloadEnd); // the event's name, empty for a manifest-based provider
        reader.ReadInt64(); // the keywords
        var version = reader.ReadInt32();
        reading.Metadata[id] = new EventMetadata { ProviderName = providerName, EventId = eventId, Version = version };
    }

    private static void ReadEvent(BinaryReader reader, int metadataId, long payloadEnd, Reading reading)
    {
        if (!reading.Metadata.TryGetValue(metadataId, out var meta))
        {
            return;
        }

        // ModuleLoad, ModuleDCStart and ModuleDCStop share a layout; DomainModule* and ModuleRange*, named
        // alike, carry other fields and are left alone.
        if (IsModuleEvent(meta))
        {
            reader.ReadInt64(); // the module
            var assemblyId = reader.ReadInt64();
            reader.ReadInt32(); // the module's flags
            reader.ReadInt32(); // reserved
            var ilPath = ReadUtf16NullTerminated(reader, payloadEnd);
            var nativePath = ReadUtf16NullTerminated(reader, payloadEnd);
            reading.Modules.Add(new ModuleEvent { AssemblyId = assemblyId, IlPath = ilPath, NativePath = nativePath });
        }
        else if (IsAssemblyEvent(meta))
        {
            var assemblyId = reader.ReadInt64();
            reader.ReadInt64(); // the app domain
            if (meta.Version >= 1)
            {
                reader.ReadInt64(); // the binding
            }

            reader.ReadInt32(); // the assembly's flags
            var fullName = ReadUtf16NullTerminated(reader, payloadEnd);
            if (fullName.Length > 0)
            {
                // "Foo, Version=1.0.0.0, Culture=...": the simple name is enough to label a module with no file.
                var comma = fullName.IndexOf(',');
                reading.AssemblyNames[assemblyId] = comma > 0 ? fullName[..comma] : fullName;
            }
        }
    }

    private static bool IsModuleEvent(EventMetadata meta)
    {
        return meta.ProviderName switch
        {
            RundownProviderName => meta.EventId is RundownModuleDCStart or RundownModuleDCStop,
            RuntimeProviderName => meta.EventId == RuntimeModuleLoad,
            _ => false,
        };
    }

    private static bool IsAssemblyEvent(EventMetadata meta)
    {
        return meta.ProviderName switch
        {
            RundownProviderName => meta.EventId is RundownAssemblyDCStart or RundownAssemblyDCStop,
            RuntimeProviderName => meta.EventId == RuntimeAssemblyLoad,
            _ => false,
        };
    }

    /// <summary>A UTF-16 string ended by a zero word, read no further than <paramref name="payloadEnd"/>. The
    /// bound keeps a payload whose layout is not what its event id promised from being followed out of its
    /// event and through the rest of the stream in search of a terminator.</summary>
    internal static string ReadUtf16NullTerminated(BinaryReader reader, long payloadEnd)
    {
        var builder = new StringBuilder();
        while (reader.BaseStream.Position + sizeof(ushort) <= payloadEnd)
        {
            var c = reader.ReadUInt16();
            if (c == 0)
            {
                return builder.ToString();
            }

            builder.Append((char)c);
        }
        throw new InvalidDataException("A nettrace event's payload ends inside a string.");
    }
}
