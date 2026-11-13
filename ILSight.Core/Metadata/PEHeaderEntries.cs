using System.Reflection.PortableExecutable;

using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata
{
	/// <summary>The grids of the PE header views: DOS, COFF and Optional headers, the data
	/// directories and the debug directory.</summary>
	internal static class PEHeaderEntries
	{
        /// <summary>The 64-byte legacy DOS header sitting at offset 0 of every PE file.</summary>
        public static GridSchema Dos(PEFile module)
        {
            return Header("DOS Header", module, () => DosEntries(module));
        }

        /// <summary>COFF (Common Object File Format) header — 20 bytes describing target machine, section
        /// count, link timestamp, and file-level characteristics. The Characteristics field is a
        /// 16-bit flags word whose bits its value's tooltip breaks out.</summary>
        public static GridSchema Coff(PEFile module)
        {
            return Header("COFF Header", module, () => CoffEntries(module));
        }

        /// <summary>PE Optional Header — image base, sizes, alignments, subsystem. Field widths flex
        /// between PE32 and PE32+ (Image Base / Stack &amp; Heap reserves go from 4 → 8 bytes,
        /// Base Of Data drops out on PE32+).</summary>
        public static GridSchema Optional(PEFile module)
        {
            return Header("Optional Header", module, () => OptionalEntries(module));
        }

        static GridSchema<Entry> Header(string key, PEFile module, Func<IEnumerable<Entry>> rows)
		{
		    return GridSchemas.AddColumns(GridSchemas.CreateGrid<Entry>(key, module, rows), columns =>
			{
				GridSchemas.AddValue(columns, "Member", e => e.Member);
				GridSchemas.AddValue(columns, "Offset", e => e.Offset);
				GridSchemas.AddValue(columns, "Size", e => e.Size);
				GridSchemas.AddValue(columns, "Value", e => e.Value, tooltip: e => e.Flags is null ? null : FilterStatePresenter.Explain(e.Flags, Convert.ToUInt32(e.Value)));
				GridSchemas.AddValue(columns, "Meaning", e => e.Meaning);
			});
		}

		static readonly FlagsSchema CharacteristicsBits = Bits(typeof(Characteristics), [
			"Relocation info stripped from file",
			"File is executable",
			"Line numbers stripped from file",
			"Local symbols stripped from file",
			"Aggressively trim working set",
			"Large address aware",
			"Reserved",
			"Bytes of machine words are reversed (Low)",
			"32-bit word machine",
			"Debugging info stripped from file in .DBG file",
			"If image is on removable media, copy and run from the swap file",
			"If image is on Net, copy and run from the swap file",
			"System",
			"DLL",
			"File should only be run on a UP machine",
			"Bytes of machine words are reversed (High)",
		]);

		static readonly FlagsSchema DllCharacteristicsBits = Bits(typeof(DllCharacteristics), [
			"Process Init (Reserved)",
			"Process Term (Reserved)",
			"Thread Init (Reserved)",
			"Thread Term (Reserved)",
			"Unused",
			"Image can handle a high entropy 64-bit virtual address space (ASLR)",
			"DLL can be relocated at load time",
			"Code integrity checks are enforced",
			"Image is NX compatible",
			"Isolation aware, but do not isolate the image",
			"Does not use structured exception handling (SEH)",
			"Do not bind the image",
			"Image must execute in an AppContainer",
			"Driver is a WDM Driver",
			"Image supports Control Flow Guard",
			"Image is Terminal Server aware",
		]);

        static FlagsSchema Bits(Type type, string[] meanings)
        {
            return new FlagsSchema
            {
                EnumType = type,
                MutexGroups = [],
                IndependentFlags = meanings.Select(static (m, i) => new IndependentFlag
                {
                    Name = m,
                    Bit = 1u << i,
                }).ToArray(),
            };
        }

        static List<Entry> DosEntries(PEFile module)
		{
			var reader = module.Reader.GetEntireImage().GetReader(0, 64);
			var entries = new List<Entry>(31) {
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_magic",
					Meaning = "Magic Number (MZ)",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_cblp",
					Meaning = "Bytes on last page of file",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_cp",
					Meaning = "Pages in file",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_crlc",
					Meaning = "Relocations",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_cparhdr",
					Meaning = "Size of header in paragraphs",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_minalloc",
					Meaning = "Minimum extra paragraphs needed",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_maxalloc",
					Meaning = "Maximum extra paragraphs needed",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_ss",
					Meaning = "Initial (relative) SS value",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_sp",
					Meaning = "Initial SP value",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_csum",
					Meaning = "Checksum",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_ip",
					Meaning = "Initial IP value",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_cs",
					Meaning = "Initial (relative) CS value",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_lfarlc",
					Meaning = "File address of relocation table",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_ovno",
					Meaning = "Overlay number",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res[0]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res[1]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res[2]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res[3]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_oemid",
					Meaning = "OEM identifier (for e_oeminfo)",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_oeminfo",
					Meaning = "OEM information; e_oemid specific",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[0]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[1]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[2]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[3]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[4]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[5]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[6]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[7]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[8]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadUInt16(),
					Size = 2,
					Member = "e_res2[9]",
					Meaning = "Reserved words",
				},
				new Entry
				{
					Offset = reader.Offset,
					Value = reader.ReadInt32(),
					Size = 4,
					Member = "e_lfanew",
					Meaning = "File address of new exe header",
				},
			};
			return entries;
		}

		static List<Entry> CoffEntries(PEFile module)
		{
			var headers = module.Reader.PEHeaders;
			var header = headers.CoffHeader;
			var linkerDateTime = DateTimeOffset.FromUnixTimeSeconds(unchecked((uint)header.TimeDateStamp)).DateTime;
			return new List<Entry> {
				new Entry
				{
					Offset = headers.CoffHeaderStartOffset,
					Value = (int)header.Machine,
					Size = 2,
					Member = "Machine",
					Meaning = header.Machine.ToString(),
				},
				new Entry
				{
					Offset = headers.CoffHeaderStartOffset + 2,
					Value = (int)header.NumberOfSections,
					Size = 2,
					Member = "Number of Sections",
					Meaning = "Number of sections; indicates size of the Section Table, which immediately follows the headers.",
				},
				new Entry
				{
					Offset = headers.CoffHeaderStartOffset + 4,
					Value = header.TimeDateStamp,
					Size = 4,
					Member = "Time/Date Stamp",
					Meaning = $"{linkerDateTime} (UTC) / {linkerDateTime.ToLocalTime()} - Time and date the file was created in seconds since January 1st 1970 00:00:00 or 0. Note that for deterministic builds this value is meaningless.",
				},
				new Entry
				{
					Offset = headers.CoffHeaderStartOffset + 8,
					Value = header.PointerToSymbolTable,
					Size = 4,
					Member = "Pointer to Symbol Table",
					Meaning = "Always 0 in .NET executables.",
				},
				new Entry
				{
					Offset = headers.CoffHeaderStartOffset + 12,
					Value = header.NumberOfSymbols,
					Size = 4,
					Member = "Number of Symbols",
					Meaning = "Always 0 in .NET executables.",
				},
				new Entry
				{
					Offset = headers.CoffHeaderStartOffset + 16,
					Value = (int)header.SizeOfOptionalHeader,
					Size = 2,
					Member = "Optional Header Size",
					Meaning = "Size of the optional header.",
				},
				new Entry
				{
					Offset = headers.CoffHeaderStartOffset + 18,
					Value = (int)header.Characteristics,
					Size = 2,
					Member = "Characteristics",
					Meaning = "Flags indicating attributes of the file.",
					Flags = CharacteristicsBits,
				},
			};
		}

		static List<Entry> OptionalEntries(PEFile module)
		{
			var headers = module.Reader.PEHeaders;
			var reader = module.Reader.GetEntireImage().GetReader(headers.PEHeaderStartOffset, 128);
			var header = DecompilerFacts.GetPEHeader(headers);
			var isPE32Plus = header.Magic == PEMagic.PE32Plus;

			var entries = new List<Entry>();
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt16(),
				Size = 2,
				Member = "Magic",
				Meaning = header.Magic.ToString(),
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadByte(),
				Size = 1,
				Member = "Major Linker Version",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadByte(),
				Size = 1,
				Member = "Minor Linker Version",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Code Size",
				Meaning = "Size of the code (text) section, or the sum of all code sections if there are multiple sections.",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Initialized Data Size",
				Meaning = "Size of the initialized data section, or the sum of all initialized data sections if there are multiple data sections.",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Uninitialized Data Size",
				Meaning = "Size of the uninitialized data section, or the sum of all uninitialized data sections if there are multiple uninitialized data sections.",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Entry Point RVA",
				Meaning = "RVA of entry point, needs to point to bytes 0xFF 0x25 followed by the RVA in a section marked execute / read for EXEs or 0 for DLLs",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Base Of Code",
				Meaning = "RVA of the code section.",
			});
			entries.Add(new Entry
			{
				Offset = isPE32Plus ? 0 : headers.PEHeaderStartOffset + reader.Offset,
				Value = isPE32Plus ? 0UL : reader.ReadUInt32(),
				Size = isPE32Plus ? 0 : 4,
				Member = "Base Of Data",
				Meaning = "PE32 only (not present in PE32Plus): RVA of the data section, relative to the Image Base.",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = isPE32Plus ? reader.ReadUInt64() : reader.ReadUInt32(),
				Size = isPE32Plus ? 8 : 4,
				Member = "Image Base",
				Meaning = "Shall be a multiple of 0x10000.",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Section Alignment",
				Meaning = "Shall be greater than File Alignment.",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "File Alignment",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt16(),
				Size = 2,
				Member = "Major OS Version",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt16(),
				Size = 2,
				Member = "Minor OS Version",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt16(),
				Size = 2,
				Member = "Major Image Version",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt16(),
				Size = 2,
				Member = "Minor Image Version",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt16(),
				Size = 2,
				Member = "Major Subsystem Version",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt16(),
				Size = 2,
				Member = "Minor Subsystem Version",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt32(),
				Size = 4,
				Member = "Win32VersionValue",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Image Size",
				Meaning = "Size, in bytes, of image, including all headers and padding; shall be a multiple of Section Alignment.",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Header Size",
				Meaning = "Combined size of MS-DOS Header, PE Header, PE Optional Header and padding; shall be a multiple of the file alignment.",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "File Checksum",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt16(),
				Size = 2,
				Member = "Subsystem",
				Meaning = header.Subsystem.ToString(),
			});
			var dllCharacteristics = reader.ReadUInt16();
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset - 2,
				Value = dllCharacteristics,
				Size = 2,
				Member = "DLL Characteristics",
				Meaning = header.DllCharacteristics.ToString(),
				Flags = DllCharacteristicsBits,
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = isPE32Plus ? reader.ReadUInt64() : reader.ReadUInt32(),
				Size = isPE32Plus ? 8 : 4,
				Member = "Stack Reserve Size",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = isPE32Plus ? reader.ReadUInt64() : reader.ReadUInt32(),
				Size = isPE32Plus ? 8 : 4,
				Member = "Stack Commit Size",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = isPE32Plus ? reader.ReadUInt64() : reader.ReadUInt32(),
				Size = isPE32Plus ? 8 : 4,
				Member = "Heap Reserve Size",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = isPE32Plus ? reader.ReadUInt64() : reader.ReadUInt32(),
				Size = isPE32Plus ? 8 : 4,
				Member = "Heap Commit Size",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadUInt32(),
				Size = 4,
				Member = "Loader Flags",
				Meaning = "",
			});
			entries.Add(new Entry
			{
				Offset = headers.PEHeaderStartOffset + reader.Offset,
				Value = reader.ReadInt32(),
				Size = 4,
				Member = "Number of Data Directories",
				Meaning = "",
			});
			return entries;
		}

		/// <summary>
		/// 15-row table of (RVA, Size, Containing-Section) triples that lives at the tail of the
		/// PE Optional Header. Each entry points at one well-known PE region (Export Table,
		/// Import Table, CLI Header, …).
		/// </summary>
		public static GridSchema DataDirectories(PEFile module)
		{
			var headers = module.Reader.PEHeaders;
			var header = DecompilerFacts.GetPEHeader(headers);
			(string Name, DirectoryEntry Entry)[] rows = [
				("Export Table", header.ExportTableDirectory),
				("Import Table", header.ImportTableDirectory),
				("Resource Table", header.ResourceTableDirectory),
				("Exception Table", header.ExceptionTableDirectory),
				("Certificate Table", header.CertificateTableDirectory),
				("Base Relocation Table", header.BaseRelocationTableDirectory),
				("Debug Table", header.DebugTableDirectory),
				("Copyright Table", header.CopyrightTableDirectory),
				("Global Pointer Table", header.GlobalPointerTableDirectory),
				("Thread Local Storage Table", header.ThreadLocalStorageTableDirectory),
				("Load Config", header.LoadConfigTableDirectory),
				("Bound Import", header.BoundImportTableDirectory),
				("Import Address Table", header.ImportAddressTableDirectory),
				("Delay Import Descriptor", header.DelayImportTableDirectory),
				("CLI Header", header.CorHeaderTableDirectory),
			];
			return GridSchemas.AddColumns(GridSchemas.CreateGrid<(string Name, DirectoryEntry Entry)>("Data Directories", module, () => rows), columns =>
			{
				GridSchemas.AddValue(columns, "Name", r => r.Name);
				GridSchemas.AddValue(columns, "RVA", r => r.Entry.RelativeVirtualAddress, "X8");
				GridSchemas.AddValue(columns, "Size", r => r.Entry.Size, "X8");
				GridSchemas.AddValue(columns, "Section", r =>
				{
					var index = headers.GetContainingSectionIndex(r.Entry.RelativeVirtualAddress);
					return index >= 0 ? headers.SectionHeaders[index].Name : "";
				});
			});
		}

		/// <summary>
		/// Each entry of the PE debug directory: timestamp, version, type, and the size /
		/// addresses of the raw debug payload.
		/// </summary>
		public static GridSchema DebugDirectory(PEFile module)
		{
			var reader = module.Reader;
			return GridSchemas.AddColumns(GridSchemas.CreateGrid<DebugDirectoryEntry>("Debug Directory", module, () => reader.ReadDebugDirectory()), columns =>
			{
				GridSchemas.AddValue(columns, "Timestamp", e => e.Stamp);
				GridSchemas.AddValue(columns, "MajorVersion", e => e.MajorVersion);
				GridSchemas.AddValue(columns, "MinorVersion", e => e.MinorVersion);
				GridSchemas.AddValue(columns, "Type", e => e.Type.ToString());
				GridSchemas.AddValue(columns, "SizeOfRawData", e => e.DataSize);
				GridSchemas.AddValue(columns, "AddressOfRawData", e => e.DataRelativeVirtualAddress, "X8");
				GridSchemas.AddValue(columns, "PointerToRawData", e => e.DataPointer, "X8");
				GridSchemas.AddValue(columns, "RawData", e =>
				{
					var data = reader.GetEntireImage().GetContent(reader.IsLoadedImage ? e.DataRelativeVirtualAddress : e.DataPointer, e.DataSize);
					return data.ToHexString(data.Length);
				});
			});
		}

        /// <summary>One row of a PE header: <see cref="Value"/> is <see cref="Size"/> bytes at
        /// <see cref="Offset"/>, and <see cref="Flags"/> breaks a flags value down.</summary>
        private sealed record Entry
        {
            public required int Offset { get; init; }

            public required object? Value { get; init; }

            public required int Size { get; init; }

            public required string Member { get; init; }

            public required string Meaning { get; init; }

            public FlagsSchema? Flags { get; init; }
        }
	}
}
