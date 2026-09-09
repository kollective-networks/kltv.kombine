#pragma kombine requires 1.6
/*---------------------------------------------------------------------------------------------------------

	Kombine Clang Extension

	(C) Kollective Networks 2026

	Verbs around the clang toolchain for build scripts: compiling sources into objects, archiving
	objects into static libraries, linking executables and shared libraries, formatting sources and
	keeping the compile database editors read. Every public member is documented in place; this
	header is the overview.

	Verbs. One method per operation on a Clang instance: Compile, Librarian, Linker, Format, Clean and
	OpenCompileCommands, plus Version, VersionCheck and ToolVersion for the tools. Each verb takes its
	arguments and reads its configuration from the ClangOptions of the instance (clang.Options); the
	options set as default with SetAsDefault() are copied into every new instance, in this script and
	in its child scripts. No variants of a verb exist: the output mode and the options decide.

	Results. Compile, Librarian and Linker return the ToolResult of the tools as they always did
	(Status Success, Failed, or NoChanges when nothing was to do; ExitCode, Stdout, Stderr); Format,
	Clean and OpenCompileCommands return true or false. Every failure leaves its reason in LastError
	(a code and the first error line) and the text the tools printed in LastOutput. What a verb did is
	in a Last<Verb> property of the instance: LastCompile (the units with their status, diagnostics and
	counts), LastLibrarian and LastLinker (the output, whether it was up to date, the objects, the
	diagnostics and their counts), LastFormat (the files formatted and rejected). Clang.Status
	accumulates the counters of the whole run, child scripts included once the main script touched
	it (Clang.Status.Reset()) before running them, as SetAsDefault() does for the options; the
	script prints or writes its own summary from those results, the extension writes no report of
	its own.

	Output. ClangOptions.Output decides what reaches the console while the tools run: Silent (nothing
	at all), Progress (one progress line per verb through ClangOptions.Progress, the default) or
	Detailed (one task line per unit). Once a build ended, Progress and Detailed print nothing when it
	had no warning and no error, and otherwise the diagnostics themselves grouped per unit, every line
	naming the offending file as the compiler wrote it. ClangOptions.Verbose adds the listings of
	include paths, defines, switches and libraries; ClangOptions.ClangVerbose passes -v to the tools.

	Abort. The abortwhenfailed parameter of a verb, null by default, takes ClangOptions.AbortOnFailure
	(true): a failure prints its reason and aborts the script, in every output mode. With false the
	verb returns its failed result and the script reads LastError and Last<Verb>; a failed unit does
	not stop the other units of the batch, so every error of a build is visible at once.

	Up to date checks. A unit is compiled when its object is missing, when its command line changed,
	when any input recorded from its previous compile (the source and every header of the dependency
	file the compiler wrote, system headers included) is missing or changed, or with rebuild. An input
	whose date and size are those of the record counts as unchanged without being read; one whose
	date or size moved is read and its content hash compared, so a file touched without an edit (a
	branch switched and switched back, a checkout, a copy) builds nothing. Every file is looked at
	once per verb call whatever the number of units that include it. The one edit that passes unseen
	is one that keeps both the date and the size of the file. The record of an object is a small file
	next to it (<object>.kdep). The archive and the link follow the same rule with their objects, the
	libraries found in the library paths and their command line; their record lives in the folder of
	their first object, named after the output, so the output folder holds nothing but what is shipped.

	Minimum version. The file declares "#pragma kombine requires 1.6": it needs the output fragments,
	the timeout and the cancellation of the Tool batches, and the progress reporters of that version.

---------------------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// An engine that compiled this file but is older than what the verbs rely on stops here with a
// friendly message instead of failing later in a callback
if (MkbHexVersion() < 0x0106)
	Msg.PrintAndAbort("The clang extension requires Kombine 1.6 or newer (running " + MkbVersion() + ")");

// ------------------------------------------------------------------------------------------------
// Enumerations
// ------------------------------------------------------------------------------------------------

/// <summary>
/// What reaches the console while the tools run, the value of ClangOptions.Output.
/// </summary>
public enum ClangOutput {
	/// <summary>Nothing at all, not even the diagnostics: the script reads LastCompile and LastError when the verb returns and prints what it wants.</summary>
	Silent,
	/// <summary>One progress line per verb through ClangOptions.Progress, started with ClangOptions.TaskLabel and ended with the result; then the diagnostics of the build, if any. The default.</summary>
	Progress,
	/// <summary>One task line per unit ("Compiling x: Ok"), the diagnostics after the batch grouped per unit, the verb closed with its result line; with Verbose the listings, with ClangVerbose the extra lines of the tools.</summary>
	Detailed
}

/// <summary>
/// What Librarian does about a symbol defined by more than one object of the archive, the value of
/// ClangOptions.DuplicateSymbols. Weak, common and COMDAT definitions (inline functions and templates,
/// which every unit emits) are not duplicates.
/// </summary>
public enum DuplicateSymbolCheck {
	/// <summary>No check: the archive takes every object as the archiver does. The default.</summary>
	Ignore,
	/// <summary>The archive is written and each duplicate is reported as a warning naming the symbol and both objects.</summary>
	Warn,
	/// <summary>The archive is not written: InvalidArgument, each duplicate reported as an error.</summary>
	Fail
}

/// <summary>
/// The outcome of one unit of a Compile, in CompileResult.Units.
/// </summary>
public enum UnitStatus {
	/// <summary>Nothing to do: the object exists and no input or command changed.</summary>
	UpToDate,
	/// <summary>Compiled without diagnostics.</summary>
	Compiled,
	/// <summary>Compiled with warnings.</summary>
	Warnings,
	/// <summary>Not compiled: the errors are in the diagnostics of the unit.</summary>
	Failed,
	/// <summary>Not run: an unknown extension (NotSupported), or the batch was cancelled before it ran.</summary>
	Skipped
}

// ------------------------------------------------------------------------------------------------
// Results
// ------------------------------------------------------------------------------------------------

/// <summary>
/// One unit of a Compile: a source, its object and what happened to it.
/// </summary>
public class CompileUnit {
	/// <summary>The source file as given to Compile.</summary>
	public string Source { get; internal set; } = string.Empty;
	/// <summary>The object file as given to Compile.</summary>
	public string Object { get; internal set; } = string.Empty;
	/// <summary>What happened to the unit.</summary>
	public UnitStatus Status { get; internal set; } = UnitStatus.UpToDate;
	/// <summary>Every line the tool printed for this unit (standard error then standard output), endings removed, as the compiler wrote them.</summary>
	public List<string> Diagnostics { get; internal set; } = new List<string>();
	/// <summary>Warning lines counted in the diagnostics (lines of the form file:line:column: warning: text).</summary>
	public int Warnings { get; internal set; } = 0;
	/// <summary>Error lines counted in the diagnostics (error and fatal error).</summary>
	public int Errors { get; internal set; } = 0;
	/// <summary>The exit code of the compiler, -1 when it was killed or never ran.</summary>
	public int ExitCode { get; internal set; } = 0;
	/// <summary>The command line of the unit, for the log and the compile database.</summary>
	public string Command { get; internal set; } = string.Empty;
}

/// <summary>
/// What Compile did, in Clang.LastCompile. The counters are those of one call; the run wide ones are
/// in Clang.Status.
/// </summary>
public class CompileResult {
	/// <summary>Every unit given to the call, in order, with its status and diagnostics.</summary>
	public List<CompileUnit> Units { get; internal set; } = new List<CompileUnit>();
	/// <summary>Units that had to be compiled (out of date or rebuild).</summary>
	public int Queued { get; internal set; } = 0;
	/// <summary>Units compiled, with or without warnings.</summary>
	public int Compiled { get; internal set; } = 0;
	/// <summary>Units that were up to date.</summary>
	public int UpToDate { get; internal set; } = 0;
	/// <summary>Warning lines counted over every unit.</summary>
	public int Warnings { get; internal set; } = 0;
	/// <summary>Error lines counted over every unit.</summary>
	public int Errors { get; internal set; } = 0;
	/// <summary>The units that failed, in order.</summary>
	public List<CompileUnit> Failed { get; internal set; } = new List<CompileUnit>();
	/// <summary>The compile database written by the call, empty when none is open.</summary>
	public string CompileDatabase { get; internal set; } = string.Empty;
	/// <summary>True when the batch was cancelled by a failure with the abort in effect: the units after the failed one are Skipped.</summary>
	public bool Cancelled { get; internal set; } = false;
}

/// <summary>
/// What Librarian or Linker did, in Clang.LastLibrarian and Clang.LastLinker.
/// </summary>
public class LinkResult {
	/// <summary>The output file, with the lib prefix when the platform takes it.</summary>
	public string Output { get; internal set; } = string.Empty;
	/// <summary>True when nothing was done: the output existed and no input or command changed.</summary>
	public bool UpToDate { get; internal set; } = false;
	/// <summary>The objects given to the call.</summary>
	public KList Objects { get; internal set; } = new KList();
	/// <summary>True for a shared library (Linker with SharedLibrary).</summary>
	public bool Shared { get; internal set; } = false;
	/// <summary>Warning lines the tool printed.</summary>
	public int Warnings { get; internal set; } = 0;
	/// <summary>Error lines the tool printed.</summary>
	public int Errors { get; internal set; } = 0;
	/// <summary>Every line the tool printed, endings removed.</summary>
	public List<string> Diagnostics { get; internal set; } = new List<string>();
}

/// <summary>
/// What Format did, in Clang.LastFormat.
/// </summary>
public class FormatResult {
	/// <summary>The files formatted.</summary>
	public KList Files { get; internal set; } = new KList();
	/// <summary>The files clang-format rejected.</summary>
	public KList Failed { get; internal set; } = new KList();
	/// <summary>What clang-format printed about them, endings removed.</summary>
	public List<string> Diagnostics { get; internal set; } = new List<string>();
}

/// <summary>
/// The version of a tool, read from "tool --version": the first major.minor[.patch] of its first
/// line, which reads clang, gcc, lld, ar and clang-format alike. The fields are -1 when the tool is
/// missing or prints no version.
/// </summary>
public class ToolVersionInfo {
	/// <summary>Major version, -1 when not available.</summary>
	public int Major { get; internal set; } = -1;
	/// <summary>Minor version, -1 when not available.</summary>
	public int Minor { get; internal set; } = -1;
	/// <summary>Patch version, -1 when not available (0 when the tool prints only major.minor).</summary>
	public int Patch { get; internal set; } = -1;
	/// <summary>The first line the tool printed.</summary>
	public string Text { get; internal set; } = string.Empty;
	/// <summary>True when a version was read.</summary>
	public bool Available { get { return Major >= 0; } }
	/// <summary>"major.minor.patch", or "not available".</summary>
	public override string ToString() {
		return Available ? Major + "." + Minor + "." + Patch : "not available";
	}
}

/// <summary>
/// The counters accumulated over the run: every Compile, Librarian, Linker and Format of this
/// script and of its child scripts adds to them. The numbers live in a container of the Share
/// registry, created by the first script that touches the status and handed to the child scripts
/// it runs from then on, the way SetAsDefault() hands the options: touch it in the main script
/// (Clang.Status.Reset()) before running the children, so every script adds to the same numbers;
/// a child that finds none keeps its own. The diagnostics are not accumulated, each verb prints its
/// own and keeps them in its result. Reset() starts over, Print() prints the summary on demand.
/// </summary>
public class ClangStatus {
	private const string Key = "ClangStatus";

	/// <summary>
	/// The shared container, created on the first use of the run. A dictionary, a type every script
	/// knows, since a class of this file would be a different type in a child script.
	/// </summary>
	private static Dictionary<string, long> Container() {
		object? o = Share.Get(Key);
		if (o is Dictionary<string, long> d)
			return d;
		Dictionary<string, long> n = new Dictionary<string, long>();
		Share.Set(Key, n);
		o = Share.Get(Key);
		return (o as Dictionary<string, long>) ?? n;
	}

	private long Get(string key) {
		Dictionary<string, long> d = Container();
		lock (d) {
			return d.TryGetValue(key, out long v) ? v : 0;
		}
	}

	/// <summary>Adds to one counter.</summary>
	internal void Add(string key, long n) {
		Dictionary<string, long> d = Container();
		lock (d) {
			d[key] = (d.TryGetValue(key, out long v) ? v : 0) + n;
		}
	}

	/// <summary>Units queued (out of date) over the run, links, archives and formats included as one each.</summary>
	public long Queued { get { return Get("queued"); } }
	/// <summary>Units completed without error over the run.</summary>
	public long Completed { get { return Get("completed"); } }
	/// <summary>Warning lines counted over the run.</summary>
	public long Warnings { get { return Get("warnings"); } }
	/// <summary>Error lines counted over the run.</summary>
	public long Errors { get { return Get("errors"); } }

	/// <summary>Sets every counter to zero.</summary>
	public void Reset() {
		Dictionary<string, long> d = Container();
		lock (d) {
			d.Clear();
		}
	}

	/// <summary>
	/// Prints the summary of the counters in one line ("Build status: 48 queued, 45 completed, 3
	/// warnings, 0 errors") at normal level, whatever the output mode: the script asked for it.
	/// </summary>
	public void Print() {
		Msg.Print("Build status: " + Queued + " queued, " + Completed + " completed, " + Warnings + " warnings, " + Errors + " errors");
	}
}

// ------------------------------------------------------------------------------------------------
// The extension
// ------------------------------------------------------------------------------------------------

/// <summary>
/// The clang toolchain for build scripts: one instance per configuration, its verbs reading the
/// options of the instance. See the header of the file for the overview.
/// </summary>
public class Clang {

	/// <summary>
	/// The configuration of an instance, shared through SetAsDefault(): every new Clang instance, in
	/// this script or in a child script, starts with a copy of the options set as default. Every
	/// default corresponds to what the previous extension did.
	/// </summary>
	public class ClangOptions {

		/// <summary>
		/// Selects the artifact extensions of the platform and the concurrency of the machine.
		/// </summary>
		public ClangOptions() {
			if (Host.IsWindows()) {
				LibExtension = ".lib";
				SharedExtension = ".dll";
				BinaryExtension = ".exe";
				ObjectExtension = ".obj";
			} else if (Host.IsLinux()) {
				LibExtension = ".a";
				SharedExtension = ".so";
				BinaryExtension = ".out";
				ObjectExtension = ".o";
			} else if (Host.IsMacOS()) {
				LibExtension = ".a";
				SharedExtension = ".dylib";
				BinaryExtension = ".out";
				ObjectExtension = ".o";
			} else {
				Msg.PrintAndAbort("Error: Unknown OS");
			}
			ConcurrentBuild = Host.ProcessorCount();
		}

		/// <summary>
		/// Stores these options in the Share registry as the defaults every new Clang instance copies,
		/// in this script and in its child scripts. The first call of the run sets them; a later call
		/// does not replace them (the registry keeps the first object under a name).
		/// </summary>
		public void SetAsDefault() {
			Share.Set(SharedName, this);
		}

		/// <summary>The C compiler. Default "clang"; "gcc" takes the same command lines.</summary>
		public string CC { get; set; } = "clang";

		/// <summary>The C++ compiler, and the driver of the link. Default "clang++"; "g++" takes the same command lines.</summary>
		public string CXX { get; set; } = "clang++";

		/// <summary>
		/// The linker the driver must use. A name ("lld", "gold", "bfd", "mold") is passed to the driver as
		/// -fuse-ld=name; a path to a linker as --ld-path=path (clang only); empty adds nothing and the driver
		/// picks the linker of its target, which is how a macOS build or a Linux without lld links. Default
		/// "lld", the linker the previous extension forced on every link.
		/// </summary>
		public string LD { get; set; } = "lld";

		/// <summary>The archiver. Default "llvm-ar"; "ar" takes the same command line.</summary>
		public string AR { get; set; } = "llvm-ar";

		/// <summary>The resource compiler of Windows resources. Default "llvm-rc" (the command line of rc.exe).</summary>
		public string RC { get; set; } = "llvm-rc";

		/// <summary>The object reader Librarian uses to find the symbols every object defines (readobj --symbols). Default "llvm-readobj".</summary>
		public string ReadObj { get; set; } = "llvm-readobj";

		/// <summary>
		/// What Librarian does about a symbol defined by more than one object of the archive: Ignore, the
		/// default, skips the check; Warn writes the archive and reports each duplicate as a warning naming the
		/// symbol and both objects; Fail refuses the archive with InvalidArgument. The check reads the symbols
		/// of the objects through ReadObj once per archive written, which costs a moment on a large archive;
		/// weak, common and COMDAT definitions are not duplicates.
		/// </summary>
		public DuplicateSymbolCheck DuplicateSymbols { get; set; } = DuplicateSymbolCheck.Ignore;

		/// <summary>The extensions of the C sources, several separated by ";" (".c"). The comparison ignores the case on Windows and macOS.</summary>
		public string CExtension { get; set; } = ".c";

		/// <summary>The extensions of the C++ sources, several separated by ";" (".cpp"). The comparison ignores the case on Windows and macOS.</summary>
		public string CppExtension { get; set; } = ".cpp";

		/// <summary>The extensions of the resource sources, several separated by ";" (".rc").</summary>
		public string ResExtension { get; set; } = ".rc";

		/// <summary>Include directories, each passed as -I to every unit.</summary>
		public KList IncludeDirs { get; set; } = new KList();

		/// <summary>Defines, each passed as -D to every unit.</summary>
		public KList Defines { get; set; } = new KList();

		/// <summary>Switches of the C units.</summary>
		public KList SwitchesCC { get; set; } = new KList();

		/// <summary>Switches of the C++ units.</summary>
		public KList SwitchesCXX { get; set; } = new KList();

		/// <summary>Library directories, each passed as -L to the link.</summary>
		public KList LibraryDirs { get; set; } = new KList();

		/// <summary>Libraries of the link: a name is passed as -lname, a full path as it is.</summary>
		public KList Libraries { get; set; } = new KList();

		/// <summary>Switches of the link.</summary>
		public KList SwitchesLD { get; set; } = new KList();

		/// <summary>Units compiled in parallel (and files formatted in parallel). Default: the number of processors.</summary>
		public int ConcurrentBuild { get; set; } = 0;

		/// <summary>
		/// The extension prints its own details: the listings of include paths, defines, switches, library
		/// paths and libraries, and the response file messages, in the Progress mode (before the progress
		/// line) and in the Detailed mode. Default false: the listings that the previous extension printed on
		/// every build print only with it. The -v of the tools, which Verbose passed before, is ClangVerbose.
		/// </summary>
		public bool Verbose { get; set; } = false;

		/// <summary>Passes -v to the compilers and to the linker and rcsv to the archiver; their extra lines show in the Detailed mode. Default false.</summary>
		public bool ClangVerbose { get; set; } = false;

		/// <summary>
		/// The start message of the progress line in the Progress mode. Empty, the default, uses the verb
		/// and the output: "Compiling 48 files", "Linking app.exe", "Library libapp.a", "Formatting 3 files".
		/// </summary>
		public string TaskLabel { get; set; } = string.Empty;

		/// <summary>
		/// True, the default, prints the warnings in full in the report that follows a build; false reduces
		/// them to their count per unit. The errors are always printed in full.
		/// </summary>
		public bool ShowWarnings { get; set; } = true;

		/// <summary>
		/// Given the source of a unit and one diagnostic line, returns whether the diagnostic (the line and
		/// the excerpt that follows it) is printed in the report. Null, the default, prints everything. The
		/// counts and the results are not affected: a hidden warning is still a warning.
		/// </summary>
		public Func<string, string, bool>? DiagnosticFilter { get; set; } = null;

		/// <summary>What reaches the console while the tools run. Default Progress.</summary>
		public ClangOutput Output { get; set; } = ClangOutput.Progress;

		/// <summary>The reporter of the progress line in the Progress mode: a ProgressBar, ProgressDots or ProgressPlain. Null, the default, takes Progress.Default of the engine.</summary>
		public ITaskProgress? Progress { get; set; } = null;

		/// <summary>The default of the abortwhenfailed parameter of every verb: true, the default, aborts the script on a failure after printing its reason.</summary>
		public bool AbortOnFailure { get; set; } = true;

		/// <summary>Milliseconds one tool invocation (one unit, one link, one archive, one format) may run before it is killed and counted as failed. Zero, the default, no limit.</summary>
		public int Timeout { get; set; } = 0;

		/// <summary>The static library extension of the platform (.lib on Windows, .a elsewhere). Read only.</summary>
		public string LibExtension { get; private set; } = ".a";

		/// <summary>The shared library extension of the platform (.dll, .so, .dylib). Read only.</summary>
		public string SharedExtension { get; private set; } = ".so";

		/// <summary>The executable extension of the platform (.exe on Windows, .out elsewhere). Read only.</summary>
		public string BinaryExtension { get; private set; } = ".out";

		/// <summary>The object extension of the platform (.obj on Windows, .o elsewhere). Read only.</summary>
		public string ObjectExtension { get; private set; } = ".o";

		/// <summary>The name the options are shared under.</summary>
		internal const string SharedName = "ClangOptions";

		/// <summary>
		/// Copies every property of another options object into this one, the lists as new lists, so a
		/// change on this instance never touches the source. The source may be the options of another
		/// script (a different type with the same properties): the copy goes by property name, and an
		/// enumeration travels by its number so Output arrives whatever the type it was declared in.
		/// </summary>
		internal void CopyFrom(object source) {
			Type mine = GetType();
			Type theirs = source.GetType();
			foreach (System.Reflection.PropertyInfo p in mine.GetProperties()) {
				if (p.SetMethod == null || !p.SetMethod.IsPublic)
					continue;
				System.Reflection.PropertyInfo? s = theirs.GetProperty(p.Name);
				if (s == null)
					continue;
				object? value;
				try {
					value = s.GetValue(source);
				} catch {
					continue;
				}
				if (value == null) {
					p.SetValue(this, null);
					continue;
				}
				try {
					if (p.PropertyType.IsEnum) {
						p.SetValue(this, Enum.ToObject(p.PropertyType, Convert.ToInt32(value)));
					} else if (p.PropertyType == typeof(KList) && value is KList list) {
						p.SetValue(this, new KList(list));
					} else if (p.PropertyType.IsInstanceOfType(value)) {
						p.SetValue(this, value);
					}
				} catch (Exception ex) {
					Msg.Print("clang: option " + p.Name + " not copied: " + ex.Message, Msg.LogLevels.Verbose);
				}
			}
		}
	}

	/// <summary>
	/// Creates an instance with a copy of the options set as default (or the built in defaults) and the
	/// compile database opened by a previous instance of the run, if any.
	/// </summary>
	public Clang() {
		OpenSharedCompileOptions();
		OpenSharedCompileCommands();
		ProcessFile = null;
	}

	/// <summary>The options of this instance, read by every verb.</summary>
	public ClangOptions Options { get; private set; } = new ClangOptions();

	/// <summary>
	/// Delegate called with every unit about to be compiled; returns extra arguments for it.
	/// </summary>
	/// <param name="file">The source about to be compiled, as given to Compile.</param>
	/// <returns>Arguments appended to the command line of that unit, empty for none.</returns>
	public delegate string ProcessFileDelegate(string file);

	/// <summary>Called with every unit about to be compiled to add arguments for it. Null by default.</summary>
	public ProcessFileDelegate? ProcessFile;

	/// <summary>The reason of the last failure: NotFound (a source, an object or a tool), Failed (a compile, link, archive or format error, a timeout, a failure of the tool), InvalidArgument (mismatched lists, duplicates), NotSupported (an unknown extension, a version too old), IoError. Reset by every verb.</summary>
	public ApiError LastError { get; private set; } = ApiError.None;

	/// <summary>The text the tools printed in the last verb, every unit in order.</summary>
	public string LastOutput { get; private set; } = string.Empty;

	/// <summary>What the last Compile did. Null before the first call.</summary>
	public CompileResult? LastCompile { get; private set; } = null;

	/// <summary>What the last Librarian did. Null before the first call.</summary>
	public LinkResult? LastLibrarian { get; private set; } = null;

	/// <summary>What the last Linker did. Null before the first call.</summary>
	public LinkResult? LastLinker { get; private set; } = null;

	/// <summary>What the last Format did. Null before the first call.</summary>
	public FormatResult? LastFormat { get; private set; } = null;

	/// <summary>The counters accumulated over the run by every instance of this script and of its child scripts.</summary>
	public static ClangStatus Status { get; } = new ClangStatus();

	// --------------------------------------------------------------------------------------------
	// Verbs
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Compiles the sources that are out of date into their objects, one to one. A unit runs when its
	/// object is missing, when its command line changed, when any input of its previous compile (the
	/// source and the headers of its dependency file, system headers included) is missing or changed
	/// in content, or with rebuild. The units run in parallel up to ConcurrentBuild. The dependency
	/// file of each unit is written next to its object (-MD -MF) and the compile database, if one is
	/// open, is updated after the checks, without the resource units.
	/// </summary>
	/// <param name="src">The sources.</param>
	/// <param name="obj">The objects, in the same order and number (InvalidArgument otherwise, as are duplicates).</param>
	/// <param name="abortwhenfailed">Null takes ClangOptions.AbortOnFailure. True aborts the script on a failure after the report; false keeps compiling the other units and returns the failed result.</param>
	/// <param name="rebuild">True compiles every unit whatever its state.</param>
	/// <returns>The ToolResult of the batch: Success (warnings or not), NoChanges when nothing was to do, Failed otherwise with the reason in LastError. Stdout and Stderr hold the text of every unit. The detail is in LastCompile.</returns>
	public ToolResult Compile(KList src, KList obj, bool? abortwhenfailed = null, bool rebuild = false) {
		bool abort = abortwhenfailed ?? Options.AbortOnFailure;
		Begin();
		CompileResult result = new CompileResult();
		LastCompile = result;
		result.CompileDatabase = compdb != null ? compdbFile : string.Empty;
		// Sanity checks over the parameters passed
		if (src.Count() != obj.Count())
			return FailResult(ErrorCode.InvalidArgument, "the source and object lists must have the same number of elements (" + src.Count() + " and " + obj.Count() + ")", "Compile", abort);
		if (src.HasDuplicates() || obj.HasDuplicates())
			return FailResult(ErrorCode.InvalidArgument, "the source or object list has duplicates", "Compile", abort);
		// A rebuilt script no longer forces a rebuild of every unit, as the previous extension did: the
		// command line of every unit is recorded, so a script change that alters it compiles by itself
		// The listings, with Verbose
		if (Options.Verbose && Options.Output != ClangOutput.Silent) {
			Listing("Include paths:", Options.IncludeDirs);
			Listing("Defines:", Options.Defines);
			Listing("Switches for C compiler:", Options.SwitchesCC);
			Listing("Switches for C++ compiler:", Options.SwitchesCXX);
		}
		// The common arguments
		foreach (KValue v in Options.IncludeDirs)
			if (v.IsEmpty())
				return FailResult(ErrorCode.InvalidArgument, "empty include directory found", "Compile", abort);
		foreach (KValue v in Options.Defines)
			if (v.IsEmpty())
				return FailResult(ErrorCode.InvalidArgument, "empty define found", "Compile", abort);
		string includes = Join(Options.IncludeDirs.Select(v => "-I" + Q(v)));
		string defines = Join(Options.Defines.Select(v => "-D" + Q(v)));
		string switchesCC = Join(Options.SwitchesCC.Select(v => (string)v));
		string switchesCXX = Join(Options.SwitchesCXX.Select(v => (string)v));
		if (Options.ClangVerbose) {
			switchesCC = Join(new[] { switchesCC, "-v" });
			switchesCXX = Join(new[] { switchesCXX, "-v" });
		}
		// The units: classified, checked, and queued when out of date
		List<Job> jobs = new List<Job>();
		Dictionary<string, string> tools = new Dictionary<string, string>();
		System.Diagnostics.Stopwatch checks = System.Diagnostics.Stopwatch.StartNew();
		for (int a = 0; a != src.Count(); a++) {
			CompileUnit unit = new CompileUnit { Source = src[a], Object = obj[a] };
			result.Units.Add(unit);
			string srcf = RealPath(src[a]);
			string objf = RealPath(obj[a]);
			string cmd;
			string args;
			string property;
			bool resource = false;
			if (HasExtension(src[a], Options.CExtension)) {
				cmd = Options.CC;
				property = "CC";
				args = Join(new[] { "-c", "-MD", "-MF", Q(DepFile(objf)), includes, defines, switchesCC, Q(srcf), "-o", Q(objf), Extra(src[a]) });
			} else if (HasExtension(src[a], Options.CppExtension)) {
				cmd = Options.CXX;
				property = "CXX";
				args = Join(new[] { "-c", "-MD", "-MF", Q(DepFile(objf)), includes, defines, switchesCXX, Q(srcf), "-o", Q(objf), Extra(src[a]) });
			} else if (HasExtension(src[a], Options.ResExtension)) {
				cmd = Options.RC;
				property = "RC";
				resource = true;
				args = Join(new[] { Q(srcf), "/FO", Q(objf) });
			} else {
				unit.Status = UnitStatus.Skipped;
				return FailResult(ErrorCode.NotSupported, "the source " + src[a] + " has an extension outside CExtension, CppExtension and ResExtension", "Compile", abort);
			}
			unit.Command = cmd + " " + args;
			// Existence first: a missing source is a failure before anything is queued
			if (!File.Exists(srcf))
				return FailResult(ErrorCode.NotFound, "source file not found: " + src[a], "Compile", abort);
			// The tool once per run
			if (!tools.ContainsKey(property)) {
				if (FindTool(cmd) == null)
					return FailResult(ErrorCode.NotFound, "tool not found: " + cmd + " (ClangOptions." + property + ")", "Compile", abort);
				tools[property] = cmd;
			}
			// The compile database receives the unit once it passed the checks, resources excluded
			if (!resource)
				AddCompileCommands(unit.Command, srcf, objf);
			if (rebuild || !UpToDate(objf, unit.Command, srcf, resource ? ResourceInputs(srcf) : null)) {
				jobs.Add(new Job { Cmd = cmd, Args = args, Unit = unit, Label = src[a], Property = property, Resource = resource, Source = srcf, Output = objf });
			} else {
				unit.Status = UnitStatus.UpToDate;
				result.UpToDate++;
			}
		}
		result.Queued = jobs.Count;
		Msg.Print("clang: " + src.Count() + " units checked in " + checks.ElapsedMilliseconds + " ms, " + jobs.Count + " to compile", Msg.LogLevels.Verbose);
		Status.Add("queued", jobs.Count);
		Folders.Create(obj.AsFolders());
		string label = Options.TaskLabel.Length > 0 ? Options.TaskLabel : "Compiling " + src.Count() + " file" + (src.Count() == 1 ? "" : "s");
		ToolResult res;
		try {
			res = RunBatch("Compile", label, jobs, Options.ConcurrentBuild, abort, result);
		} finally {
			// Whatever happened while the units ran, the compile database is saved
			compdb?.Save();
		}
		// The counters of the units, the records of the compiled ones
		foreach (Job j in jobs) {
			CompileUnit u = j.Unit!;
			result.Warnings += u.Warnings;
			result.Errors += u.Errors;
			if (u.Status == UnitStatus.Compiled || u.Status == UnitStatus.Warnings) {
				result.Compiled++;
				Record(RecordFile(j.Output), u.Command, DepInputs(j.Source, j.Output, j.Resource));
			} else {
				DeleteRecord(RecordFile(j.Output));
				if (u.Status == UnitStatus.Failed)
					result.Failed.Add(u);
			}
		}
		result.Cancelled = jobs.Any(j => j.Unit!.Status == UnitStatus.Skipped);
		Status.Add("completed", result.Compiled);
		Status.Add("warnings", result.Warnings);
		Status.Add("errors", result.Errors);
		LastOutput = string.Join("", jobs.Select(j => j.Text));
		// The report and the result line
		Report("Compile", jobs, result.Warnings, result.Errors, result.Failed.Count, result.Queued == 0, jobs.Any(j => j.ToolFailure));
		if (result.Failed.Count > 0 || result.Cancelled)
			return FailResult(ErrorCode.Failed, FailureMessage(jobs), "Compile", abort, res);
		if (jobs.Count == 0)
			return ToolResult.DefaultNoChanges();
		return res;
	}

	/// <summary>
	/// Builds a static library from the objects when the archive is missing, when an object or the
	/// command line changed (an object removed from the list too: the archive is written again without
	/// it), or when an object is missing from the previous record. The archive is deleted and created
	/// again with every object in one command (ar rcs); when the command line exceeds what the
	/// platform allows, the objects go through a response file in the temp folder, removed afterwards.
	/// </summary>
	/// <param name="objs">The objects. Every one must exist (NotFound otherwise). A symbol defined by two of them is reported per ClangOptions.DuplicateSymbols when the check is on (a warning with Warn, InvalidArgument with Fail).</param>
	/// <param name="output">The archive; on Linux and macOS the name takes the lib prefix.</param>
	/// <param name="abortwhenfailed">Null takes ClangOptions.AbortOnFailure.</param>
	/// <returns>The ToolResult of the archiver: Success, NoChanges when the archive was up to date, Failed otherwise with the reason in LastError. The detail is in LastLibrarian.</returns>
	public ToolResult Librarian(KList objs, KValue output, bool? abortwhenfailed = null) {
		bool abort = abortwhenfailed ?? Options.AbortOnFailure;
		Begin();
		if (Host.IsLinux() || Host.IsMacOS())
			output = Unix(output.WithNamePrefix("lib"));
		LinkResult result = new LinkResult { Output = output, Objects = new KList(objs) };
		LastLibrarian = result;
		if (FindTool(Options.AR) == null)
			return FailResult(ErrorCode.NotFound, "tool not found: " + Options.AR + " (ClangOptions.AR)", "Librarian", abort);
		List<string> inputs = new List<string>();
		foreach (KValue o in objs) {
			string f = RealPath(o);
			if (!File.Exists(f))
				return FailResult(ErrorCode.NotFound, "object file not found: " + o, "Librarian", abort);
			inputs.Add(f);
		}
		string outf = RealPath(output);
		string mode = Options.ClangVerbose ? "rcsv" : "rcs";
		// The recorded command carries a version of the way the archive is written: the archives the
		// previous, chunked way left incomplete are made again once
		string command = "archive-v2 " + Options.AR + " " + mode + " " + Q(outf) + " " + Join(inputs.Select(i => Q(i)));
		string label = Options.TaskLabel.Length > 0 ? Options.TaskLabel : "Library " + Path.GetFileName(outf);
		if (UpToDate(outf, command, null, inputs)) {
			result.UpToDate = true;
			UpToDateLine("Librarian", label);
			return ToolResult.DefaultNoChanges();
		}
		// A symbol defined by two objects: the archiver keeps both members and the linker loads both.
		// Reported here, where the mistake is (a source list taking a generic and a platform folder)
		List<string> duplicates = new List<string>();
		if (Options.DuplicateSymbols != DuplicateSymbolCheck.Ignore) {
			string severity = Options.DuplicateSymbols == DuplicateSymbolCheck.Fail ? "error" : "warning";
			List<string>? found = DuplicateSymbols(inputs, severity, out string note);
			if (found == null) {
				if (Options.DuplicateSymbols == DuplicateSymbolCheck.Fail)
					return FailResult(ErrorCode.NotFound, note, "Librarian", abort);
				duplicates.Add("librarian: warning: " + note);
			} else {
				duplicates = found;
			}
			if (Options.DuplicateSymbols == DuplicateSymbolCheck.Fail && found != null && found.Count > 0) {
				Job refused = new Job { Cmd = Options.AR, Label = Path.GetFileName(outf), Property = "AR", Output = outf, Lines = found, Errors = found.Count, Failed = true, Ran = true };
				if (Options.Output == ClangOutput.Progress) {
					ITaskProgress progress = Options.Progress ?? Kltv.Kombine.Api.Progress.Default;
					progress.Start(label);
					progress.Finish(ResultText(1, found.Count, 0, false, false), ProgressOutcome.Error);
				}
				result.Errors = found.Count;
				result.Diagnostics.AddRange(found);
				Report("Librarian", new List<Job> { refused }, 0, found.Count, 1, false, false);
				return FailResult(ErrorCode.InvalidArgument, found[0].Substring("librarian: error: ".Length) + (found.Count > 1 ? " (" + found.Count + " duplicate symbols)" : ""), "Librarian", abort);
			}
		}
		Folders.Create(output.AsFolder());
		// Created again from scratch so a removed object leaves it
		if (File.Exists(outf))
			File.Delete(outf);
		DeleteRecord(LinkRecordFile(outf, inputs));
		if (Options.Verbose && Options.Output == ClangOutput.Detailed)
			foreach (string o in inputs)
				Msg.Print("Adding object: " + o);
		// One command with every object. When the line exceeds what the platform allows (Windows:
		// 32767) the objects go through a response file, never through several incremental commands:
		// each of those reads the archive and writes it back, so two overlapping ones lose objects
		Status.Add("queued", 1);
		Job job = new Job { Cmd = Options.AR, Args = mode + " " + Q(outf) + " " + Join(inputs.Select(i => Q(i))), Label = Path.GetFileName(outf), Property = "AR", Output = outf, Extra = duplicates };
		string? responsefile = null;
		ToolResult res;
		try {
			if (job.Args.Length > 32766) {
				responsefile = Path.Combine(Path.GetTempPath(), "kombine-" + Guid.NewGuid().ToString("N") + ".rsp");
				// Backslashes are escapes inside a response file; one object per line, quoted when needed
				File.WriteAllLines(responsefile, inputs.Select(i => Q(i.Replace("\\", "/"))));
				job.Args = mode + " " + Q(outf) + " @" + Q(responsefile);
				if (Options.Verbose && Options.Output == ClangOutput.Detailed)
					Msg.Print("Response file created: " + responsefile);
			}
			res = RunBatch("Librarian", label, new List<Job> { job }, 1, abort, null);
		} finally {
			if (responsefile != null) {
				try {
					File.Delete(responsefile);
					if (Options.Verbose && Options.Output == ClangOutput.Detailed)
						Msg.Print("Response file deleted: " + responsefile);
				} catch (Exception ex) {
					Msg.PrintWarning("clang: response file not deleted: " + responsefile + " (" + ex.Message + ")", Msg.LogLevels.Verbose);
				}
			}
		}
		List<Job> jobs = new List<Job> { job };
		result.Warnings = job.Warnings;
		result.Errors = job.Errors;
		result.Diagnostics.AddRange(job.Lines);
		LastOutput = string.Join("", jobs.Select(j => j.Text));
		bool failed = jobs.Any(j => j.Failed);
		Status.Add("completed", failed ? 0 : 1);
		Status.Add("warnings", result.Warnings);
		Status.Add("errors", result.Errors);
		Report("Librarian", jobs, result.Warnings, result.Errors, failed ? 1 : 0, false, jobs.Any(j => j.ToolFailure));
		if (failed) {
			if (File.Exists(outf))
				File.Delete(outf);
			return FailResult(ErrorCode.Failed, FailureMessage(jobs), "Librarian", abort, res);
		}
		Record(LinkRecordFile(outf, inputs), command, inputs);
		return res;
	}

	/// <summary>
	/// Links the objects into an executable, or into a shared library with SharedLibrary, when the
	/// output is missing, when the command line changed, or when an object or a library of the options
	/// (found in the library paths) is missing or changed. The command is the C++ compiler of the
	/// options as driver, the linker of LD (-fuse-ld= or --ld-path=, nothing when LD is empty), the
	/// switches of SwitchesLD, -shared for a shared library and -v with ClangVerbose. When the command
	/// line exceeds what the platform allows, the arguments go through a response file in the temp
	/// folder, removed afterwards whatever happens.
	/// </summary>
	/// <param name="objs">The objects. Every one must exist (NotFound otherwise).</param>
	/// <param name="output">The output file; a shared library takes the lib prefix on Linux and macOS.</param>
	/// <param name="SharedLibrary">True links a shared library.</param>
	/// <param name="abortwhenfailed">Null takes ClangOptions.AbortOnFailure.</param>
	/// <returns>The ToolResult of the driver: Success, NoChanges when the output was up to date, Failed otherwise with the reason in LastError. The detail is in LastLinker.</returns>
	public ToolResult Linker(KList objs, KValue output, bool SharedLibrary = false, bool? abortwhenfailed = null) {
		bool abort = abortwhenfailed ?? Options.AbortOnFailure;
		Begin();
		if ((Host.IsLinux() || Host.IsMacOS()) && SharedLibrary)
			output = Unix(output.WithNamePrefix("lib"));
		LinkResult result = new LinkResult { Output = output, Objects = new KList(objs), Shared = SharedLibrary };
		LastLinker = result;
		if (FindTool(Options.CXX) == null)
			return FailResult(ErrorCode.NotFound, "tool not found: " + Options.CXX + " (ClangOptions.CXX)", "Linker", abort);
		foreach (KValue v in Options.LibraryDirs)
			if (v.IsEmpty())
				return FailResult(ErrorCode.InvalidArgument, "empty library directory found", "Linker", abort);
		foreach (KValue v in Options.Libraries)
			if (v.IsEmpty())
				return FailResult(ErrorCode.InvalidArgument, "empty library found", "Linker", abort);
		if (Options.Verbose && Options.Output != ClangOutput.Silent) {
			Listing("Library paths:", Options.LibraryDirs);
			Listing("Libraries:", Options.Libraries);
			Listing("Switches for linker:", Options.SwitchesLD);
		}
		List<string> inputs = new List<string>();
		foreach (KValue o in objs) {
			string f = RealPath(o);
			if (!File.Exists(f))
				return FailResult(ErrorCode.NotFound, "object file not found: " + o, "Linker", abort);
			inputs.Add(f);
		}
		// The libraries of the options that exist as files are inputs: a rebuilt library links again
		inputs.AddRange(LibraryFiles());
		string outf = RealPath(output);
		string libdirs = Join(Options.LibraryDirs.Select(v => "-L" + Q(v)));
		string libs = Join(Options.Libraries.Select(v => Path.IsPathRooted(v) ? Q(v) : "-l" + (string)v));
		string switches = Join(Options.SwitchesLD.Select(v => (string)v));
		if (Options.ClangVerbose)
			switches = Join(new[] { switches, "-v" });
		if (SharedLibrary)
			switches = Join(new[] { switches, "-shared" });
		string args = Join(new[] { LinkerSwitch(), switches, Join(inputs.Take(objs.Count()).Select(i => Q(i))), libdirs, libs, "-o", Q(outf) });
		string command = Options.CXX + " " + args;
		string label = Options.TaskLabel.Length > 0 ? Options.TaskLabel : "Linking " + Path.GetFileName(outf);
		if (UpToDate(outf, command, null, inputs)) {
			result.UpToDate = true;
			UpToDateLine("Linker", label);
			return ToolResult.DefaultNoChanges();
		}
		Folders.Create(output.AsFolder());
		DeleteRecord(LinkRecordFile(outf, inputs));
		Status.Add("queued", 1);
		// A response file when the line is too long for the platform (Windows: 32767)
		string? responsefile = null;
		Job job = new Job { Cmd = Options.CXX, Args = args, Label = Path.GetFileName(outf), Property = "CXX", Output = outf };
		ToolResult res;
		try {
			if (args.Length > 32766) {
				responsefile = Path.Combine(Path.GetTempPath(), "kombine-" + Guid.NewGuid().ToString("N") + ".rsp");
				// Backslashes are escapes inside a response file
				File.WriteAllText(responsefile, args.Replace("\\", "/"));
				job.Args = "@" + Q(responsefile);
				if (Options.Verbose && Options.Output == ClangOutput.Detailed)
					Msg.Print("Response file created: " + responsefile);
			}
			res = RunBatch("Linker", label, new List<Job> { job }, 1, abort, null);
		} finally {
			if (responsefile != null) {
				try {
					File.Delete(responsefile);
					if (Options.Verbose && Options.Output == ClangOutput.Detailed)
						Msg.Print("Response file deleted: " + responsefile);
				} catch (Exception ex) {
					Msg.PrintWarning("clang: response file not deleted: " + responsefile + " (" + ex.Message + ")", Msg.LogLevels.Verbose);
				}
			}
		}
		result.Warnings = job.Warnings;
		result.Errors = job.Errors;
		result.Diagnostics.AddRange(job.Lines);
		LastOutput = job.Text;
		Status.Add("completed", job.Failed ? 0 : 1);
		Status.Add("warnings", result.Warnings);
		Status.Add("errors", result.Errors);
		Report("Linker", new List<Job> { job }, result.Warnings, result.Errors, job.Failed ? 1 : 0, false, job.ToolFailure);
		if (job.Failed)
			return FailResult(ErrorCode.Failed, FailureMessage(new List<Job> { job }), "Linker", abort, res);
		Record(LinkRecordFile(outf, inputs), command, inputs);
		return res;
	}

	/// <summary>
	/// Formats the files in place with clang-format, one command per file run in parallel up to
	/// ConcurrentBuild, through the same runner as the other verbs: the output mode, the timeout, the
	/// diagnostics collected and printed after the batch, the abort rule.
	/// </summary>
	/// <param name="src">The files to format. Every one must exist (NotFound otherwise).</param>
	/// <param name="extraArgs">Arguments added to every clang-format command, for example "--style=file".</param>
	/// <param name="abortwhenfailed">Null takes ClangOptions.AbortOnFailure.</param>
	/// <returns>True when every file was formatted; false with LastError (NotFound for a missing tool or file, Failed for a rejected file). The detail is in LastFormat.</returns>
	public bool Format(KList src, string extraArgs, bool? abortwhenfailed = null) {
		bool abort = abortwhenfailed ?? Options.AbortOnFailure;
		Begin();
		FormatResult result = new FormatResult();
		LastFormat = result;
		if (src.Count() == 0)
			return true;
		if (FindTool("clang-format") == null)
			return Fail(ErrorCode.NotFound, "tool not found: clang-format", "Format", abort);
		List<Job> jobs = new List<Job>();
		foreach (KValue f in src) {
			string file = RealPath(f);
			if (!File.Exists(file))
				return Fail(ErrorCode.NotFound, "file not found: " + f, "Format", abort);
			jobs.Add(new Job { Cmd = "clang-format", Args = Join(new[] { "-i", Q(file), extraArgs ?? string.Empty }), Label = f, Property = "clang-format", Output = file });
		}
		Status.Add("queued", jobs.Count);
		string label = Options.TaskLabel.Length > 0 ? Options.TaskLabel : "Formatting " + jobs.Count + " file" + (jobs.Count == 1 ? "" : "s");
		RunBatch("Format", label, jobs, Options.ConcurrentBuild, abort, null);
		int warnings = 0, errors = 0, failed = 0;
		foreach (Job j in jobs) {
			if (j.Failed) {
				failed++;
				result.Failed.Add(j.Label);
			} else {
				result.Files.Add(j.Label);
			}
			warnings += j.Warnings;
			errors += j.Errors;
			result.Diagnostics.AddRange(j.Lines);
		}
		LastOutput = string.Join("", jobs.Select(j => j.Text));
		Status.Add("completed", jobs.Count - failed);
		Status.Add("warnings", warnings);
		Status.Add("errors", errors);
		Report("Format", jobs, warnings, errors, failed, false, jobs.Any(j => j.ToolFailure));
		if (failed > 0)
			return Fail(ErrorCode.Failed, FailureMessage(jobs), "Format", abort);
		return true;
	}

	/// <summary>
	/// Deletes the folders of the objects and the folder of the output, with everything in them
	/// (the dependency files and the records included).
	/// </summary>
	/// <param name="obj">The objects whose folders are deleted.</param>
	/// <param name="output">The output whose folder is deleted.</param>
	/// <returns>True when the folders are gone (already absent counts); false with IoError in LastError otherwise.</returns>
	public bool Clean(KList obj, KValue output) {
		Begin();
		KList folders = obj.AsFolders();
		Folders.Delete(folders, true);
		Folders.Delete(output.AsFolder(), true);
		foreach (KValue f in folders)
			if (Folders.Exists(f))
				return Fail(ErrorCode.IoError, "folder not deleted: " + f + (Folders.LastError.IsError ? " (" + Folders.LastError.Message + ")" : ""), "Clean", false);
		if (Folders.Exists(output.AsFolder()))
			return Fail(ErrorCode.IoError, "folder not deleted: " + output.AsFolder() + (Folders.LastError.IsError ? " (" + Folders.LastError.Message + ")" : ""), "Clean", false);
		return true;
	}

	/// <summary>
	/// Opens or creates the compile database (compile_commands.json) and shares it through the Share
	/// registry, so every Clang instance of the run, in this script and in its child scripts, appends
	/// to the same file. The file is saved by every Compile.
	/// </summary>
	/// <param name="file">The database file, absolute or relative to the current folder.</param>
	/// <returns>True when the database is open; false with IoError in LastError when the file cannot be read or created.</returns>
	public bool OpenCompileCommands(KValue file) {
		Begin();
		object? shared = Share.Get(CompileCommandsName);
		if (shared is JsonFile existing) {
			compdb = existing;
			compdbFile = (Share.Get(CompileCommandsName + ".file") as string) ?? string.Empty;
			return true;
		}
		try {
			JsonFile db = new JsonFile(file);
			if (db.Doc == null) {
				db.Doc = new JsonArray();
				if (!db.Save())
					return Fail(ErrorCode.IoError, "the compile database could not be created: " + file, "OpenCompileCommands", false);
			}
			compdb = db;
			compdbFile = RealPath(file);
			Share.Set(CompileCommandsName, db);
			Share.Set(CompileCommandsName + ".file", compdbFile);
			return true;
		} catch (Exception ex) {
			return Fail(ErrorCode.IoError, "the compile database could not be opened: " + file + " (" + ex.Message + ")", "OpenCompileCommands", false);
		}
	}

	/// <summary>
	/// The version of the C compiler of the options (CC --version).
	/// </summary>
	/// <returns>The version, fields -1 when the tool is missing or prints no version.</returns>
	public ToolVersionInfo Version() {
		Begin();
		ToolVersionInfo v = ToolVersion(Options.CC);
		if (!v.Available)
			Fail(ErrorCode.NotFound, "tool not found or no version printed: " + Options.CC + " (ClangOptions.CC)", "Version", false);
		return v;
	}

	/// <summary>
	/// True when the C compiler of the options is at least the given version.
	/// </summary>
	/// <param name="major">Major version required.</param>
	/// <param name="minor">Minor version required.</param>
	/// <param name="patch">Patch version required.</param>
	/// <returns>True when the version is enough; false with NotSupported (or NotFound when the tool is missing) in LastError.</returns>
	public bool VersionCheck(int major, int minor, int patch) {
		Begin();
		ToolVersionInfo v = ToolVersion(Options.CC);
		if (!v.Available)
			return Fail(ErrorCode.NotFound, "tool not found or no version printed: " + Options.CC + " (ClangOptions.CC)", "VersionCheck", false);
		bool enough = v.Major > major || (v.Major == major && (v.Minor > minor || (v.Minor == minor && v.Patch >= patch)));
		if (!enough)
			return Fail(ErrorCode.NotSupported, Options.CC + " " + v + " is older than the required " + major + "." + minor + "." + patch, "VersionCheck", false);
		return true;
	}

	/// <summary>
	/// The version of any tool, by name or path (tool --version). Static: it needs nothing of an
	/// instance.
	/// </summary>
	/// <param name="tool">The tool, a name on the PATH or a path.</param>
	/// <returns>The version, fields -1 when the tool is missing or prints no version.</returns>
	public static ToolVersionInfo ToolVersion(string tool) {
		ToolVersionInfo info = new ToolVersionInfo();
		if (FindTool(tool) == null)
			return info;
		Tool t = new Tool("clang");
		t.Timeout = 20000;
		ToolResult r = t.CommandSync(tool, "--version");
		string text = string.Concat(r.Stdout) + string.Concat(r.Stderr);
		// The first lines: llvm-ar prints its banner first and the version on the second line
		int lines = 0;
		foreach (string line in text.Split('\n')) {
			string l = line.Trim('\r', ' ', '\t');
			if (l.Length == 0)
				continue;
			if (info.Text.Length == 0)
				info.Text = l;
			Match m = VersionPattern.Match(l);
			if (m.Success) {
				info.Major = int.Parse(m.Groups[1].Value);
				info.Minor = int.Parse(m.Groups[2].Value);
				info.Patch = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
				break;
			}
			if (++lines >= 4)
				break;
		}
		return info;
	}

	// --------------------------------------------------------------------------------------------
	// The runner
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// One command of a batch and what it produced.
	/// </summary>
	private class Job {
		public string Cmd = string.Empty;
		public string Args = string.Empty;
		/// <summary>What the unit is called in the lines: the source, the file, the archive.</summary>
		public string Label = string.Empty;
		/// <summary>The ClangOptions property that names the tool, for the failure messages.</summary>
		public string Property = string.Empty;
		public CompileUnit? Unit = null;
		public bool Resource = false;
		public string Source = string.Empty;
		public string Output = string.Empty;
		public ToolResult? Result = null;
		/// <summary>Every line the tool printed, standard error first, endings removed.</summary>
		public List<string> Lines = new List<string>();
		/// <summary>Diagnostic lines of the extension itself, placed before the lines of the tool (the duplicate symbols of an archive).</summary>
		public List<string> Extra = new List<string>();
		/// <summary>The text the tool printed, as it came.</summary>
		public string Text = string.Empty;
		public int Warnings = 0;
		public int Errors = 0;
		public bool Failed = false;
		/// <summary>A failure without any error line: the tool itself did not run properly.</summary>
		public bool ToolFailure = false;
		public bool TimedOut = false;
		public bool Ran = false;
	}

	/// <summary>
	/// Runs the commands of a verb as one batch with the concurrency given, the timeout of the options
	/// and the output mode: the per unit lines in Detailed, the progress line in Progress, nothing in
	/// Silent. The completion callback of every command records its result, counts its diagnostics and,
	/// with the abort in effect, cancels the batch at the first failure; the reporting happens after the
	/// batch, on the script thread.
	/// </summary>
	private ToolResult RunBatch(string verb, string label, List<Job> jobs, int concurrency, bool abort, CompileResult? compile) {
		ClangOutput mode = Options.Output;
		if (jobs.Count == 0) {
			if (mode == ClangOutput.Progress)
				UpToDateLine(verb, label);
			return ToolResult.DefaultNoChanges();
		}
		Tool tool = new Tool("clang");
		tool.ConcurrentCommands = (uint)Math.Max(1, concurrency);
		tool.Timeout = Options.Timeout;
		ITaskProgress? progress = null;
		if (mode == ClangOutput.Progress) {
			progress = Options.Progress ?? Kltv.Kombine.Api.Progress.Default;
			progress.Start(label);
		}
		int done = 0;
		int warnings = 0;
		int failed = 0;
		bool cancelled = false;
		bool closed = false;
		object sync = new object();
		foreach (Job job in jobs) {
			Job current = job;
			Msg.Print("clang: " + current.Cmd + " " + current.Args, Msg.LogLevels.Verbose);
			tool.QueueCommand(current.Cmd, current.Args, current.Label, (ref ToolResult r) => {
				// The callbacks run one at a time under the lock of the tool; a result arriving after the
				// batch ended (a process killed by the cancellation that still reported) is ignored
				lock (sync) {
					if (closed)
						return;
					Collect(current, r, cancelled);
					done++;
					warnings += current.Warnings;
					if (current.Failed)
						failed++;
					if (current.Unit != null && current.Warnings > 0 && !current.Failed)
						r.Status = ToolStatus.Warnings;
					if (mode == ClangOutput.Detailed)
						UnitLine(verb, current);
					if (mode == ClangOutput.Progress && progress != null)
						progress.Report((double)done / jobs.Count, done + "/" + jobs.Count + (jobs.Count == 1 ? " file" : " files") + (warnings > 0 ? ", " + warnings + " warning" + (warnings == 1 ? "" : "s") : ""));
					if (current.Failed && abort && !cancelled) {
						cancelled = true;
						tool.CancelCommands();
					}
				}
			});
		}
		ToolResult res;
		try {
			res = tool.ExecuteCommands(true);
		} finally {
			lock (sync) {
				closed = true;
			}
			// The units the cancellation left behind
			foreach (Job job in jobs) {
				if (!job.Ran) {
					if (job.Unit != null)
						job.Unit.Status = UnitStatus.Skipped;
					if (!cancelled) {
						// Not cancelled and never ran: the tool could not be launched
						job.Failed = true;
						job.ToolFailure = true;
						if (job.Unit != null)
							job.Unit.Status = UnitStatus.Failed;
					}
				}
			}
			if (progress != null) {
				int errors = jobs.Sum(j => j.Errors);
				int failedJobs = jobs.Count(j => j.Failed);
				progress.Finish(ResultText(failedJobs, errors, warnings, jobs.Any(j => j.ToolFailure), false), failedJobs > 0 ? ProgressOutcome.Error : (warnings > 0 ? ProgressOutcome.Warning : ProgressOutcome.Success));
			}
		}
		if (compile != null)
			compile.Cancelled = cancelled;
		return res;
	}

	/// <summary>
	/// Records the result of one command into its job: the text, the lines, the counts and the status.
	/// </summary>
	private void Collect(Job job, ToolResult r, bool cancelled) {
		job.Ran = true;
		job.Result = r;
		job.Text = string.Concat(r.Stdout) + string.Concat(r.Stderr);
		job.Lines = job.Extra.Concat(Lines(r.Stderr)).Concat(Lines(r.Stdout)).ToList();
		job.TimedOut = job.Lines.Any(l => l.StartsWith("timeout after "));
		foreach (string line in job.Lines) {
			switch (Severity(line)) {
				case 1: job.Warnings++; break;
				case 2: job.Errors++; break;
			}
		}
		bool exitFailed = r.ExitCode != 0;
		job.Failed = exitFailed;
		if (exitFailed && job.Errors == 0 && !job.TimedOut) {
			// Killed by the cancellation of the batch: not a failure of its own
			if (cancelled) {
				job.Failed = false;
				job.Ran = false;
				return;
			}
			job.ToolFailure = true;
		}
		if (exitFailed && job.Lines.Any(l => l.Contains("invalid linker name") || l.Contains("unable to execute command")))
			job.ToolFailure = true;
		if (job.Unit != null) {
			job.Unit.ExitCode = r.ExitCode;
			job.Unit.Diagnostics = job.Lines;
			job.Unit.Warnings = job.Warnings;
			job.Unit.Errors = job.Errors;
			job.Unit.Status = job.Failed ? UnitStatus.Failed : (job.Warnings > 0 ? UnitStatus.Warnings : UnitStatus.Compiled);
		}
	}

	/// <summary>
	/// The task line of one unit in the Detailed mode: "Compiling x: Ok", as the previous extension.
	/// </summary>
	private static void UnitLine(string verb, Job job) {
		string task = verb == "Compile" ? "Compiling " : (verb == "Format" ? "Formatting " : (verb == "Linker" ? "Linking " : "Archiving "));
		Msg.PrintTask(task + job.Label + ":");
		if (job.Failed)
			Msg.PrintTaskError(" Failed");
		else if (job.Warnings > 0)
			Msg.PrintTaskWarning(" Warnings");
		else
			Msg.PrintTaskSuccess(" Ok");
	}

	/// <summary>
	/// The one line of a verb that had nothing to do: the progress line ended with "ok (up to date)" in
	/// Progress, the result line in Detailed, nothing in Silent.
	/// </summary>
	private void UpToDateLine(string verb, string label) {
		if (Options.Output == ClangOutput.Progress) {
			ITaskProgress progress = Options.Progress ?? Kltv.Kombine.Api.Progress.Default;
			progress.Start(label);
			progress.Finish("ok (up to date)", ProgressOutcome.Success);
		} else if (Options.Output == ClangOutput.Detailed) {
			Msg.PrintTask(verb + ": ");
			Msg.PrintTaskSuccess("ok (up to date)");
		}
	}

	/// <summary>
	/// The result text of a verb: ok, ok (up to date), ok (n warnings), failed (n errors), failed
	/// (tool failure), failed (timeout).
	/// </summary>
	private static string ResultText(int failed, int errors, int warnings, bool toolFailure, bool upToDate) {
		if (failed > 0) {
			if (errors > 0)
				return "failed (" + errors + " error" + (errors == 1 ? "" : "s") + ")";
			return toolFailure ? "failed (tool failure)" : "failed";
		}
		if (upToDate)
			return "ok (up to date)";
		if (warnings > 0)
			return "ok (" + warnings + " warning" + (warnings == 1 ? "" : "s") + ")";
		return "ok";
	}

	/// <summary>
	/// The report that follows a batch, in the Progress and Detailed modes: nothing when the build had no
	/// warning and no error; otherwise the diagnostics grouped per unit, the errors always, the warnings
	/// in full or as a count per ShowWarnings, each diagnostic through DiagnosticFilter. In Detailed the
	/// verb closes with its result line.
	/// </summary>
	private void Report(string verb, List<Job> jobs, int warnings, int errors, int failed, bool upToDate, bool toolFailure) {
		if (Options.Output == ClangOutput.Silent)
			return;
		// With ClangVerbose in Detailed, the text of the tools shows for every unit that printed some
		bool toolText = Options.ClangVerbose && Options.Output == ClangOutput.Detailed;
		foreach (Job job in jobs) {
			if (job.Warnings == 0 && job.Errors == 0 && !job.Failed && !(toolText && job.Lines.Count > 0))
				continue;
			PrintDiagnostics(job);
		}
		if (Options.Output == ClangOutput.Detailed) {
			Msg.PrintTask(verb + ": ");
			string text = ResultText(failed, errors, warnings, toolFailure, upToDate);
			if (failed > 0)
				Msg.PrintTaskError(text);
			else if (warnings > 0)
				Msg.PrintTaskWarning(text);
			else
				Msg.PrintTaskSuccess(text);
		}
	}

	/// <summary>
	/// The diagnostics of one unit: a header with the unit, then each diagnostic (its line and the
	/// excerpt that follows it) indented, printed as warning or error; the other lines the tool printed
	/// (a tool failure, a timeout, the -v text with ClangVerbose in Detailed) plain.
	/// </summary>
	private void PrintDiagnostics(Job job) {
		int hiddenWarnings = 0;
		List<string> lines = new List<string>();
		List<int> kinds = new List<int>();
		// Group the lines into diagnostics: a diagnostic line and the lines until the next one
		int i = 0;
		while (i < job.Lines.Count) {
			string head = job.Lines[i];
			int severity = Severity(head);
			int end = i + 1;
			while (end < job.Lines.Count && Severity(job.Lines[end]) == 0 && !IsDiagnostic(job.Lines[end]))
				end++;
			bool show = true;
			if (severity == 1 && !Options.ShowWarnings) {
				show = false;
				hiddenWarnings++;
			}
			if (show && severity != 0 && Options.DiagnosticFilter != null && !Options.DiagnosticFilter(job.Label, head))
				show = false;
			if (severity == 0 && !job.Failed && !job.TimedOut && !(Options.ClangVerbose && Options.Output == ClangOutput.Detailed))
				show = false;
			if (show) {
				for (int k = i; k < end; k++) {
					lines.Add(job.Lines[k]);
					kinds.Add(k == i ? severity : 0);
				}
			}
			i = end;
		}
		if (lines.Count == 0 && hiddenWarnings == 0)
			return;
		string header = job.Label + ":" + (hiddenWarnings > 0 ? " " + hiddenWarnings + " warning" + (hiddenWarnings == 1 ? "" : "s") : "");
		if (job.Failed && job.Errors > 0)
			Msg.PrintError(header);
		else if (job.Failed)
			Msg.PrintError(header + (job.TimedOut ? " timed out" : " tool failure, see below"));
		else if (job.Warnings > 0)
			Msg.PrintWarning(header);
		else
			Msg.Print(header);
		Msg.BeginIndent();
		for (int k = 0; k < lines.Count; k++) {
			if (kinds[k] == 2)
				Msg.PrintError(lines[k]);
			else if (kinds[k] == 1)
				Msg.PrintWarning(lines[k]);
			else
				Msg.Print(lines[k]);
		}
		Msg.EndIndent();
	}

	/// <summary>
	/// The message of LastError for a failed batch: the first error line of the first failed unit and
	/// the counts, or the hint of a tool failure.
	/// </summary>
	private string FailureMessage(List<Job> jobs) {
		int errors = jobs.Sum(j => j.Errors);
		int failed = jobs.Count(j => j.Failed);
		Job? first = jobs.FirstOrDefault(j => j.Failed);
		if (first == null)
			return "the batch was cancelled";
		if (first.TimedOut)
			return first.Label + ": timeout after " + Options.Timeout + " ms (" + failed + " failed)";
		if (first.ToolFailure) {
			string hint = first.Lines.Count > 0 ? first.Lines[0] : "no output";
			return first.Cmd + " failed without any diagnostic (exit code " + (first.Result?.ExitCode ?? -1) + "): check that '" + first.Cmd + "' (ClangOptions." + first.Property + ") is installed and on the PATH, and the length of the command line. " + hint;
		}
		string line = first.Lines.FirstOrDefault(l => Severity(l) == 2) ?? first.Label + ": failed";
		return line + " (" + errors + " error" + (errors == 1 ? "" : "s") + " in " + failed + " unit" + (failed == 1 ? "" : "s") + ")";
	}

	// --------------------------------------------------------------------------------------------
	// Diagnostics
	// --------------------------------------------------------------------------------------------

	/// <summary>file:line:column: severity: text, and file:line: severity: text, a drive letter allowed in front.</summary>
	private static readonly Regex DiagnosticGnu = new Regex(@"^(?:[A-Za-z]:)?[^:\r\n]+:\d+(?::\d+)?: (warning|error|fatal error|note): ", RegexOptions.Compiled);
	/// <summary>file(line,column): severity Cnnnn: text, as clang-cl prints them.</summary>
	private static readonly Regex DiagnosticCl = new Regex(@"^.+?\(\d+(?:,\d+)?\): (warning|error|fatal error)\b", RegexOptions.Compiled);
	/// <summary>tool: severity: text, as the driver, the linker and the archiver print them.</summary>
	private static readonly Regex DiagnosticTool = new Regex(@"^[A-Za-z0-9_.+\-]+: (warning|error|fatal error): ", RegexOptions.Compiled);
	/// <summary>The first major.minor[.patch] of a version line.</summary>
	private static readonly Regex VersionPattern = new Regex(@"(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.Compiled);

	/// <summary>
	/// 1 for a warning line, 2 for an error or fatal error line, 0 for anything else (notes, excerpts,
	/// carets, summaries, the tool text).
	/// </summary>
	private static int Severity(string line) {
		Match m = DiagnosticGnu.Match(line);
		if (!m.Success)
			m = DiagnosticCl.Match(line);
		if (!m.Success)
			m = DiagnosticTool.Match(line);
		if (!m.Success)
			return 0;
		string s = m.Groups[1].Value;
		if (s == "warning")
			return 1;
		if (s == "error" || s == "fatal error")
			return 2;
		return 0;
	}

	/// <summary>
	/// True for a diagnostic line of any severity, notes included, so a note starts its own group.
	/// </summary>
	private static bool IsDiagnostic(string line) {
		return DiagnosticGnu.IsMatch(line) || DiagnosticCl.IsMatch(line) || DiagnosticTool.IsMatch(line);
	}

	/// <summary>
	/// The fragments of an output as lines, endings removed, empty ones dropped.
	/// </summary>
	private static List<string> Lines(string[] fragments) {
		return string.Concat(fragments).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
	}

	// --------------------------------------------------------------------------------------------
	// Failures
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Starts a verb: the last failure and output are reset.
	/// </summary>
	private void Begin() {
		LastError = ApiError.None;
		LastOutput = string.Empty;
		states.Clear();
		compdbIndex = null;
	}

	/// <summary>
	/// Records a failure: LastError is set, the reason logged at verbose level, and the script aborted
	/// when the abort is in effect. Always returns false so a verb can "return Fail(...)".
	/// </summary>
	private bool Fail(ErrorCode code, string message, string source, bool abort) {
		LastError = new ApiError(code, message, source);
		Msg.PrintWarning("clang: " + LastError.ToString(), Msg.LogLevels.Verbose);
		if (abort)
			Msg.PrintAndAbort("clang " + source + " failed (" + code + "): " + message);
		return false;
	}

	/// <summary>
	/// Records a failure and returns the failed ToolResult of the verb (the one given, or a default).
	/// </summary>
	private ToolResult FailResult(ErrorCode code, string message, string source, bool abort, ToolResult? result = null) {
		Fail(code, message, source, abort);
		if (result != null && result.Status != ToolStatus.Failed)
			result.Status = ToolStatus.Failed;
		return result ?? ToolResult.DefaultFailed();
	}

	// --------------------------------------------------------------------------------------------
	// Command lines and tools
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// Quotes an argument that contains spaces.
	/// </summary>
	private static string Q(string arg) {
		if (arg.Length == 0)
			return arg;
		if (arg.Contains(' ') && !arg.StartsWith("\""))
			return "\"" + arg + "\"";
		return arg;
	}

	/// <summary>
	/// Joins arguments with one space, skipping the empty ones.
	/// </summary>
	private static string Join(IEnumerable<string> parts) {
		return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
	}

	/// <summary>
	/// The extra arguments of a unit from the ProcessFile delegate.
	/// </summary>
	private string Extra(string file) {
		return ProcessFile != null ? (ProcessFile.Invoke(file) ?? string.Empty) : string.Empty;
	}

	/// <summary>
	/// The listing of one option in the Verbose mode: a header and the values indented.
	/// </summary>
	private static void Listing(string title, KList values) {
		Msg.Print(title);
		Msg.BeginIndent();
		foreach (KValue v in values)
			Msg.Print(v);
		Msg.EndIndent();
	}

	/// <summary>
	/// True when the file has one of the extensions of a list ("a;b"), the case ignored on Windows and macOS.
	/// </summary>
	private static bool HasExtension(string file, string extensions) {
		StringComparison c = Host.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		foreach (string e in extensions.Split(';')) {
			string ext = e.Trim();
			if (ext.Length > 0 && file.EndsWith(ext, c))
				return true;
		}
		return false;
	}

	/// <summary>
	/// The switch that names the linker to the driver: -fuse-ld=name, --ld-path=path, or nothing.
	/// </summary>
	private string LinkerSwitch() {
		string ld = Options.LD.Trim();
		if (ld.Length == 0)
			return string.Empty;
		if (ld.Contains('/') || ld.Contains('\\') || File.Exists(ld))
			return "--ld-path=" + Q(RealPath(ld));
		return "-fuse-ld=" + ld;
	}

	/// <summary>
	/// A path with forward slashes, the form the rest of the engine uses.
	/// </summary>
	private static KValue Unix(KValue path) {
		return ((string)path).Replace('\\', '/');
	}

	/// <summary>
	/// The symbols defined by more than one of the objects, as diagnostic lines ("librarian: severity:
	/// duplicate symbol X defined in a.obj and b.obj"), read through ReadObj in one invocation. Only
	/// the strong external definitions count: weak and common symbols, the COMDAT sections of COFF
	/// objects (inline functions, templates), the weak definitions of Mach-O objects and the weak
	/// bindings of ELF objects are left out. Null when the reader is not available, with the reason in
	/// note.
	/// </summary>
	private List<string>? DuplicateSymbols(List<string> objects, string severity, out string note) {
		note = string.Empty;
		if (FindTool(Options.ReadObj) == null) {
			note = "duplicate symbol check skipped: " + Options.ReadObj + " not found (ClangOptions.ReadObj)";
			return null;
		}
		string responsefile = Path.Combine(Path.GetTempPath(), "kombine-" + Guid.NewGuid().ToString("N") + ".rsp");
		ToolResult r;
		try {
			File.WriteAllLines(responsefile, objects.Select(o => Q(o.Replace("\\", "/"))));
			Tool tool = new Tool("clang");
			tool.Timeout = Options.Timeout;
			Msg.Print("clang: " + Options.ReadObj + " --symbols @" + responsefile, Msg.LogLevels.Verbose);
			r = tool.CommandSync(Options.ReadObj, "--symbols @" + Q(responsefile));
		} finally {
			try {
				File.Delete(responsefile);
			} catch {
			}
		}
		if (r.ExitCode != 0) {
			note = "duplicate symbol check skipped: " + Options.ReadObj + " failed (" + (Lines(r.Stderr).FirstOrDefault() ?? "exit code " + r.ExitCode) + ")";
			return null;
		}
		// One pass over the text: a "File:" header per object, a "Format:" line, then the symbol blocks
		Dictionary<string, string> firstDefinition = new Dictionary<string, string>(StringComparer.Ordinal);
		List<string> duplicates = new List<string>();
		string file = string.Empty;
		string format = string.Empty;
		HashSet<int> comdat = new HashSet<int>();
		List<(string name, int section)> coffCandidates = new List<(string, int)>();
		List<string> defined = new List<string>();
		// The fields of the symbol block being read
		bool inSymbol = false;
		string name = string.Empty;
		int section = 0;
		string sectionName = string.Empty;
		string storage = string.Empty;
		string binding = string.Empty;
		string type = string.Empty;
		bool external = false;
		bool weak = false;
		int selection = 0;
		void CloseFile() {
			// COFF: the COMDAT sections are known once the whole table was read
			foreach ((string n, int s) in coffCandidates)
				if (!comdat.Contains(s))
					defined.Add(n);
			Msg.Print("clang: symbols of " + file + " (" + format + "): " + defined.Count + " defined, " + coffCandidates.Count + " external, " + comdat.Count + " comdat sections", Msg.LogLevels.Verbose);
			foreach (string n in defined) {
				if (firstDefinition.TryGetValue(n, out string? first)) {
					if (first != file)
						duplicates.Add("librarian: " + severity + ": duplicate symbol " + n + " defined in " + first + " and " + file);
				} else {
					firstDefinition[n] = file;
				}
			}
			coffCandidates.Clear();
			comdat.Clear();
			defined.Clear();
		}
		foreach (string raw in Lines(r.Stdout)) {
			string line = raw.TrimEnd();
			if (line.StartsWith("File: ")) {
				if (file.Length > 0)
					CloseFile();
				file = line.Substring(6).Trim();
				format = string.Empty;
				continue;
			}
			if (line.StartsWith("Format: ")) {
				format = line.Substring(8).Trim();
				continue;
			}
			string t = line.Trim();
			if (t == "Symbol {") {
				inSymbol = true;
				name = string.Empty; section = 0; sectionName = string.Empty; storage = string.Empty; binding = string.Empty; type = string.Empty;
				external = false; weak = false; selection = 0;
				continue;
			}
			if (!inSymbol)
				continue;
			if (t == "}" && line.StartsWith("  }")) {
				inSymbol = false;
				if (format.StartsWith("COFF")) {
					if (storage.StartsWith("Static") && selection != 0)
						comdat.Add(section);
					else if (storage.StartsWith("External") && section > 0)
						coffCandidates.Add((name, section));
				} else if (format.StartsWith("elf") || format.StartsWith("ELF")) {
					if (binding.StartsWith("Global") && section > 0 && section != 0xFFF2 && !type.StartsWith("Section") && !type.StartsWith("File"))
						defined.Add(name);
				} else if (format.StartsWith("Mach-O")) {
					if (external && !weak && type.StartsWith("Section") && sectionName != "__common")
						defined.Add(name);
				}
				continue;
			}
			if (t.StartsWith("Name: ")) {
				name = t.Substring(6);
				int paren = name.LastIndexOf(" (");
				if (paren > 0 && name.EndsWith(")"))
					name = name.Substring(0, paren);
			} else if (t.StartsWith("Section: ")) {
				string s = t.Substring(9);
				int paren = s.LastIndexOf('(');
				sectionName = paren > 0 ? s.Substring(0, paren).Trim() : s.Trim();
				string num = paren > 0 ? s.Substring(paren + 1).TrimEnd(')') : string.Empty;
				section = ParseNumber(num);
			} else if (t.StartsWith("StorageClass: ")) {
				storage = t.Substring(14);
			} else if (t.StartsWith("Binding: ")) {
				binding = t.Substring(9);
			} else if (t.StartsWith("Type: ")) {
				type = t.Substring(6);
			} else if (t == "Extern") {
				external = true;
			} else if (t.StartsWith("WeakDef")) {
				weak = true;
			} else if (t.StartsWith("Selection: ")) {
				// "Selection: Any (0x2)" for a COMDAT section, "Selection: 0x0" for a plain one
				string v = t.Substring(11).Trim();
				int paren = v.LastIndexOf('(');
				selection = ParseNumber(paren >= 0 ? v.Substring(paren + 1).TrimEnd(')') : v);
			}
		}
		if (file.Length > 0)
			CloseFile();
		return duplicates;
	}

	/// <summary>
	/// A number as readobj prints it: decimal, or hexadecimal with 0x; -1 for anything else.
	/// </summary>
	private static int ParseNumber(string text) {
		string s = text.Trim();
		try {
			if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				return Convert.ToInt32(s.Substring(2), 16);
			return int.Parse(s);
		} catch {
			return -1;
		}
	}

	/// <summary>
	/// The libraries of the options that exist as files: a path as it is, a name searched in every
	/// library path as libname.a, name.lib, libname.so, libname.dylib, name.dll, libname.dll.a and
	/// name.a. A library found nowhere (a system library) is not an input.
	/// </summary>
	private List<string> LibraryFiles() {
		List<string> files = new List<string>();
		foreach (KValue lib in Options.Libraries) {
			string name = lib;
			if (Path.IsPathRooted(name)) {
				if (File.Exists(name))
					files.Add(Path.GetFullPath(name));
				continue;
			}
			string[] candidates = { "lib" + name + ".a", name + ".lib", "lib" + name + ".so", "lib" + name + ".dylib", name + ".dll", "lib" + name + ".dll.a", name + ".a", "lib" + name + ".lib" };
			foreach (KValue dir in Options.LibraryDirs) {
				foreach (string c in candidates) {
					string candidate = Path.Combine(RealPath(dir), c);
					if (File.Exists(candidate)) {
						files.Add(Path.GetFullPath(candidate));
						break;
					}
				}
			}
		}
		return files.Distinct().ToList();
	}

	/// <summary>The tools found, by name, once per run.</summary>
	private static readonly Dictionary<string, string?> FoundTools = new Dictionary<string, string?>();

	/// <summary>
	/// The full path of a tool: a path is checked as it is, a name searched on the PATH (with the
	/// extensions of PATHEXT on Windows). Null when it is not found.
	/// </summary>
	private static string? FindTool(string tool) {
		lock (FoundTools) {
			if (FoundTools.TryGetValue(tool, out string? known))
				return known;
		}
		string? found = null;
		if (tool.Contains('/') || tool.Contains('\\')) {
			if (File.Exists(tool))
				found = Path.GetFullPath(tool);
		} else {
			List<string> extensions = new List<string> { string.Empty };
			if (Host.IsWindows()) {
				string pathext = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT";
				extensions.AddRange(pathext.Split(';').Where(e => e.Length > 0));
			}
			string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
			foreach (string dir in path.Split(Path.PathSeparator)) {
				if (dir.Length == 0)
					continue;
				foreach (string ext in extensions) {
					string candidate = Path.Combine(dir.Trim('"'), tool + ext);
					if (File.Exists(candidate)) {
						found = candidate;
						break;
					}
				}
				if (found != null)
					break;
			}
		}
		lock (FoundTools) {
			FoundTools[tool] = found;
		}
		return found;
	}

	// --------------------------------------------------------------------------------------------
	// Up to date checks
	// --------------------------------------------------------------------------------------------

	/// <summary>
	/// The dependency file of an object, written by the compiler with -MD -MF.
	/// </summary>
	private static string DepFile(string obj) {
		return Path.ChangeExtension(obj, ".d");
	}

	/// <summary>
	/// The record of an object: what it was built from, next to it.
	/// </summary>
	private static string RecordFile(string output) {
		return output + ".kdep";
	}

	/// <summary>
	/// The record of an archive or an executable: in the folder of its first object, named after the
	/// output, so the output folder holds nothing but what is shipped.
	/// </summary>
	private static string LinkRecordFile(string output, List<string> inputs) {
		if (inputs.Count == 0)
			return RecordFile(output);
		return Path.Combine(Path.GetDirectoryName(inputs[0]) ?? string.Empty, Path.GetFileName(output) + ".kdep");
	}

	/// <summary>
	/// The inputs of a compiled unit: the source and the dependencies of the dependency file the
	/// compiler wrote (or, for a resource, the files the resource script names).
	/// </summary>
	private List<string> DepInputs(string source, string obj, bool resource) {
		List<string> inputs = new List<string> { source };
		if (resource) {
			inputs.AddRange(ResourceInputs(source));
		} else {
			string dep = DepFile(obj);
			if (File.Exists(dep))
				inputs.AddRange(ParseDepFile(File.ReadAllText(dep)));
		}
		return inputs.Select(i => Path.GetFullPath(i)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	/// <summary>The first line of a record, with the version of its layout.</summary>
	private const string RecordHeader = "kombine-record 2";

	/// <summary>
	/// Decides whether an output is up to date from its record: the command line must be the same and
	/// every recorded input must exist and be unchanged. An input whose date and size are those of
	/// the record counts as unchanged without being read; one whose date or size moved is read and
	/// its content hash compared, so a file touched without an edit builds nothing. Every file is
	/// looked at once per verb call, whatever the number of units that include it. A unit also needs
	/// its dependency file. An output without record but present is checked the way the previous
	/// extension did (the dates of the source and its dependency file against the object) and gets
	/// its record when it passes, so an upgrade does not rebuild everything once.
	/// </summary>
	/// <param name="output">The object, archive or executable.</param>
	/// <param name="command">Its command line.</param>
	/// <param name="source">The source of a unit, null for a link or an archive.</param>
	/// <param name="inputs">The inputs of a link or an archive, or of a resource unit; null for a unit with a dependency file.</param>
	private bool UpToDate(string output, string command, string? source, List<string>? inputs) {
		if (!File.Exists(output)) {
			Msg.Print("clang: " + output + " does not exist, it will be built", Msg.LogLevels.Verbose);
			return false;
		}
		if (source != null && inputs == null && !File.Exists(DepFile(output))) {
			Msg.Print("clang: " + DepFile(output) + " is missing, " + output + " will be built", Msg.LogLevels.Verbose);
			return false;
		}
		string commandHash = Hash(Encoding.UTF8.GetBytes(command));
		string recordFile = source == null && inputs != null ? LinkRecordFile(output, inputs) : RecordFile(output);
		string[] lines;
		try {
			lines = File.ReadAllLines(recordFile);
		} catch (FileNotFoundException) {
			// No record yet: the check of the previous extension, then the record is written
			List<string> legacyInputs = inputs != null ? new List<string>(inputs) : new List<string>();
			if (source != null) {
				legacyInputs.Insert(0, source);
				string dep = DepFile(output);
				if (inputs == null && File.Exists(dep))
					legacyInputs.AddRange(ParseDepFile(File.ReadAllText(dep)));
			}
			long outputTime = Files.GetModifiedTime(output);
			foreach (string i in legacyInputs) {
				FileState? state = StateOf(i);
				if (state == null) {
					Msg.Print("clang: " + i + " is missing, " + output + " will be built", Msg.LogLevels.Verbose);
					return false;
				}
				if (Files.GetModifiedTime(i) > outputTime) {
					Msg.Print("clang: " + i + " is newer than " + output + ", it will be built", Msg.LogLevels.Verbose);
					return false;
				}
			}
			Record(recordFile, command, legacyInputs);
			return true;
		} catch (Exception ex) {
			Msg.Print("clang: the record of " + output + " cannot be read (" + ex.Message + "), it will be built", Msg.LogLevels.Verbose);
			return false;
		}
		if (lines.Length < 2 || lines[0] != RecordHeader || !lines[1].StartsWith("command ")) {
			Msg.Print("clang: the record of " + output + " is of another layout, it will be built", Msg.LogLevels.Verbose);
			return false;
		}
		if (lines[1].Substring(8) != commandHash) {
			Msg.Print("clang: the command of " + output + " changed, it will be built", Msg.LogLevels.Verbose);
			return false;
		}
		bool refresh = false;
		for (int l = 2; l < lines.Length; l++) {
			string line = lines[l];
			if (line.Length == 0)
				continue;
			// hash, date, size and path, separated by tabs
			string[] fields = line.Split('\t', 4);
			if (fields.Length != 4) {
				Msg.Print("clang: the record of " + output + " is unreadable, it will be built", Msg.LogLevels.Verbose);
				return false;
			}
			string path = fields[3];
			FileState? state = StateOf(path);
			if (state == null) {
				Msg.Print("clang: " + path + " is missing, " + output + " will be built", Msg.LogLevels.Verbose);
				return false;
			}
			if (long.TryParse(fields[1], out long ticks) && long.TryParse(fields[2], out long size) && ticks == state.Ticks && size == state.Size)
				continue;
			// The date or the size moved: the content decides
			if (HashOf(path, state) != fields[0]) {
				Msg.Print("clang: " + path + " changed, " + output + " will be built", Msg.LogLevels.Verbose);
				return false;
			}
			lines[l] = fields[0] + "\t" + state.Ticks + "\t" + state.Size + "\t" + path;
			refresh = true;
		}
		if (refresh) {
			// The moved dates are recorded so the next check does not read those files again
			try {
				File.WriteAllLines(recordFile, lines);
			} catch (Exception ex) {
				Msg.PrintWarning("clang: record not refreshed: " + recordFile + " (" + ex.Message + ")", Msg.LogLevels.Verbose);
			}
		}
		return true;
	}

	/// <summary>
	/// Writes the record of an output: a header, the hash of its command line, and one line per input
	/// with its content hash, date, size and path, separated by tabs.
	/// </summary>
	private void Record(string recordFile, string command, List<string> inputs) {
		List<string> lines = new List<string> { RecordHeader, "command " + Hash(Encoding.UTF8.GetBytes(command)) };
		foreach (string i in inputs.Distinct(StringComparer.OrdinalIgnoreCase)) {
			string path = Path.IsPathRooted(i) ? i : Path.GetFullPath(i);
			FileState? state = StateOf(path);
			if (state == null)
				continue;
			lines.Add(HashOf(path, state) + "\t" + state.Ticks + "\t" + state.Size + "\t" + path);
		}
		try {
			File.WriteAllLines(recordFile, lines);
		} catch (Exception ex) {
			Msg.PrintWarning("clang: record not written: " + recordFile + " (" + ex.Message + ")", Msg.LogLevels.Verbose);
		}
	}

	/// <summary>
	/// Removes the record of an output that failed to build, so the next call builds it again.
	/// </summary>
	private static void DeleteRecord(string recordFile) {
		try {
			if (File.Exists(recordFile))
				File.Delete(recordFile);
		} catch {
		}
	}

	/// <summary>
	/// What is known of an input during one verb call: its date and size from one look at the file
	/// system, and its content hash once it was read.
	/// </summary>
	private class FileState {
		public long Ticks;
		public long Size;
		public string? Hash;
	}

	/// <summary>
	/// The states of the inputs seen during the current verb call, so a header included by many units
	/// is looked at once and read at most once. Cleared by every verb.
	/// </summary>
	private readonly Dictionary<string, FileState?> states = new Dictionary<string, FileState?>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The state of a file, null when it is missing.
	/// </summary>
	private FileState? StateOf(string path) {
		if (states.TryGetValue(path, out FileState? known))
			return known;
		FileState? state = null;
		FileInfo fi = new FileInfo(path);
		if (fi.Exists)
			state = new FileState { Ticks = fi.LastWriteTimeUtc.Ticks, Size = fi.Length };
		states[path] = state;
		return state;
	}

	/// <summary>
	/// The content hash of a file, read once per verb call.
	/// </summary>
	private string HashOf(string path, FileState state) {
		if (state.Hash == null) {
			using (FileStream s = File.OpenRead(path)) {
				state.Hash = Convert.ToHexString(SHA256.HashData(s));
			}
		}
		return state.Hash;
	}

	/// <summary>
	/// The hash of a text.
	/// </summary>
	private static string Hash(byte[] data) {
		return Convert.ToHexString(SHA256.HashData(data));
	}

	/// <summary>
	/// Parses a dependency file in the make syntax the compiler writes: a backslash before a newline
	/// continues the line, "\ " is a space inside a path, "$$" a dollar, "\#" a hash, any other
	/// backslash a directory separator of a Windows path; the target ends at the first ":" token.
	/// </summary>
	internal static List<string> ParseDepFile(string text) {
		List<string> tokens = new List<string>();
		StringBuilder token = new StringBuilder();
		int i = 0;
		while (i < text.Length) {
			char c = text[i];
			if (c == '\\' && i + 1 < text.Length) {
				char n = text[i + 1];
				if (n == '\r' || n == '\n') {
					// Continuation: the newline is whitespace
					i += 2;
					if (n == '\r' && i < text.Length && text[i] == '\n')
						i++;
					Flush(tokens, token);
					continue;
				}
				if (n == ' ' || n == '#') {
					token.Append(n);
					i += 2;
					continue;
				}
				token.Append(c);
				i++;
				continue;
			}
			if (c == '$' && i + 1 < text.Length && text[i + 1] == '$') {
				token.Append('$');
				i += 2;
				continue;
			}
			if (c == ' ' || c == '\t' || c == '\r' || c == '\n') {
				Flush(tokens, token);
				i++;
				continue;
			}
			token.Append(c);
			i++;
		}
		Flush(tokens, token);
		// The target: everything up to the first token ending with ":" (a Windows drive letter is not the end)
		int start = 0;
		for (int t = 0; t < tokens.Count; t++) {
			string tk = tokens[t];
			if (tk == ":" || (tk.EndsWith(":") && !(tk.Length == 2 && char.IsLetter(tk[0])))) {
				start = t + 1;
				break;
			}
		}
		return tokens.Skip(start).ToList();
	}

	private static void Flush(List<string> tokens, StringBuilder token) {
		if (token.Length > 0) {
			tokens.Add(token.ToString());
			token.Clear();
		}
	}

	/// <summary>
	/// The files a resource script names: its #include lines and every quoted name that exists as a
	/// file next to the script or in an include directory.
	/// </summary>
	private List<string> ResourceInputs(string rc) {
		List<string> inputs = new List<string>();
		if (!File.Exists(rc))
			return inputs;
		string folder = Path.GetDirectoryName(rc) ?? string.Empty;
		List<string> dirs = new List<string> { folder };
		dirs.AddRange(Options.IncludeDirs.Select(d => (string)RealPath(d)));
		foreach (string raw in File.ReadAllLines(rc)) {
			string line = raw.Trim();
			if (line.StartsWith("//"))
				continue;
			foreach (Match m in ResourceName.Matches(line)) {
				string name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
				foreach (string d in dirs) {
					string candidate = Path.Combine(d, name.Replace("\\\\", "\\"));
					if (File.Exists(candidate)) {
						inputs.Add(Path.GetFullPath(candidate));
						break;
					}
				}
			}
		}
		return inputs;
	}

	/// <summary>A quoted name or an angle bracket include of a resource script.</summary>
	private static readonly Regex ResourceName = new Regex("\"([^\"]+)\"|#include\\s*<([^>]+)>", RegexOptions.Compiled);

	// --------------------------------------------------------------------------------------------
	// Shared objects
	// --------------------------------------------------------------------------------------------

	/// <summary>The compile database of the run, shared through the registry.</summary>
	private JsonFile? compdb = null;

	/// <summary>The file of the compile database, shared next to it.</summary>
	private string compdbFile = string.Empty;

	/// <summary>The name the compile database is shared under.</summary>
	private const string CompileCommandsName = "compile_commands";

	/// <summary>
	/// Takes a copy of the options set as default by this script or a parent script, if any.
	/// </summary>
	private void OpenSharedCompileOptions() {
		object? obj = Share.Get(ClangOptions.SharedName);
		if (obj != null)
			Options.CopyFrom(obj);
	}

	/// <summary>
	/// Takes the compile database opened by a previous instance of the run, if any.
	/// </summary>
	private void OpenSharedCompileCommands() {
		if (Share.Get(CompileCommandsName) is JsonFile db) {
			compdb = db;
			compdbFile = (Share.Get(CompileCommandsName + ".file") as string) ?? string.Empty;
		}
	}

	/// <summary>
	/// Adds or updates the entry of a unit in the compile database.
	/// </summary>
	private void AddCompileCommands(string cmd, string file, string outputfile) {
		if (compdb == null)
			return;
		if (compdb.Doc == null)
			compdb.Doc = new JsonArray();
		string directory = Folders.GetCurrentFolder();
		// The entries indexed once per call by folder and file, instead of a scan of the whole
		// database for every unit
		if (compdbIndex == null) {
			compdbIndex = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
			foreach (JsonNode? node in compdb.Doc.AsArray()) {
				if (node == null)
					continue;
				string key = (node["directory"]?.ToString() ?? string.Empty) + "\n" + (node["file"]?.ToString() ?? string.Empty);
				compdbIndex[key] = node;
			}
		}
		if (compdbIndex.TryGetValue(directory + "\n" + file, out JsonNode? existing)) {
			existing["command"] = JsonValue.Create(cmd);
			existing["output"] = JsonValue.Create(outputfile);
			return;
		}
		JsonObject entry = new JsonObject();
		entry.Add("directory", JsonValue.Create(directory));
		entry.Add("command", JsonValue.Create(cmd));
		entry.Add("file", JsonValue.Create(file));
		entry.Add("output", JsonValue.Create(outputfile));
		compdb.Doc.AsArray().Add(entry);
		compdbIndex[directory + "\n" + file] = entry;
	}

	/// <summary>The entries of the compile database by folder and file, built once per verb call.</summary>
	private Dictionary<string, JsonNode>? compdbIndex = null;
}
