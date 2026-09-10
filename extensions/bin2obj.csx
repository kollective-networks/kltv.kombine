#pragma kombine requires 1.6
#pragma kombine module
/*---------------------------------------------------------------------------------------------------------

	Kombine Bin2obj Extension

	(C) Kollective Networks 2026

	Generates object files embedding binary files as data sections, one object per file or one
	object for all of them, so assets can be linked without an intermediate C++ source. The format
	follows the host, COFF on Windows, ELF on Linux, Mach-O 64 bit on macOS, and the machine its
	architecture; Format and Machine set them to anything else. Every public member is documented
	in place; this header is the overview.

	Verbs. Generate(bin, obj) with two lists makes one object per binary; Generate(bin, obj) with one
	output makes a single object holding every binary. Both return true when every output is in
	place and false otherwise, with the reason in LastError (NotFound for a missing binary,
	InvalidArgument for mismatched lists or duplicate names, Failed for a file that could not be
	written) and what was done in LastGenerate: every entry with its source, output, symbol and
	status (UpToDate, Generated, Failed, Skipped). Symbols holds the friendly name and the symbol of
	every input of the last call, generated or not.

	Up to date checks. An output is generated again when it or its record is missing, when what
	generates it changed (the layout version of this extension, the symbol and friendly names, the
	list of inputs of a single output, Machine and the format written) or when an input changed. An
	input whose date and size are those of the record counts as unchanged without being read; one
	whose date or size moved is read and its content hash compared, so a file touched without an
	edit generates nothing and an edit dated older than the output still generates. The one edit
	that passes unseen keeps both the date and the size of the file. The record lives next to the
	output (<output>.kdep). An output is written to a temporary file and moved into place once
	complete, so a failure leaves the previous output as it was. The objects are reproducible: no
	header carries a timestamp, so an object generated again from the same data has the same bytes
	and the archive or the link behind it is not made again.

	Output. Output decides what reaches the console: Silent (nothing), Progress (one progress line
	per call through Progress, the default) or Detailed (one line per file, as the previous version
	printed). AbortOnFailure, true by default, makes a failing call print its reason and abort the
	script instead of returning false.

	The symbol of an input derives from its path exactly as given, so a script should pass its
	binaries through relative paths and keep them stable; a path given differently is a different
	symbol and the output is generated again with it. The size symbol (<symbol>_size) is a 32 bit
	unsigned value in every format: declared as unsigned int, not as unsigned long, which is 8 bytes
	on Linux and macOS.

---------------------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;

if (MkbHexVersion() < 0x0106)
	Msg.PrintAndAbort("The bin2obj extension requires Kombine 1.6 or newer (running " + MkbVersion() + ")");

/// <summary>
/// Generates object files embedding binary files as data sections. See the header of the file for
/// the overview.
/// </summary>
public class Bin2obj {

	/// <summary>
	/// What reaches the console while the files are generated.
	/// </summary>
	public enum OutputMode {
		/// <summary>Nothing at all: the script reads LastGenerate and LastError.</summary>
		Silent,
		/// <summary>One progress line per call through Progress, ended with the result. The default.</summary>
		Progress,
		/// <summary>One line per file ("Bin2obj: Processing x: Generated successfully."), as the previous version printed.</summary>
		Detailed
	}

	/// <summary>
	/// The outcome of one entry of a call.
	/// </summary>
	public enum EntryStatus {
		/// <summary>Nothing to do: the output exists and neither its inputs nor what generates it changed.</summary>
		UpToDate,
		/// <summary>Written.</summary>
		Generated,
		/// <summary>Not written: the reason is in the message of the entry and in LastError.</summary>
		Failed,
		/// <summary>Not attempted because an earlier entry failed with the abort in effect.</summary>
		Skipped
	}

	/// <summary>
	/// One input of a call and what happened to its output.
	/// </summary>
	public class Entry {
		/// <summary>The binary file as given.</summary>
		public string Source { get; internal set; } = string.Empty;
		/// <summary>The output written for it (the single output when one object holds every binary).</summary>
		public string Output { get; internal set; } = string.Empty;
		/// <summary>The symbol of the data; "_size" appended names its length.</summary>
		public string Symbol { get; internal set; } = string.Empty;
		/// <summary>The friendly name, the key of Symbols.</summary>
		public string FriendlyName { get; internal set; } = string.Empty;
		/// <summary>What happened to the output.</summary>
		public EntryStatus Status { get; internal set; } = EntryStatus.UpToDate;
		/// <summary>The reason of a failure, empty otherwise.</summary>
		public string Message { get; internal set; } = string.Empty;
	}

	/// <summary>
	/// What a Generate call did, in LastGenerate.
	/// </summary>
	public class Result {
		/// <summary>Every input of the call, in order.</summary>
		public List<Entry> Entries { get; internal set; } = new List<Entry>();
		/// <summary>Outputs written.</summary>
		public int Generated { get; internal set; } = 0;
		/// <summary>Outputs that were up to date.</summary>
		public int UpToDate { get; internal set; } = 0;
		/// <summary>Outputs that could not be written.</summary>
		public int Failed { get; internal set; } = 0;
		/// <summary>True when the call made one object for every binary.</summary>
		public bool Single { get; internal set; } = false;
	}

	/// <summary>
	/// The format of the objects.
	/// </summary>
	public enum ObjectFormat {
		/// <summary>COFF, the object format of Windows.</summary>
		Coff,
		/// <summary>ELF, the object format of Linux and the other Unix systems.</summary>
		Elf,
		/// <summary>Mach-O 64 bit, the object format of macOS. Takes the x64 and arm64 machines only.</summary>
		MachO
	}

	/// <summary>
	/// The format of the objects: Coff, Elf or MachO. Default: the format of the host (HostFormat),
	/// Coff on Windows, MachO on macOS, Elf elsewhere. A change generates the outputs again.
	/// </summary>
	public ObjectFormat Format { get; set; } = HostFormat();

	/// <summary>
	/// The machine of the objects: "x86", "x64", "arm" or "arm64" (arm64 pads every block to four
	/// bytes). Default: the architecture of the host (HostMachine), empty on a host with another
	/// architecture, which fails the calls until it is set. Mach-O takes x64 and arm64 only. A change
	/// generates the outputs again.
	/// </summary>
	public string Machine { get; set; } = HostMachine();

	/// <summary>
	/// The object format of the host: Coff on Windows, MachO on macOS, Elf elsewhere.
	/// </summary>
	public static ObjectFormat HostFormat() {
		if (Host.IsWindows())
			return ObjectFormat.Coff;
		if (Host.IsMacOS())
			return ObjectFormat.MachO;
		return ObjectFormat.Elf;
	}

	/// <summary>
	/// The machine of the host, from the architecture of the operating system (not of the process,
	/// which may run emulated): "x86", "x64", "arm" or "arm64", empty for any other.
	/// </summary>
	public static string HostMachine() {
		switch (RuntimeInformation.OSArchitecture) {
			case Architecture.X86: return "x86";
			case Architecture.X64: return "x64";
			case Architecture.Arm: return "arm";
			case Architecture.Arm64: return "arm64";
			default: return string.Empty;
		}
	}

	/// <summary>
	/// The symbols of the last call: the friendly name of every input (its path with dots, lower
	/// case) and the symbol of its data, generated or not.
	/// </summary>
	public Dictionary<string, string> Symbols { get; private set; } = new Dictionary<string, string>();

	/// <summary>What reaches the console. Default Progress.</summary>
	public OutputMode Output { get; set; } = OutputMode.Progress;

	/// <summary>The reporter of the progress line: a ProgressBar, ProgressDots or ProgressPlain. Null, the default, takes Progress.Default of the engine.</summary>
	public ITaskProgress? Progress { get; set; } = null;

	/// <summary>True, the default, makes a failing call print its reason and abort the script; false returns false with the reason in LastError.</summary>
	public bool AbortOnFailure { get; set; } = true;

	/// <summary>The start message of the progress line. Empty, the default, uses "Generating N files" or "Generating <output>".</summary>
	public string TaskLabel { get; set; } = string.Empty;

	/// <summary>The reason of the last failure: NotFound, InvalidArgument or Failed, and the message. Reset by every call.</summary>
	public ApiError LastError { get; private set; } = ApiError.None;

	/// <summary>What the last call did. Null before the first call.</summary>
	public Result? LastGenerate { get; private set; } = null;

	/// <summary>
	/// The version of the generated layout, part of what generates every output: bumped when the
	/// object layout changes, so outputs of an older version are generated again once.
	/// </summary>
	private const int FormatVersion = 2;

	// --------------------------------------------------------------------------------------------
	// Verbs
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Generates one object per binary file, for the outputs that are missing or out of date.
	/// </summary>
	/// <param name="bin">The binary files. Every one must exist (NotFound otherwise).</param>
	/// <param name="obj">The objects to write, in the same order and number (InvalidArgument otherwise).</param>
	/// <returns>True when every output is in place; false with the reason in LastError. The detail is in LastGenerate.</returns>
	public bool Generate(KList bin, KList obj) {
		Begin();
		Result result = new Result();
		LastGenerate = result;
		if (bin.Count() != obj.Count())
			return Fail(ErrorCode.InvalidArgument, "the number of binary files must match the number of output files (" + bin.Count() + " and " + obj.Count() + ")", "Generate");
		if (!Prepare(bin, obj, result))
			return false;
		Folders.Create(obj.AsFolders());
		ITaskProgress? progress = StartProgress(TaskLabel.Length > 0 ? TaskLabel : "Generating " + result.Entries.Count + " file" + (result.Entries.Count == 1 ? "" : "s"));
		int done = 0;
		foreach (Entry e in result.Entries) {
			if (result.Failed > 0 && AbortOnFailure) {
				e.Status = EntryStatus.Skipped;
				continue;
			}
			string generator = Hash("bin2obj|" + FormatVersion + "|" + Target() + "|" + e.Symbol + "|" + e.FriendlyName);
			List<string> inputs = new List<string> { e.Source };
			if (UpToDate(e.Output, generator, inputs)) {
				e.Status = EntryStatus.UpToDate;
				result.UpToDate++;
				Line(e.Source, " No changes. Skipping.", null);
			} else {
				try {
					Write(e.Output, (string temp) => GenerateObjectFile(File.ReadAllBytes(e.Source), e.Symbol, temp));
					Record(e.Output, generator, inputs);
					e.Status = EntryStatus.Generated;
					result.Generated++;
					Line(e.Source, " Generated successfully.", null);
				} catch (Exception ex) {
					DeleteRecord(e.Output);
					e.Status = EntryStatus.Failed;
					e.Message = ex.Message;
					result.Failed++;
					Line(e.Source, null, " Failed to generate: " + ex.Message);
				}
			}
			done++;
			progress?.Report((double)done / result.Entries.Count, done + "/" + result.Entries.Count);
		}
		return Finish(progress, result, "Generate");
	}

	/// <summary>
	/// Generates one object holding every binary file, when it is missing or out of date.
	/// </summary>
	/// <param name="bin">The binary files. Every one must exist (NotFound otherwise).</param>
	/// <param name="obj">The object to write.</param>
	/// <returns>True when the output is in place; false with the reason in LastError. The detail is in LastGenerate.</returns>
	public bool Generate(KList bin, KValue obj) {
		Begin();
		Result result = new Result { Single = true };
		LastGenerate = result;
		KList outputs = new KList();
		foreach (KValue b in bin)
			outputs.Add(obj);
		if (!Prepare(bin, outputs, result))
			return false;
		Folders.Create(obj.AsFolder());
		ITaskProgress? progress = StartProgress(TaskLabel.Length > 0 ? TaskLabel : "Generating " + Path.GetFileName(obj));
		// The list of inputs, by name and order, is part of what generates the output
		string generator = Hash("bin2obj|" + FormatVersion + "|" + Target() + "|single|" + string.Join("|", result.Entries.Select(e => e.Symbol + ":" + e.FriendlyName)));
		List<string> inputs = result.Entries.Select(e => e.Source).ToList();
		if (UpToDate(obj, generator, inputs)) {
			foreach (Entry e in result.Entries)
				e.Status = EntryStatus.UpToDate;
			result.UpToDate = 1;
			Line(obj, " No changes. Skipping.", null);
		} else {
			try {
				Write(obj, (string temp) => {
					List<byte[]> datas = new List<byte[]>();
					List<string> names = new List<string>();
					foreach (Entry e in result.Entries) {
						datas.Add(File.ReadAllBytes(e.Source));
						names.Add(e.Symbol);
					}
					GenerateObjectFileMultipleSections(datas, names, temp);
				});
				Record(obj, generator, inputs);
				foreach (Entry e in result.Entries)
					e.Status = EntryStatus.Generated;
				result.Generated = 1;
				Line(obj, " Generated successfully.", null);
			} catch (Exception ex) {
				DeleteRecord(obj);
				foreach (Entry e in result.Entries) {
					e.Status = EntryStatus.Failed;
					e.Message = ex.Message;
				}
				result.Failed = 1;
				Line(obj, null, " Failed to generate: " + ex.Message);
			}
		}
		progress?.Report(1, null);
		return Finish(progress, result, "Generate");
	}

	// --------------------------------------------------------------------------------------------
	// The call
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// The symbol of a binary: "var", its file name with dots and dashes as underscores, and a hash
	/// of its path as given, for uniqueness. Unchanged from the previous version.
	/// </summary>
	private static string GetVarName(KValue filePath) {
		string varname = "var" + Path.GetFileName(filePath).Replace('.', '_').Replace('-', '_');
		string objectname = filePath.GetHashCode64().ToString();
		return varname + objectname;
	}

	/// <summary>
	/// The friendly name of a binary: its path with slashes as dots, lower case.
	/// </summary>
	private static string GetFriendlyName(string filePath) {
		return filePath.Replace('/', '.').Replace('\\', '.').ToLower();
	}

	/// <summary>
	/// What the format of the outputs depends on: the object format and the machine. Part of what
	/// generates every output.
	/// </summary>
	private string Target() {
		return Format.ToString().ToLowerInvariant() + "|" + Machine;
	}

	/// <summary>
	/// The reason Format and Machine cannot be written, null when they can: an empty machine (a host
	/// with another architecture), an unknown one, or one the format does not take.
	/// </summary>
	private string? TargetError() {
		if (string.IsNullOrEmpty(Machine))
			return "no machine: the host architecture is " + RuntimeInformation.OSArchitecture + ", set Machine (x86, x64, arm or arm64)";
		if (!MachineTypes.ContainsKey(Machine))
			return "unknown machine: " + Machine + " (x86, x64, arm or arm64)";
		if (Format == ObjectFormat.MachO && Machine != "x64" && Machine != "arm64")
			return "Mach-O objects take the x64 or arm64 machines, not " + Machine;
		return null;
	}

	/// <summary>
	/// Builds the entries of a call and runs the checks that come before any write: every binary
	/// exists, no friendly name or symbol repeats, the format and the machine are valid. Fills Symbols.
	/// </summary>
	private bool Prepare(KList bin, KList outputs, Result result) {
		string? target = TargetError();
		if (target != null)
			return Fail(ErrorCode.InvalidArgument, target, "Generate");
		HashSet<string> friendly = new HashSet<string>();
		HashSet<string> symbols = new HashSet<string>();
		for (int i = 0; i < bin.Count(); i++) {
			Entry e = new Entry { Source = bin[i], Output = outputs[i], Symbol = GetVarName(bin[i]), FriendlyName = GetFriendlyName(bin[i]) };
			result.Entries.Add(e);
			if (!File.Exists(e.Source))
				return Fail(ErrorCode.NotFound, "binary file not found: " + e.Source, "Generate");
			if (!friendly.Add(e.FriendlyName))
				return Fail(ErrorCode.InvalidArgument, "two inputs give the same name: " + e.FriendlyName, "Generate");
			if (!symbols.Add(e.Symbol))
				return Fail(ErrorCode.InvalidArgument, "two inputs give the same symbol: " + e.Symbol, "Generate");
		}
		foreach (Entry e in result.Entries)
			Symbols[e.FriendlyName] = e.Symbol;
		return true;
	}

	/// <summary>
	/// Writes an output through a temporary file next to it, moved into place once complete, so a
	/// failure leaves the previous output as it was and never a partial file.
	/// </summary>
	private static void Write(string output, Action<string> writeTo) {
		string temp = output + ".tmp";
		try {
			writeTo(temp);
			File.Move(temp, output, true);
		} finally {
			if (File.Exists(temp)) {
				try {
					File.Delete(temp);
				} catch {
				}
			}
		}
	}

	/// <summary>
	/// Opens the progress line in the Progress mode.
	/// </summary>
	private ITaskProgress? StartProgress(string label) {
		if (Output != OutputMode.Progress)
			return null;
		ITaskProgress progress = Progress ?? Kltv.Kombine.Api.Progress.Default;
		progress.Start(label);
		return progress;
	}

	/// <summary>
	/// The line of one file in the Detailed mode, as the previous version printed it.
	/// </summary>
	private void Line(string file, string? success, string? error) {
		// Always emitted: at normal level in the Detailed mode, at verbose level otherwise, where it
		// reaches a Msg.OnMessage handler without reaching the console
		Msg.LogLevels level = Output == OutputMode.Detailed ? Msg.LogLevels.Normal : Msg.LogLevels.Verbose;
		Msg.PrintTask("Bin2obj: Processing " + file + ":", level);
		if (error != null)
			Msg.PrintTaskError(error, level);
		else
			Msg.PrintTaskSuccess(success ?? string.Empty, level);
	}

	/// <summary>
	/// Closes the progress line with the result and records the failure of the call, if any.
	/// </summary>
	private bool Finish(ITaskProgress? progress, Result result, string source) {
		string text = result.Failed > 0 ? "failed (" + result.Failed + ")" : (result.Generated == 0 ? "ok (up to date)" : (result.UpToDate > 0 ? "ok (" + result.UpToDate + " up to date)" : "ok"));
		progress?.Finish(text, result.Failed > 0 ? ProgressOutcome.Error : ProgressOutcome.Success);
		if (result.Failed > 0) {
			Entry first = result.Entries.First(e => e.Status == EntryStatus.Failed);
			return Fail(ErrorCode.Failed, first.Output + ": " + first.Message + (result.Failed > 1 ? " (" + result.Failed + " failed)" : ""), source);
		}
		return true;
	}

	/// <summary>
	/// Starts a call: the last failure and the symbols are reset.
	/// </summary>
	private void Begin() {
		LastError = ApiError.None;
		Symbols.Clear();
		hashes.Clear();
	}

	/// <summary>
	/// Records a failure: LastError is set, the reason logged at verbose level, and the script
	/// aborted when AbortOnFailure is set. Always returns false.
	/// </summary>
	private bool Fail(ErrorCode code, string message, string source) {
		LastError = new ApiError(code, message, source);
		Msg.PrintWarning("bin2obj: " + LastError.ToString(), Msg.LogLevels.Verbose);
		if (AbortOnFailure)
			Msg.PrintAndAbort("bin2obj " + source + " failed (" + code + "): " + message);
		return false;
	}

	// --------------------------------------------------------------------------------------------
	// Up to date checks
	// --------------------------------------------------------------------------------------------

	/// <summary>The record of an output: what it was generated from, next to it.</summary>
	private static string RecordFile(string output) {
		return output + ".kdep";
	}

	/// <summary>
	/// True when the output exists, its record exists, what generates it is the same and every
	/// recorded input exists with the same content hash. Dates are never compared.
	/// </summary>
	private bool UpToDate(string output, string generator, List<string> inputs) {
		if (!File.Exists(output))
			return false;
		string recordFile = RecordFile(output);
		if (!File.Exists(recordFile))
			return false;
		JsonObject? record;
		try {
			record = JsonNode.Parse(File.ReadAllText(recordFile)) as JsonObject;
		} catch {
			record = null;
		}
		if (record == null || record["inputs"] is not JsonArray recorded)
			return false;
		if ((record["generator"]?.ToString() ?? string.Empty) != generator)
			return false;
		HashSet<string> current = new HashSet<string>(inputs.Select(i => Path.GetFullPath(i)), StringComparer.OrdinalIgnoreCase);
		int seen = 0;
		bool refresh = false;
		foreach (JsonNode? n in recorded) {
			if (n is not JsonObject entry)
				return false;
			string path = entry["path"]?.ToString() ?? string.Empty;
			if (path.Length == 0 || !current.Contains(path))
				return false;
			FileInfo fi = new FileInfo(path);
			if (!fi.Exists)
				return false;
			seen++;
			// Same date and size: unchanged without reading it; otherwise the content decides
			if ((long?)entry["date"] == fi.LastWriteTimeUtc.Ticks && (long?)entry["size"] == fi.Length)
				continue;
			if (HashFile(path) != (entry["hash"]?.ToString() ?? string.Empty))
				return false;
			entry["date"] = fi.LastWriteTimeUtc.Ticks;
			entry["size"] = fi.Length;
			refresh = true;
		}
		if (seen != current.Count)
			return false;
		if (refresh) {
			// The moved date is recorded so the next check does not read the file again
			try {
				File.WriteAllText(recordFile, record.ToJsonString());
			} catch {
			}
		}
		return true;
	}

	/// <summary>
	/// Writes the record of an output: the hash of what generates it and, for every input, its
	/// path, content hash, date and size.
	/// </summary>
	private void Record(string output, string generator, List<string> inputs) {
		JsonObject record = new JsonObject();
		record["generator"] = generator;
		JsonArray list = new JsonArray();
		foreach (string i in inputs.Select(i => Path.GetFullPath(i)).Distinct(StringComparer.OrdinalIgnoreCase)) {
			FileInfo fi = new FileInfo(i);
			JsonObject entry = new JsonObject();
			entry["path"] = i;
			entry["date"] = fi.LastWriteTimeUtc.Ticks;
			entry["size"] = fi.Length;
			entry["hash"] = HashFile(i, true);
			list.Add(entry);
		}
		record["inputs"] = list;
		File.WriteAllText(RecordFile(output), record.ToJsonString());
	}

	/// <summary>Removes the record of an output that failed, so the next call generates it again.</summary>
	private static void DeleteRecord(string output) {
		try {
			if (File.Exists(RecordFile(output)))
				File.Delete(RecordFile(output));
		} catch {
		}
	}

	/// <summary>The content hashes read during the current call.</summary>
	private readonly Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>The content hash of a file, from the cache of the current call unless fresh.</summary>
	private string HashFile(string path, bool fresh = false) {
		string key = Path.GetFullPath(path);
		if (!fresh && hashes.TryGetValue(key, out string? known))
			return known;
		string hash;
		using (FileStream s = File.OpenRead(path)) {
			hash = Convert.ToHexString(SHA256.HashData(s));
		}
		hashes[key] = hash;
		return hash;
	}

	/// <summary>The hash of a text.</summary>
	private static string Hash(string text) {
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
	}

	// --------------------------------------------------------------------------------------------
	// The object files
	// --------------------------------------------------------------------------------------------

	/// <summary>The machine field of a COFF header.</summary>
	public enum CoffMachine : ushort {
		I386 = 0x014c,
		Amd64 = 0x8664,
		Arm = 0x01c0,
		Arm64 = 0xAA64
	}

	/// <summary>The characteristics field of a COFF header.</summary>
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

	/// <summary>The characteristics field of a COFF section.</summary>
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

	/// <summary>The type field of a COFF symbol.</summary>
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

	/// <summary>The storage class field of a COFF symbol.</summary>
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

	private const uint MH_MAGIC_64 = 0xFEEDFACF;
	private const uint MH_OBJECT = 0x1;
	private const int CPU_TYPE_X86_64 = 0x01000007;
	private const int CPU_TYPE_ARM64 = 0x0100000C;
	private const int CPU_SUBTYPE_X86_64_ALL = 3;
	private const int CPU_SUBTYPE_ARM64_ALL = 0;
	private const uint LC_SEGMENT_64 = 0x19;
	private const uint LC_SYMTAB = 0x02;
	private const byte SYM_EXT_SECT = 0x0F;

	/// <summary>
	/// A symbol of an object: its name, its offset in the data section, the length of its block and
	/// whether it names the size of a block (a 32 bit unsigned value) or the block itself.
	/// </summary>
	private class ObjectSymbol {
		public string Name { get; }
		public uint Offset { get; }
		public uint Size { get; }
		public bool IsSize { get; }
		public ObjectSymbol(string name, uint offset, uint size, bool isSize) {
			Name = name;
			Offset = offset;
			Size = size;
			IsSize = isSize;
		}
	}

	/// <summary>
	/// Lays out the data section: every binary followed by its size as a 32 bit unsigned value,
	/// padded to four bytes on arm64, with a symbol for the block and one for its size. The same
	/// layout in every format.
	/// </summary>
	private byte[] Layout(List<byte[]> datas, List<string> names, out List<ObjectSymbol> symbols) {
		List<byte> section = new List<byte>();
		symbols = new List<ObjectSymbol>();
		uint offset = 0;
		for (int i = 0; i < datas.Count; i++) {
			byte[] data = datas[i];
			int paddingLen = (Machine == "arm64") ? (4 - data.Length % 4) % 4 : 0;
			uint sizeOffset = offset + (uint)data.Length + (uint)paddingLen;
			symbols.Add(new ObjectSymbol(names[i], offset, (uint)data.Length, false));
			symbols.Add(new ObjectSymbol(names[i] + "_size", sizeOffset, 4, true));
			section.AddRange(data);
			section.AddRange(new byte[paddingLen]);
			section.AddRange(BitConverter.GetBytes((uint)data.Length));
			offset = sizeOffset + 4;
		}
		return section.ToArray();
	}

	/// <summary>
	/// Writes the object of one binary: a data section with the bytes, padding on arm64, and the
	/// size, with a symbol for each.
	/// </summary>
	private void GenerateObjectFile(byte[] data, string symbolName, string outputFile) {
		GenerateObjectFileMultipleSections(new List<byte[]> { data }, new List<string> { symbolName }, outputFile);
	}

	/// <summary>
	/// Writes the object holding every binary: one data section with every one after the other,
	/// each followed by its size, with the symbols at their offsets, in the format selected.
	/// </summary>
	private void GenerateObjectFileMultipleSections(List<byte[]> datas, List<string> names, string outputFile) {
		byte[] section = Layout(datas, names, out List<ObjectSymbol> symbols);
		switch (Format) {
			case ObjectFormat.Elf: {
				WriteElfFile(section, symbols, outputFile);
				break;
			}
			case ObjectFormat.MachO: {
				WriteMachOFile(section, symbols.Select(s => (name: s.Name, offset: s.Offset)).ToList(), outputFile);
				break;
			}
			default: {
				List<Section> sections = new() { new Section(section, ".data") };
				List<Symbol> coffSymbols = symbols.Select(s => new Symbol(s.Name, 1, s.Offset, s.IsSize ? CoffSymbolType.Uint : CoffSymbolType.Null, CoffSymbolClass.External)).ToList();
				WriteCoffFile(sections, coffSymbols, outputFile);
				break;
			}
		}
	}

	/// <summary>
	/// Writes a COFF object with the sections and symbols given. The timestamp of the header is
	/// zero, so the same data gives the same bytes.
	/// </summary>
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
		writer.Write((uint)0); // TimeDateStamp: zero, the object is reproducible
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

	/// <summary>
	/// Writes a Mach-O 64 bit object with one data section and the symbols given, for the machine
	/// selected (x64 or arm64).
	/// </summary>
	private void WriteMachOFile(byte[] sectionData, List<(string name, uint offset)> symbols, string outputFile) {
		using FileStream fs = new(outputFile, FileMode.Create);
		using BinaryWriter writer = new(fs);

		bool arm64 = Machine == "arm64";
		int cputype = arm64 ? CPU_TYPE_ARM64 : CPU_TYPE_X86_64;
		int cpusubtype = arm64 ? CPU_SUBTYPE_ARM64_ALL : CPU_SUBTYPE_X86_64_ALL;

		var macSymbols = symbols.Select(s => ("_" + s.name, s.offset)).ToList();

		List<byte> stringTable = new List<byte> { 0 };
		var symStrOffsets = new List<uint>();
		foreach (var (symName, _) in macSymbols) {
			symStrOffsets.Add((uint)stringTable.Count);
			stringTable.AddRange(Encoding.ASCII.GetBytes(symName));
			stringTable.Add(0);
		}
		while (stringTable.Count % 4 != 0)
			stringTable.Add(0);

		int headerSize = 32;
		int segmentCmdSize = 72 + 80;
		int symtabCmdSize = 24;
		int sizeofcmds = segmentCmdSize + symtabCmdSize;
		int dataOffset = headerSize + sizeofcmds;
		int symtabOffset = dataOffset + sectionData.Length;
		int symtabAlign = (8 - (symtabOffset % 8)) % 8;
		symtabOffset += symtabAlign;
		int symCount = macSymbols.Count;
		int strOffset = symtabOffset + symCount * 16;

		writer.Write(MH_MAGIC_64);
		writer.Write(cputype);
		writer.Write(cpusubtype);
		writer.Write((uint)MH_OBJECT);
		writer.Write((uint)2);
		writer.Write((uint)sizeofcmds);
		writer.Write((uint)0);
		writer.Write((uint)0);

		writer.Write(LC_SEGMENT_64);
		writer.Write((uint)segmentCmdSize);
		WriteFixedString(writer, "__DATA", 16);
		writer.Write((ulong)0);
		writer.Write((ulong)sectionData.Length);
		writer.Write((ulong)dataOffset);
		writer.Write((ulong)sectionData.Length);
		writer.Write((int)7);
		writer.Write((int)3);
		writer.Write((uint)1);
		writer.Write((uint)0);

		WriteFixedString(writer, "__data", 16);
		WriteFixedString(writer, "__DATA", 16);
		writer.Write((ulong)0);
		writer.Write((ulong)sectionData.Length);
		writer.Write((uint)dataOffset);
		writer.Write((uint)2);
		writer.Write((uint)0);
		writer.Write((uint)0);
		writer.Write((uint)0);
		writer.Write((uint)0);
		writer.Write((uint)0);
		writer.Write((uint)0);

		writer.Write(LC_SYMTAB);
		writer.Write((uint)24);
		writer.Write((uint)symtabOffset);
		writer.Write((uint)symCount);
		writer.Write((uint)strOffset);
		writer.Write((uint)stringTable.Count);

		writer.Write(sectionData);

		writer.Write(new byte[symtabAlign]);

		for (int i = 0; i < macSymbols.Count; i++) {
			var (_, symOffset) = macSymbols[i];
			writer.Write(symStrOffsets[i]);
			writer.Write(SYM_EXT_SECT);
			writer.Write((byte)1);
			writer.Write((ushort)0);
			writer.Write((ulong)symOffset);
		}

		writer.Write(stringTable.ToArray());
	}

	private static void WriteFixedString(BinaryWriter w, string s, int len) {
		byte[] buf = new byte[len];
		Encoding.ASCII.GetBytes(s, 0, Math.Min(s.Length, len), buf, 0);
		w.Write(buf);
	}

	// --------------------------------------------------------------------------------------------
	// ELF
	// --------------------------------------------------------------------------------------------

	/// <summary>The machine field of an ELF header, by machine name.</summary>
	private static readonly Dictionary<string, ushort> ElfMachines = new() {
		["x86"] = 3,      // EM_386
		["x64"] = 62,     // EM_X86_64
		["arm"] = 40,     // EM_ARM
		["arm64"] = 183   // EM_AARCH64
	};

	private const ushort ET_REL = 1;
	private const uint SHT_PROGBITS = 1;
	private const uint SHT_SYMTAB = 2;
	private const uint SHT_STRTAB = 3;
	private const uint SHF_ALLOC = 0x2;
	private const uint EF_ARM_EABI_VER5 = 0x05000000;
	private const byte STB_GLOBAL_STT_OBJECT = 0x11;

	/// <summary>
	/// Writes an ELF relocatable object, 64 bit for x64 and arm64 and 32 bit for x86 and arm, little
	/// endian, with one read only data section (.rodata), the symbols given as global objects with
	/// their sizes, and no relocations. Nothing in it depends on the time, so the same data gives
	/// the same bytes.
	/// </summary>
	private void WriteElfFile(byte[] sectionData, List<ObjectSymbol> symbols, string outputFile) {
		bool is64 = Machine == "x64" || Machine == "arm64";
		int ehsize = is64 ? 64 : 52;
		int shentsize = is64 ? 64 : 40;
		int symentsize = is64 ? 24 : 16;
		// The string tables: the symbol names and the section names
		List<byte> strtab = new List<byte> { 0 };
		List<uint> nameOffsets = new List<uint>();
		foreach (ObjectSymbol s in symbols) {
			nameOffsets.Add((uint)strtab.Count);
			strtab.AddRange(Encoding.ASCII.GetBytes(s.Name));
			strtab.Add(0);
		}
		string[] sectionNames = { ".rodata", ".symtab", ".strtab", ".shstrtab" };
		List<byte> shstrtab = new List<byte> { 0 };
		uint[] sectionNameOffsets = new uint[sectionNames.Length];
		for (int i = 0; i < sectionNames.Length; i++) {
			sectionNameOffsets[i] = (uint)shstrtab.Count;
			shstrtab.AddRange(Encoding.ASCII.GetBytes(sectionNames[i]));
			shstrtab.Add(0);
		}
		// The layout: header, data, symbol table (aligned), string tables, section headers (aligned)
		ulong dataOffset = (ulong)ehsize;
		ulong symtabOffset = Align(dataOffset + (ulong)sectionData.Length, 8);
		ulong symtabSize = (ulong)((symbols.Count + 1) * symentsize);
		ulong strtabOffset = symtabOffset + symtabSize;
		ulong shstrtabOffset = strtabOffset + (ulong)strtab.Count;
		ulong shoff = Align(shstrtabOffset + (ulong)shstrtab.Count, 8);

		using FileStream fs = new(outputFile, FileMode.Create);
		using BinaryWriter writer = new(fs);
		// The ELF header
		writer.Write(new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', (byte)(is64 ? 2 : 1), 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
		writer.Write(ET_REL);
		writer.Write(ElfMachines[Machine]);
		writer.Write((uint)1);                                    // e_version
		WriteWord(writer, 0, is64);                               // e_entry
		WriteWord(writer, 0, is64);                               // e_phoff
		WriteWord(writer, shoff, is64);                           // e_shoff
		writer.Write(Machine == "arm" ? EF_ARM_EABI_VER5 : 0u);   // e_flags
		writer.Write((ushort)ehsize);
		writer.Write((ushort)0);                                  // e_phentsize
		writer.Write((ushort)0);                                  // e_phnum
		writer.Write((ushort)shentsize);
		writer.Write((ushort)5);                                  // e_shnum
		writer.Write((ushort)4);                                  // e_shstrndx
		// The data
		writer.Write(sectionData);
		Pad(writer, symtabOffset);
		// The symbol table: the null symbol, then every symbol as a global object of the data section
		writer.Write(new byte[symentsize]);
		for (int i = 0; i < symbols.Count; i++) {
			ObjectSymbol s = symbols[i];
			if (is64) {
				writer.Write(nameOffsets[i]);                     // st_name
				writer.Write(STB_GLOBAL_STT_OBJECT);              // st_info
				writer.Write((byte)0);                            // st_other
				writer.Write((ushort)1);                          // st_shndx
				writer.Write((ulong)s.Offset);                    // st_value
				writer.Write((ulong)s.Size);                      // st_size
			} else {
				writer.Write(nameOffsets[i]);                     // st_name
				writer.Write(s.Offset);                           // st_value
				writer.Write(s.Size);                             // st_size
				writer.Write(STB_GLOBAL_STT_OBJECT);              // st_info
				writer.Write((byte)0);                            // st_other
				writer.Write((ushort)1);                          // st_shndx
			}
		}
		writer.Write(strtab.ToArray());
		writer.Write(shstrtab.ToArray());
		Pad(writer, shoff);
		// The section headers: null, .rodata, .symtab, .strtab, .shstrtab
		writer.Write(new byte[shentsize]);
		WriteSectionHeader(writer, is64, sectionNameOffsets[0], SHT_PROGBITS, SHF_ALLOC, dataOffset, (ulong)sectionData.Length, 0, 0, 16, 0);
		WriteSectionHeader(writer, is64, sectionNameOffsets[1], SHT_SYMTAB, 0, symtabOffset, symtabSize, 3, 1, (ulong)(is64 ? 8 : 4), (ulong)symentsize);
		WriteSectionHeader(writer, is64, sectionNameOffsets[2], SHT_STRTAB, 0, strtabOffset, (ulong)strtab.Count, 0, 0, 1, 0);
		WriteSectionHeader(writer, is64, sectionNameOffsets[3], SHT_STRTAB, 0, shstrtabOffset, (ulong)shstrtab.Count, 0, 0, 1, 0);
	}

	/// <summary>One ELF section header, 64 or 32 bit.</summary>
	private static void WriteSectionHeader(BinaryWriter w, bool is64, uint name, uint type, ulong flags, ulong offset, ulong size, uint link, uint info, ulong align, ulong entsize) {
		w.Write(name);
		w.Write(type);
		WriteWord(w, flags, is64);
		WriteWord(w, 0, is64);                                    // sh_addr
		WriteWord(w, offset, is64);
		WriteWord(w, size, is64);
		w.Write(link);
		w.Write(info);
		WriteWord(w, align, is64);
		WriteWord(w, entsize, is64);
	}

	/// <summary>A word of the ELF class: 8 bytes in a 64 bit object, 4 in a 32 bit one.</summary>
	private static void WriteWord(BinaryWriter w, ulong value, bool is64) {
		if (is64)
			w.Write(value);
		else
			w.Write((uint)value);
	}

	/// <summary>Zero bytes up to the offset.</summary>
	private static void Pad(BinaryWriter w, ulong offset) {
		long missing = (long)offset - w.BaseStream.Position;
		if (missing > 0)
			w.Write(new byte[missing]);
	}

	/// <summary>The value rounded up to the alignment.</summary>
	private static ulong Align(ulong value, ulong alignment) {
		return (value + alignment - 1) / alignment * alignment;
	}
}
