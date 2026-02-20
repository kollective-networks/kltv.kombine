/*---------------------------------------------------------------------------------------------------------

	Kombine Bin2obj Extension

	It generates COFF object files from binary input files, embedding the data as sections in the object file.

	(C) Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Provides functionality to generate COFF object files from binary input files.
/// </summary>
/// <remarks>The Bin2obj class offers methods to automate the conversion of binary files into COFF object files,
/// embedding the data as sections. It includes utilities to determine whether files require processing based on
/// modification times and existence. This class is intended for use in build processes where binary resources
/// need to be embedded in object files.
/// </remarks>
public class Bin2obj {

	/// <summary>
	/// Sets the machine type for the COFF file (e.g., "x64", "x86", "arm", "arm64").
	/// Not used yet, but it can be used in the future to generate the appropriate COFF file based on the target architecture.
	/// COFF generation code is more or less prepared
	/// </summary>
	public string Machine { get; set; } = "x64";

	/// <summary>
	/// Holds all the symbols generated during the process, with the key being the friendly name 
	/// and the value being the symbol name. This can be used for referencing the generated symbols.
	/// </summary>
	public Dictionary<string, string> Symbols { get; private set; } = new Dictionary<string, string>();

	/// <summary>
	/// Generates COFF object files for each binary file.
	/// </summary>
	/// <param name="bin">List of input binary files</param>
	/// <param name="obj">List of output object files</param>
	/// <returns>true if everything fine. False otherwise.</returns>
	public bool Generate(KList bin, KList obj) {
		// Clear the symbol table before processing
		Symbols.Clear();
		// First compare if the number of input files matches the number of output files
		if (bin.Count() != obj.Count()) {
			Msg.PrintError("Bin2obj: The number of input binary files must match the number of output object files.");
			return false;
		}
		// Create the output folder(s)
		KList folders = obj.AsFolders();
		Folders.Create(folders);
		// Now we can generate the obj files only for the ones that are missing or outdated
		for (int i = 0; i < bin.Count(); i++) {
			string binFile = bin[i];
			string objFile = obj[i];
			Msg.PrintTask($"Bin2obj: Processing {binFile}:");
			// We generate a variable name and a friendly name for the file,
			// and we store it in the symbol table
			string varName = GetVarName(binFile);
			string friendlyName = GetFriendlyName(binFile);
			Symbols.Add(friendlyName, varName);
			// Check if the file should be processed
			if (ShouldProcess(binFile, objFile) == false) {
				Msg.PrintTaskSuccess(" No changes. Skipping.");
				continue;
			}
			// Generate the obj file
			try {
				byte[] data = File.ReadAllBytes(binFile);
				GenerateCoffFile(data, varName, objFile);
				Msg.PrintTaskSuccess(" Generated successfully.");
			} catch (Exception ex) {
				Msg.PrintTaskError(" Failed to generate: " + ex.Message);
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Generates a single COFF object file with multiple sections, one for each binary file.
	/// </summary>
	/// <param name="bin">List of input binary files</param>
	/// <param name="obj">Single output object file</param>
	/// <returns>true if everything fine. False otherwise.</returns>
	public bool Generate(KList bin, KValue obj) {
		// Clear the symbol table before processing
		Symbols.Clear();
		// Check if the output file should be processed (based on the newest input file)
		bool shouldProcess = false;
		// If destination file does not exist, we should process
		if (Files.Exists(obj) == false) {
			Msg.Print($"Bin2obj: Output file {obj} does not exist. It will be processed.", Msg.LogLevels.Verbose);
			shouldProcess = true;
		} else {
			Msg.Print($"Bin2obj: Output file {obj} exists. Checking for changes...", Msg.LogLevels.Verbose);
			long dsttime = Files.GetModifiedTime(obj);
			foreach (KValue binFile in bin) {
				if (Files.Exists(binFile) == false) {
					Msg.PrintAndAbort("Bin2obj: Source file " + binFile + " sent to be processed is not found.");
					return false;
				}
				long modTime = Files.GetModifiedTime(binFile);
				if (modTime > dsttime) {
					Msg.Print($"Bin2obj: Source {binFile} is newer than destination. It will be processed.", Msg.LogLevels.Verbose);
					shouldProcess = true;
					break;
				}
			}
		}
		// Create the symbol table even if we do not need to process
		foreach (KValue binFile in bin) {
			string varName = GetVarName(binFile);
			string friendlyName = GetFriendlyName(binFile);
			Symbols.Add(friendlyName, varName);
		}
		Msg.PrintTask($"Bin2obj: Processing {obj}:");
		if (!shouldProcess) {
			Msg.PrintTaskSuccess($"Bin2obj: No changes for {obj}. Skipping.");
			return true;
		}
		// Create the output folder(s)
		KValue folders = obj.AsFolder();
		Folders.Create(folders);
		// Generate the single obj file with multiple sections
		try {
			List<byte[]> datas = new List<byte[]>();
			List<string> names = new List<string>();
			foreach (KValue binFile in bin) {
				datas.Add(File.ReadAllBytes(binFile));
				names.Add(GetVarName(binFile));
			}
			GenerateCoffFileMultipleSections(datas, names, obj);
			Msg.PrintTaskSuccess($"Bin2obj: Generated {obj} successfully.");
		} catch (Exception ex) {
			Msg.PrintTaskError(" Failed to generate: " + ex.Message);
			return false;
		}
		return true;
	}

	/// <summary>
	/// Returns a valid symbol name for the given file path.
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	private string GetVarName(KValue filePath) {
		string varname = "var" + Path.GetFileName(filePath).Replace('.', '_').Replace('-', '_');
		string objectname = filePath.GetHashCode64().ToString();
		return varname + objectname;
	}

	/// <summary>
	/// Returns a friendly name for the file path.
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	private string GetFriendlyName(string filePath) {
		return filePath.Replace('/', '.').Replace('\\', '.').ToLower();
	}

	/// <summary>
	/// Checks if the file should be processed
	/// </summary>
	/// <param name="src">Source file</param>
	/// <param name="dest">Destination file</param>
	/// <returns>True if should be processed. False otherwise</returns>
	static private bool ShouldProcess(KValue src, KValue dest) {
		// Check if the source file exists
		if (Files.Exists(src) == false) {
			Msg.PrintAndAbort("Bin2obj: Source file " + src + " sent to be processed is not found.");
			return false;
		}
		// Check if the destination file exists. If not exists, it should be built
		if (Files.Exists(dest) == false) {
			Msg.Print("Bin2obj: File " + dest + " does not exists. It will be processed.", Msg.LogLevels.Verbose);
			return true;
		}
		// Check for the source file modification time
		if (Files.GetModifiedTime(src) > Files.GetModifiedTime(dest)) {
			Msg.Print($"Bin2obj: Source {src} newer than destination. It will be processed.", Msg.LogLevels.Verbose);
			return true;
		}
		Msg.Print($"Bin2obj: Source {src} older than destination. No Process.", Msg.LogLevels.Verbose);
		return false;
	}

	public enum CoffMachine : ushort {
		I386 = 0x014c,
		Amd64 = 0x8664,
		Arm = 0x01c0,
		Arm64 = 0xAA64
	}

	public enum CoffCharacteristics : ushort {
		RelocsStripped = 0x0001,
		ExecutableImage = 0x0002,
		LineNumsStripped = 0x0004,
		LocalSymsStripped = 0x0008,
		AggressiveWsTrim = 0x0010,
		LargeAddressAware = 0x0020,
		BytesReversedLo = 0x0080,
		Machine32Bit = 0x0100,
		DebugStripped = 0x0200,
		RemovableRunFromSwap = 0x0400,
		NetRunFromSwap = 0x0800,
		System = 0x1000,
		Dll = 0x2000,
		UpSystemOnly = 0x4000,
		BytesReversedHi = 0x8000
	}

	public enum CoffSectionCharacteristics : uint {
		TypeNoPad = 0x00000008,
		CntCode = 0x00000020,
		CntInitializedData = 0x00000040,
		CntUninitializedData = 0x00000080,
		LnkOther = 0x00000100,
		LnkInfo = 0x00000200,
		LnkRemove = 0x00000800,
		LnkComdat = 0x00001000,
		GpRel = 0x00008000,
		MemPurgeable = 0x00020000,
		Mem16Bit = 0x00020000,
		MemLocked = 0x00040000,
		MemPreload = 0x00080000,
		Align1Bytes = 0x00100000,
		Align2Bytes = 0x00200000,
		Align4Bytes = 0x00300000,
		Align8Bytes = 0x00400000,
		Align16Bytes = 0x00500000,
		Align32Bytes = 0x00600000,
		Align64Bytes = 0x00700000,
		Align128Bytes = 0x00800000,
		Align256Bytes = 0x00900000,
		Align512Bytes = 0x00A00000,
		Align1024Bytes = 0x00B00000,
		Align2048Bytes = 0x00C00000,
		Align4096Bytes = 0x00D00000,
		Align8192Bytes = 0x00E00000,
		LnkNRelocOvfl = 0x01000000,
		MemDiscardable = 0x02000000,
		MemNotCached = 0x04000000,
		MemNotPaged = 0x08000000,
		MemShared = 0x10000000,
		MemExecute = 0x20000000,
		MemRead = 0x40000000,
		MemWrite = 0x80000000
	}

	public enum CoffSymbolType : ushort {
		Null = 0,
		Void = 1,
		Char = 2,
		Short = 3,
		Int = 4,
		Long = 5,
		Float = 6,
		Double = 7,
		Struct = 8,
		Union = 9,
		Enum = 10,
		Moe = 11,
		Byte = 12,
		Word = 13,
		Uint = 14,
		Dword = 15
	}

	public enum CoffSymbolClass : byte {
		EndOfFunction = 0xFF,
		Null = 0,
		Automatic = 1,
		External = 2,
		Static = 3,
		Register = 4,
		ExternalDef = 5,
		Label = 6,
		UndefinedLabel = 7,
		MemberOfStruct = 8,
		Argument = 9,
		StructTag = 10,
		MemberOfUnion = 11,
		UnionTag = 12,
		TypeDefinition = 13,
		UndefinedStatic = 14,
		EnumTag = 15,
		MemberOfEnum = 16,
		RegisterParam = 17,
		BitField = 18,
		Block = 100,
		Function = 101,
		EndOfStruct = 102,
		File = 103,
		Section = 104,
		WeakExternal = 105,
		ClrToken = 107
	}

	private static readonly Dictionary<string, CoffMachine> MachineTypes = new() {
		["x86"] = CoffMachine.I386,
		["x64"] = CoffMachine.Amd64,
		["arm"] = CoffMachine.Arm,
		["arm64"] = CoffMachine.Arm64
	};

	private void GenerateCoffFile(byte[] data, string symbolName, string outputFile) {
		int paddingLen = (Machine == "arm64") ? (4 - data.Length % 4) % 4 : 0;
		byte[] padding = new byte[paddingLen];
		uint sizeValue = (uint)data.Length;
		byte[] sizeBytes = BitConverter.GetBytes(sizeValue);
		List<byte> sectionData = new List<byte>();
		sectionData.AddRange(data);
		sectionData.AddRange(padding);
		sectionData.AddRange(sizeBytes);
		List<Section> sections = new() { new Section(sectionData.ToArray(), ".data") };
		List<Symbol> symbols = new() {
			new Symbol(symbolName, 1, 0, CoffSymbolType.Null, CoffSymbolClass.External),
			new Symbol(symbolName + "_size", 1, (uint)(data.Length + paddingLen), CoffSymbolType.Uint, CoffSymbolClass.External)
		};
		WriteCoffFile(sections, symbols, outputFile);
	}

	private void GenerateCoffFileMultipleSections(List<byte[]> datas, List<string> names, string outputFile) {
		List<byte> allData = new List<byte>();
		List<Symbol> symbols = new List<Symbol>();
		uint offset = 0;
		for (int i = 0; i < datas.Count; i++) {
			byte[] data = datas[i];
			int paddingLen = (Machine == "arm64") ? (4 - data.Length % 4) % 4 : 0;
			byte[] padding = new byte[paddingLen];
			uint sizeValue = (uint)data.Length;
			byte[] sizeBytes = BitConverter.GetBytes(sizeValue);
			symbols.Add(new Symbol(names[i], 1, offset, CoffSymbolType.Null, CoffSymbolClass.External));
			uint sizeOffset = offset + (uint)data.Length + (uint)paddingLen;
			symbols.Add(new Symbol(names[i] + "_size", 1, sizeOffset, CoffSymbolType.Uint, CoffSymbolClass.External));
			allData.AddRange(data);
			allData.AddRange(padding);
			allData.AddRange(sizeBytes);
			offset += (uint)(data.Length + paddingLen + 4);
		}
		List<Section> sections = new() { new Section(allData.ToArray(), ".data") };
		WriteCoffFile(sections, symbols, outputFile);
	}

	private void WriteCoffFile(List<Section> sections, List<Symbol> symbols, string outputFile) {
		using FileStream fs = new(outputFile, FileMode.Create);
		using BinaryWriter writer = new(fs);

		uint symbolTableOffset = (uint)(20 + sections.Count * 40); // After header and section headers
		foreach (var section in sections) {
			symbolTableOffset += (uint)section.Data.Length;
		}

		// COFF header
		writer.Write((ushort)MachineTypes[Machine]); // Machine
		writer.Write((ushort)sections.Count); // NumberOfSections
		writer.Write((uint)DateTimeOffset.Now.ToUnixTimeSeconds()); // TimeDateStamp
		writer.Write(symbolTableOffset); // PointerToSymbolTable
		writer.Write((uint)symbols.Count); // NumberOfSymbols
		writer.Write((ushort)0); // SizeOfOptionalHeader
		writer.Write((ushort)0); // Characteristics

		// Section headers
		uint dataOffset = (uint)(20 + sections.Count * 40); // After header and section headers
		for (int i = 0; i < sections.Count; i++) {
			var section = sections[i];
			writer.Write(section.Name.PadRight(8, '\0').ToCharArray()); // Name
			writer.Write(0u); // VirtualSize
			writer.Write(0u); // VirtualAddress
			writer.Write((uint)section.Data.Length); // SizeOfRawData
			writer.Write(dataOffset); // PointerToRawData
			writer.Write(0u); // PointerToRelocations
			writer.Write(0u); // PointerToLinenumbers
			writer.Write((ushort)0); // NumberOfRelocations
			writer.Write((ushort)0); // NumberOfLinenumbers
			writer.Write((uint)CoffSectionCharacteristics.CntInitializedData | (uint)CoffSectionCharacteristics.MemRead); // Characteristics
			dataOffset += (uint)section.Data.Length;
		}

		// Section data
		foreach (var section in sections) {
			writer.Write(section.Data);
		}

		// Symbol table
		List<string> longNames = new List<string>();
		Dictionary<string, uint> nameOffsets = new Dictionary<string, uint>();
		uint stringOffset = 4; // after TotalSize
		foreach (var sym in symbols) {
			if (sym.Name.Length > 8) {
				if (!nameOffsets.ContainsKey(sym.Name)) {
					nameOffsets[sym.Name] = stringOffset;
					stringOffset += (uint)Encoding.ASCII.GetByteCount(sym.Name) + 1;
					longNames.Add(sym.Name + "\0");
				}
			}
		}
		foreach (var symbol in symbols) {
			if (symbol.Name.Length <= 8) {
				writer.Write(Encoding.ASCII.GetBytes(symbol.Name.PadRight(8, '\0')));
			} else {
				writer.Write(0u); // zeroes
				writer.Write(nameOffsets[symbol.Name]);
			}
			writer.Write(symbol.Value); // Value
			writer.Write(symbol.Section); // Section
			writer.Write((ushort)symbol.Type); // Type
			writer.Write((byte)symbol.StorageClass); // StorageClass
			writer.Write(symbol.AuxCount); // NumberOfAuxSymbols
		}
		if (longNames.Any()) {
			uint totalSize = 4 + (uint)longNames.Sum(s => s.Length);
			writer.Write(totalSize);
			foreach (var s in longNames) {
				writer.Write(Encoding.ASCII.GetBytes(s));
			}
		}
	}

	private class Section {
		public string Name { get; }
		public byte[] Data { get; }

		public Section(byte[] data, string name) {
			Data = data;
			Name = name.Length > 8 ? name.Substring(0, 8) : name;
		}
	}

	private class Symbol {
		public string Name { get; }
		public uint Value { get; }
		public ushort Section { get; }
		public CoffSymbolType Type { get; }
		public CoffSymbolClass StorageClass { get; }
		public byte AuxCount { get; }

		public Symbol(string name, ushort section, uint value, CoffSymbolType type, CoffSymbolClass storageClass) {
			Name = name;
			Value = value;
			Section = section;
			Type = type;
			StorageClass = storageClass;
			AuxCount = 0;
		}
	}
}