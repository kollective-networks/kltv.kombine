#pragma kombine requires 1.6
/*---------------------------------------------------------------------------------------------------------

	Kombine Bin2cpp Extension

	(C) Kollective Networks 2026

	Generates C++ sources embedding binary files as byte arrays, one source per file or one source
	for all of them, so assets can be compiled into a C++ project. Every public member is documented
	in place; this header is the overview.

	Verbs. Generate(bin, cpp) with two lists makes one source per binary; Generate(bin, cpp) with one
	output makes a single source holding every binary. Both return true when every output is in
	place and false otherwise, with the reason in LastError (NotFound for a missing binary,
	InvalidArgument for mismatched lists or duplicate names, Failed for a file that could not be
	written) and what was done in LastGenerate: every entry with its source, output, symbol and
	status (UpToDate, Generated, Failed, Skipped). Symbols holds the friendly name and the symbol of
	every input of the last call, generated or not, for a script that writes a header declaring them.

	Up to date checks. An output is generated again when it or its record is missing, when what
	generates it changed (the layout version of this extension, the symbol and friendly names, the
	list of inputs of a single output) or when an input changed. An input whose date and size are
	those of the record counts as unchanged without being read; one whose date or size moved is read
	and its content hash compared, so a file touched without an edit generates nothing and an edit
	dated older than the output still generates. The one edit that passes unseen keeps both the date
	and the size of the file. The record lives next to the output (<output>.kdep). An output is
	written to a temporary file and moved into place once complete, so a failure leaves the previous
	output as it was.

	Output. Output decides what reaches the console: Silent (nothing), Progress (one progress line
	per call through Progress, the default) or Detailed (one line per file, as the previous version
	printed). AbortOnFailure, true by default, makes a failing call print its reason and abort the
	script instead of returning false.

	The symbol of an input derives from its path exactly as given, so a script should pass its
	binaries through relative paths and keep them stable; a path given differently is a different
	symbol and the output is generated again with it.

---------------------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;

if (MkbHexVersion() < 0x0106)
	Msg.PrintAndAbort("The bin2cpp extension requires Kombine 1.6 or newer (running " + MkbVersion() + ")");

/// <summary>
/// Generates C++ sources embedding binary files as byte arrays. See the header of the file for the
/// overview.
/// </summary>
public class Bin2cpp {

	/// <summary>
	/// What reaches the console while the files are generated.
	/// </summary>
	public enum OutputMode {
		/// <summary>Nothing at all: the script reads LastGenerate and LastError.</summary>
		Silent,
		/// <summary>One progress line per call through Progress, ended with the result. The default.</summary>
		Progress,
		/// <summary>One line per file ("Bin2cpp: Processing x: Generated successfully."), as the previous version printed.</summary>
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
		/// <summary>The output written for it (the single output when one source holds every binary).</summary>
		public string Output { get; internal set; } = string.Empty;
		/// <summary>The symbol of the byte array; "_size" appended names its length.</summary>
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
		/// <summary>True when the call made one source for every binary.</summary>
		public bool Single { get; internal set; } = false;
	}

	/// <summary>
	/// The symbols of the last call: the friendly name of every input (its path with dots, lower
	/// case) and the symbol of its byte array, generated or not, so a script can write a header
	/// declaring them.
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
	/// generated text changes, so outputs of an older version are generated again once.
	/// </summary>
	private const int FormatVersion = 2;

	// --------------------------------------------------------------------------------------------
	// Verbs
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Generates one C++ source per binary file, for the outputs that are missing or out of date.
	/// </summary>
	/// <param name="bin">The binary files. Every one must exist (NotFound otherwise).</param>
	/// <param name="cpp">The sources to write, in the same order and number (InvalidArgument otherwise).</param>
	/// <returns>True when every output is in place; false with the reason in LastError. The detail is in LastGenerate.</returns>
	public bool Generate(KList bin, KList cpp) {
		Begin();
		Result result = new Result();
		LastGenerate = result;
		if (bin.Count() != cpp.Count())
			return Fail(ErrorCode.InvalidArgument, "the number of binary files must match the number of output files (" + bin.Count() + " and " + cpp.Count() + ")", "Generate");
		if (!Prepare(bin, cpp, result))
			return false;
		Folders.Create(cpp.AsFolders());
		ITaskProgress? progress = StartProgress(TaskLabel.Length > 0 ? TaskLabel : "Generating " + result.Entries.Count + " file" + (result.Entries.Count == 1 ? "" : "s"));
		int done = 0;
		foreach (Entry e in result.Entries) {
			if (result.Failed > 0 && AbortOnFailure) {
				e.Status = EntryStatus.Skipped;
				continue;
			}
			string generator = Hash("bin2cpp|" + FormatVersion + "|" + e.Symbol + "|" + e.FriendlyName);
			List<string> inputs = new List<string> { e.Source };
			if (UpToDate(e.Output, generator, inputs)) {
				e.Status = EntryStatus.UpToDate;
				result.UpToDate++;
				Line(e.Source, " No changes. Skipping.", null);
			} else {
				try {
					Write(e.Output, (StreamWriter w) => WriteOne(w, e));
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
	/// Generates one C++ source holding every binary file, when it is missing or out of date.
	/// </summary>
	/// <param name="bin">The binary files. Every one must exist (NotFound otherwise).</param>
	/// <param name="cpp">The source to write.</param>
	/// <returns>True when the output is in place; false with the reason in LastError. The detail is in LastGenerate.</returns>
	public bool Generate(KList bin, KValue cpp) {
		Begin();
		Result result = new Result { Single = true };
		LastGenerate = result;
		KList outputs = new KList();
		foreach (KValue b in bin)
			outputs.Add(cpp);
		if (!Prepare(bin, outputs, result))
			return false;
		Folders.Create(cpp.AsFolder());
		ITaskProgress? progress = StartProgress(TaskLabel.Length > 0 ? TaskLabel : "Generating " + Path.GetFileName(cpp));
		// The list of inputs, by name and order, is part of what generates the output
		string generator = Hash("bin2cpp|" + FormatVersion + "|single|" + string.Join("|", result.Entries.Select(e => e.Symbol + ":" + e.FriendlyName)));
		List<string> inputs = result.Entries.Select(e => e.Source).ToList();
		if (UpToDate(cpp, generator, inputs)) {
			foreach (Entry e in result.Entries)
				e.Status = EntryStatus.UpToDate;
			result.UpToDate = 1;
			Line(cpp, " No changes. Skipping.", null);
		} else {
			try {
				Write(cpp, (StreamWriter w) => WriteAll(w, result.Entries));
				Record(cpp, generator, inputs);
				foreach (Entry e in result.Entries)
					e.Status = EntryStatus.Generated;
				result.Generated = 1;
				Line(cpp, " Generated successfully.", null);
			} catch (Exception ex) {
				DeleteRecord(cpp);
				foreach (Entry e in result.Entries) {
					e.Status = EntryStatus.Failed;
					e.Message = ex.Message;
				}
				result.Failed = 1;
				Line(cpp, null, " Failed to generate: " + ex.Message);
			}
		}
		progress?.Report(1, null);
		return Finish(progress, result, "Generate");
	}

	// --------------------------------------------------------------------------------------------
	// The generated text
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Writes the source of one binary: the declarations, the byte array and its size.
	/// </summary>
	private static void WriteOne(StreamWriter writer, Entry e) {
		byte[] data = File.ReadAllBytes(e.Source);
		writer.WriteLine("// This file is generated by Kombine Bin2cpp extension. Do not edit manually.");
		writer.WriteLine($"// Source binary file: {e.Source}");
		writer.WriteLine($"// Friendly name: {e.FriendlyName}");
		writer.WriteLine();
		writer.WriteLine();
		writer.WriteLine($"extern \"C\" const unsigned char {e.Symbol}[];");
		writer.WriteLine($"extern \"C\" const unsigned long {e.Symbol}_size;");
		writer.WriteLine($"const unsigned char {e.Symbol}[] = {{");
		WriteBytes(writer, data);
		writer.WriteLine("};");
		writer.WriteLine();
		writer.WriteLine($"const unsigned long {e.Symbol}_size = {data.Length};");
	}

	/// <summary>
	/// Writes the source holding every binary.
	/// </summary>
	private static void WriteAll(StreamWriter writer, List<Entry> entries) {
		writer.WriteLine("// This file is generated by Bin2cpp extension. Do not edit manually.");
		writer.WriteLine("// Source binary files:");
		foreach (Entry e in entries)
			writer.WriteLine($"//   {e.Source}");
		writer.WriteLine();
		writer.WriteLine();
		foreach (Entry e in entries) {
			byte[] data = File.ReadAllBytes(e.Source);
			writer.WriteLine($"extern \"C\" const unsigned char {e.Symbol}[];");
			writer.WriteLine($"const unsigned char {e.Symbol}[] = {{");
			WriteBytes(writer, data);
			writer.WriteLine("};");
			writer.WriteLine();
			writer.WriteLine($"extern \"C\" const unsigned long {e.Symbol}_size;");
			writer.WriteLine($"const unsigned long {e.Symbol}_size = {data.Length};");
			writer.WriteLine();
		}
	}

	/// <summary>The text of every byte value, "0x00" to "0xFF", so the writer formats nothing per byte.</summary>
	private static readonly string[] HexBytes = Enumerable.Range(0, 256).Select(b => "0x" + b.ToString("X2")).ToArray();

	/// <summary>
	/// The bytes of an array, sixteen per line, written line by line: the same text as before, at
	/// the speed a large asset needs.
	/// </summary>
	private static void WriteBytes(StreamWriter writer, byte[] data) {
		StringBuilder line = new StringBuilder(16 * 6);
		for (int j = 0; j < data.Length; j++) {
			line.Append(HexBytes[data[j]]);
			if (j < data.Length - 1)
				line.Append(", ");
			if ((j + 1) % 16 == 0) {
				writer.WriteLine(line);
				line.Clear();
			}
		}
		if (line.Length > 0)
			writer.Write(line);
		writer.WriteLine();
	}

	/// <summary>
	/// Writes an output through a temporary file next to it, moved into place once complete, so a
	/// failure leaves the previous output as it was and never a partial file.
	/// </summary>
	private static void Write(string output, Action<StreamWriter> content) {
		string temp = output + ".tmp";
		try {
			using (StreamWriter writer = new StreamWriter(temp, false, new UTF8Encoding(false))) {
				content(writer);
			}
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

	// --------------------------------------------------------------------------------------------
	// The call
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Builds the entries of a call and runs the checks that come before any write: every binary
	/// exists, no friendly name or symbol repeats. Fills Symbols.
	/// </summary>
	private bool Prepare(KList bin, KList outputs, Result result) {
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
		if (Output != OutputMode.Detailed)
			return;
		Msg.PrintTask("Bin2cpp: Processing " + file + ":");
		if (error != null)
			Msg.PrintTaskError(error);
		else
			Msg.PrintTaskSuccess(success ?? string.Empty);
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
		Msg.PrintWarning("bin2cpp: " + LastError.ToString(), Msg.LogLevels.Verbose);
		if (AbortOnFailure)
			Msg.PrintAndAbort("bin2cpp " + source + " failed (" + code + "): " + message);
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
}
