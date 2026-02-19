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


	private static readonly Dictionary<string, ushort> MachineTypes = new() {
		["x86"] = 0x014c, // IMAGE_FILE_MACHINE_I386
		["x64"] = 0x8664, // IMAGE_FILE_MACHINE_AMD64
		["arm"] = 0x01c0, // IMAGE_FILE_MACHINE_ARM
		["arm64"] = 0xAA64, // IMAGE_FILE_MACHINE_ARM64
	};

	private void GenerateCoffFile(byte[] data, string symbolName, string outputFile) {
		List<Section> sections = new() { new Section(data, symbolName) };
		List<Symbol> symbols = new() { new Symbol(symbolName, 1, 0, 0, 3) }; // Section 1, value 0, type 0, class 3 (static)
		WriteCoffFile(sections, symbols, outputFile);
	}

	private void GenerateCoffFileMultipleSections(List<byte[]> datas, List<string> names, string outputFile) {
		List<Section> sections = new();
		List<Symbol> symbols = new();
		for (int i = 0; i < datas.Count; i++) {
			sections.Add(new Section(datas[i], names[i]));
			symbols.Add(new Symbol(names[i], (ushort)(i + 1), 0, 0, 3));
		}
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
			writer.Write(0x40000040u); // Characteristics (IMAGE_SCN_CNT_INITIALIZED_DATA | IMAGE_SCN_MEM_READ)
			dataOffset += (uint)section.Data.Length;
		}

		// Section data
		foreach (var section in sections) {
			writer.Write(section.Data);
		}

		// Symbol table
		foreach (var symbol in symbols) {
			writer.Write(symbol.NameBytes); // Name (8 bytes)
			writer.Write(symbol.Value); // Value
			writer.Write(symbol.Section); // Section
			writer.Write(symbol.Type); // Type
			writer.Write(symbol.StorageClass); // StorageClass
			writer.Write(symbol.AuxCount); // NumberOfAuxSymbols
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
		public byte[] NameBytes { get; }
		public uint Value { get; }
		public ushort Section { get; }
		public ushort Type { get; }
		public byte StorageClass { get; }
		public byte AuxCount { get; }

		public Symbol(string name, ushort section, uint value, ushort type, byte storageClass) {
			NameBytes = System.Text.Encoding.ASCII.GetBytes(name.PadRight(8, '\0').Substring(0, 8));
			Value = value;
			Section = section;
			Type = type;
			StorageClass = storageClass;
			AuxCount = 0;
		}
	}
}