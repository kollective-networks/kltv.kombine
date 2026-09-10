/*---------------------------------------------------------------------------------------------------------

	Kombine Clang Extension Example

	(C)Kollective Networks 2026

	Test of the clang extension (extensions/clang.csx), by groups. Every group checks what it needs
	first and skips itself with a visible line when the environment cannot satisfy it:

		[1/8] Tools                    clang, clang++, llvm-ar (and llvm-rc on Windows, clang-format for the format checks)
		[2/8] Output modes             the same build run three times, one per mode, shown on screen
		[3/8] Every verb once          compile, librarian, linker (executable and shared library), format, clean,
		                               compile database, status, a second instance with its own options
		[4/8] Failures                 an error unit, the abort through a child script, missing files, a bad tool,
		                               a timeout, a linker error
		[5/8] Nothing left behind      every change that must build something builds exactly that
		[6/8] Nothing built without need  every change that must build nothing builds nothing
		[7/8] Child script             the status counters and the default options cross to a child script
		[8/8] Batch concurrency        the engine runs exactly ConcurrentCommands commands at a time

	The sources are written into a sandbox (.tmp.clang) by the example itself and removed at the end.
	Every check prints one aligned line with an OK or FAILED tag. Groups 5 and 6 verify each claim two
	ways: the counts of LastCompile and the dates of the objects, the archive and the executables.

		mkb test     runs every group
		mkb clean    removes the sandbox
		mkb build    builds the three sub projects (lib, exe, dll) the way a build script does
		mkb cleanall removes the sandbox and the output of the sub projects

---------------------------------------------------------------------------------------------------------*/

#load "extensions/clang.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
// The results of the checks, for the examples runner
#load "mkb.results.csx"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

int passed = 0;
int failed = 0;
int skipped = 0;
string Sandbox = Path.GetFullPath(Path.Combine(CurrentWorkingFolder, ".tmp.clang"));
string Src = string.Empty;
string Inc = string.Empty;
string IncSpaced = string.Empty;
string Sys = string.Empty;
string Obj = string.Empty;
string Lib = string.Empty;
string Bin = string.Empty;
string Res = string.Empty;
string ObjExt = new Clang().Options.ObjectExtension;
string LibExt = new Clang().Options.LibExtension;
string BinExt = new Clang().Options.BinaryExtension;
bool HasFormat = false;

// The output folders of the sub projects, exported so the child scripts build into one tree
KValue OutputBin = CurrentWorkingFolder + "/out/bin/";
KValue OutputTmp = CurrentWorkingFolder + "/out/tmp/";
KValue OutputLib = CurrentWorkingFolder + "/out/lib/";
OutputBin.Export("OutputBin");
OutputTmp.Export("OutputTmp");
OutputLib.Export("OutputLib");

/// <summary>
/// Runs every group.
/// </summary>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing the clang extension");
	Msg.BeginIndent();
	Clang.Status.Reset();
	Fixtures();
	bool ready = TestTools();
	if (ready){
		TestOutputModes();
		TestVerbs();
		TestFailures();
		TestNothingLeftBehind();
		TestNothingWithoutNeed();
		TestChildScript();
		TestBatchLimit();
	}
	int total = passed + failed;
	TestSummary(passed, failed, skipped);
	Msg.EndIndent();
	Msg.EndIndent();
	Nuke(Sandbox);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failed != 0){
		Msg.PrintError($"clang extension tests failed: {failed} of {total}");
		return 1;
	}
	return 0;
}

/// <summary>
/// Removes the sandbox.
/// </summary>
int clean(string[] args){
	Nuke(Sandbox);
	return 0;
}

/// <summary>
/// Builds the three sub projects the way a build script does (a static library, an executable that
/// links it, a shared library), each in its own child script.
/// </summary>
int build(string[] args){
	Clang clang = new Clang();
	clang.OpenCompileCommands("out/tmp/compile_commands.json");
	if (Kombine("lib/mylib.csx", "build", args) != 0)
		return 1;
	if (Kombine("exe/mybin.csx", "build", args) != 0)
		return 1;
	if (Kombine("dll/mydll.csx", "build", args) != 0)
		return 1;
	return 0;
}

/// <summary>
/// Removes the sandbox and the output of the sub projects.
/// </summary>
int cleanall(string[] args){
	Nuke(Sandbox);
	Kombine("lib/mylib.csx", "clean", args);
	Kombine("exe/mybin.csx", "clean", args);
	Kombine("dll/mydll.csx", "clean", args);
	Nuke(Path.Combine(CurrentWorkingFolder, "out"));
	return 0;
}

// ------------------------------------------------------------------------------------------------
// [1/8] Tools
// ------------------------------------------------------------------------------------------------

bool TestTools(){
	Banner("[1/8] Tools");
	Clang clang = Silent();
	ToolVersionInfo v = clang.Version();
	Check("Version", v.Available ? "available" : "not available", "available");
	if (!v.Available){
		EndBanner();
		Skip("[2/8] to [7/8]", "clang is not available: " + clang.LastError.Message);
		return false;
	}
	Check("VersionCheck same", Show(clang.VersionCheck(v.Major, v.Minor, v.Patch)), "true");
	Check("VersionCheck 999", Show(clang.VersionCheck(999, 0, 0)), "false");
	Check("error code", clang.LastError.Code.ToString(), "NotSupported");
	Check("ToolVersion archiver", Clang.ToolVersion(clang.Options.AR).Available ? "available" : "not available", "available");
	Check("ToolVersion missing tool", Clang.ToolVersion("no-such-tool-here").Available ? "available" : "not available", "not available");
	HasFormat = Clang.ToolVersion("clang-format").Available;
	Check("ToolVersion clang-format", HasFormat ? "available" : "not available (format checks skipped)", HasFormat ? "available" : "not available (format checks skipped)");
	clang.Options.CC = "no-such-compiler";
	ToolResult r = clang.Compile(Sources("a.c"), Objects("a.c"), false);
	Check("wrong tool name", clang.LastError.Code.ToString(), "NotFound");
	Check("names the option", clang.LastError.Message.Contains("ClangOptions.CC") ? "ClangOptions.CC named" : clang.LastError.Message, "ClangOptions.CC named");
	Check("failed result", r.Status.ToString(), "Failed");
	EndBanner();
	return true;
}

// ------------------------------------------------------------------------------------------------
// The fixtures: a small tree with three units sharing headers in every way the checks need
// ------------------------------------------------------------------------------------------------

void Fixtures(){
	Nuke(Sandbox);
	Src = Path.Combine(Sandbox, "src");
	Inc = Path.Combine(Sandbox, "inc");
	IncSpaced = Path.Combine(Sandbox, "inc dir");
	Sys = Path.Combine(Sandbox, "sys");
	Obj = Path.Combine(Sandbox, "obj");
	Lib = Path.Combine(Sandbox, "lib");
	Bin = Path.Combine(Sandbox, "bin");
	Res = Path.Combine(Sandbox, "res");
	foreach (string d in new[] { Src, Inc, IncSpaced, Sys, Obj, Lib, Bin, Res })
		Directory.CreateDirectory(d);
	WriteFixtures();
}

/// <summary>
/// Writes (or restores) every source and header of the sandbox.
/// </summary>
void WriteFixtures(){
	File.WriteAllText(Path.Combine(Inc, "shared.h"), "#pragma once\n#define SHARED_VALUE 1\n");
	File.WriteAllText(Path.Combine(Inc, "chain.h"), "#pragma once\n#include \"inner.h\"\n");
	File.WriteAllText(Path.Combine(Inc, "inner.h"), "#pragma once\n#define INNER_VALUE 1\n");
	File.WriteAllText(Path.Combine(Inc, "only.h"), "#pragma once\n#define ONLY_VALUE 1\n");
	File.WriteAllText(Path.Combine(Inc, "unused.h"), "#pragma once\n#define UNUSED_VALUE 1\n");
	File.WriteAllText(Path.Combine(IncSpaced, "spaced.h"), "#pragma once\n#define SPACED_VALUE 1\n");
	File.WriteAllText(Path.Combine(Sys, "sysheader.h"), "#pragma once\n#define SYS_VALUE 1\n");
	File.WriteAllText(Path.Combine(Src, "a.c"), "#include \"shared.h\"\n#include \"spaced.h\"\nint a(void) { return SHARED_VALUE + SPACED_VALUE; }\n");
	File.WriteAllText(Path.Combine(Src, "b.cpp"), "#include \"shared.h\"\n#include \"chain.h\"\n#include <sysheader.h>\nextern \"C\" int b(void) { return SHARED_VALUE + INNER_VALUE + SYS_VALUE; }\n");
	File.WriteAllText(Path.Combine(Src, "c.c"), "#include \"only.h\"\nint c(void) { return ONLY_VALUE; }\n");
	File.WriteAllText(Path.Combine(Src, "main1.cpp"), "extern \"C\" int a(void);\nextern \"C\" int b(void);\nint main() { return a() + b(); }\n");
	File.WriteAllText(Path.Combine(Src, "main2.c"), "int c(void);\nint main(void) { return c(); }\n");
	File.WriteAllText(Path.Combine(Src, "warn.c"), "int w(void) { int unused = 1; return 0; }\n");
	File.WriteAllText(Path.Combine(Src, "bad.c"), "int bad(void) { return missing_function(); }\n");
	File.WriteAllText(Path.Combine(Src, "flag.c"), "#ifndef FROM_PROCESSFILE\n#error FROM_PROCESSFILE missing\n#endif\nint flag(void) { return FROM_PROCESSFILE; }\n");
	File.WriteAllText(Path.Combine(Src, "fmt.c"), "int   f( int x ){return x;}\n");
	File.WriteAllText(Path.Combine(Src, "undefined.c"), "int missing_symbol(void);\nint main(void) { return missing_symbol(); }\n");
	File.WriteAllText(Path.Combine(Res, "resource.h"), "#define IDR_DATA 100\n");
	File.WriteAllText(Path.Combine(Res, "data.txt"), "payload one\n");
	File.WriteAllText(Path.Combine(Res, "app.rc"), "#include \"resource.h\"\nIDR_DATA RCDATA \"data.txt\"\n");
}

/// <summary>
/// A configured instance for the sandbox: the include paths, -Wall, the system header through
/// -isystem, silent unless told otherwise, never aborting.
/// </summary>
Clang Configured(ClangOutput output = ClangOutput.Silent){
	Clang clang = new Clang();
	clang.Options.Output = output;
	clang.Options.AbortOnFailure = false;
	clang.Options.IncludeDirs = new KList { Inc, IncSpaced };
	clang.Options.SwitchesCC = new KList { "-Wall", "-isystem", Sys };
	clang.Options.SwitchesCXX = new KList { "-Wall", "-isystem", Sys };
	if (Host.IsWindows()){
		// Objects of the MSVC target carry the time of their compile: without this switch a unit
		// compiled again from the same code gives different bytes, and the archive and the link behind
		// it would be made again for nothing
		clang.Options.SwitchesCC.Add("-mno-incremental-linker-compatible");
		clang.Options.SwitchesCXX.Add("-mno-incremental-linker-compatible");
	}
	clang.Options.LibraryDirs = new KList { Lib };
	return clang;
}

Clang Silent(){
	Clang clang = new Clang();
	clang.Options.Output = ClangOutput.Silent;
	clang.Options.AbortOnFailure = false;
	return clang;
}

KList Sources(params string[] names){
	KList list = new KList();
	foreach (string n in names)
		list.Add(Path.Combine(Src, n).Replace('\\', '/'));
	return list;
}

KList Objects(params string[] names){
	KList list = new KList();
	foreach (string n in names)
		list.Add(Path.Combine(Obj, Path.GetFileNameWithoutExtension(n) + ObjExt).Replace('\\', '/'));
	return list;
}

string Archive(){ return Path.Combine(Lib, "core" + LibExt).Replace('\\', '/'); }
string ArchiveFile(){ return Host.IsWindows() ? Archive() : Path.Combine(Lib, "libcore" + LibExt).Replace('\\', '/'); }
string Exe1(){ return Path.Combine(Bin, "app1" + BinExt).Replace('\\', '/'); }
string Exe2(){ return Path.Combine(Bin, "app2" + BinExt).Replace('\\', '/'); }

/// <summary>
/// The whole build of the sandbox: every unit, the archive of a and b, app1 linking it, app2 with c.
/// Returns false at the first failure; the results stay in the instance.
/// </summary>
bool Build(Clang clang){
	string[] units = { "a.c", "b.cpp", "c.c", "main1.cpp", "main2.c" };
	if (clang.Compile(Sources(units), Objects(units)).Status == ToolStatus.Failed)
		return false;
	if (clang.Librarian(Objects("a.c", "b.cpp"), Archive()).Status == ToolStatus.Failed)
		return false;
	clang.Options.Libraries = new KList { "core" };
	if (clang.Linker(Objects("main1.cpp"), Exe1()).Status == ToolStatus.Failed)
		return false;
	clang.Options.Libraries = new KList();
	if (clang.Linker(Objects("main2.c", "c.c"), Exe2()).Status == ToolStatus.Failed)
		return false;
	return true;
}

// ------------------------------------------------------------------------------------------------
// [2/8] Output modes, shown on screen
// ------------------------------------------------------------------------------------------------

void TestOutputModes(){
	Banner("[2/8] Output modes");
	foreach (ClangOutput mode in new[] { ClangOutput.Silent, ClangOutput.Progress, ClangOutput.Detailed }){
		Msg.Print("--- " + mode + " mode, from clean: compile, archive, link; then a unit with a warning and a unit with an error ---");
		Msg.BeginIndent();
		Nuke(Obj); Nuke(Lib); Nuke(Bin);
		Clang clang = Configured(mode);
		clang.Options.Verbose = (mode == ClangOutput.Detailed);
		bool ok = Build(clang);
		Check("build in " + mode, Show(ok), "true");
		ToolResult r = clang.Compile(Sources("warn.c", "bad.c"), Objects("warn.c", "bad.c"), false);
		Check("warning and error in " + mode, r.Status + ", " + clang.LastCompile!.Warnings + " warnings, " + clang.LastCompile.Errors + " errors", "Failed, 1 warnings, 1 errors");
		Msg.EndIndent();
	}
	Msg.Print("--- Progress mode with a label, a dots reporter, the warnings as a count, and a filter hiding the error ---");
	Msg.BeginIndent();
	Clang custom = Configured(ClangOutput.Progress);
	custom.Options.TaskLabel = "Building the noisy units";
	custom.Options.Progress = new ProgressDots();
	custom.Options.ShowWarnings = false;
	custom.Compile(Sources("warn.c", "bad.c"), Objects("warn.c", "bad.c"), false, true);
	custom.Options.TaskLabel = "Same build, the error hidden by the filter";
	custom.Options.DiagnosticFilter = (string source, string line) => !line.Contains("missing_function");
	custom.Compile(Sources("warn.c", "bad.c"), Objects("warn.c", "bad.c"), false, true);
	Msg.EndIndent();
	Msg.Print("--- Detailed mode with ClangVerbose: the -v text of the compiler for one unit ---");
	Msg.BeginIndent();
	Clang verbose = Configured(ClangOutput.Detailed);
	verbose.Options.ClangVerbose = true;
	verbose.Compile(Sources("c.c"), Objects("c.c"), false, true);
	Msg.EndIndent();
	// What each mode renders, through a recording reporter
	RecordingProgress recorder = new RecordingProgress();
	foreach (ClangOutput mode in new[] { ClangOutput.Silent, ClangOutput.Progress, ClangOutput.Detailed }){
		recorder.Reset();
		Clang clang = Configured(mode);
		clang.Options.Progress = recorder;
		clang.Compile(Sources("a.c"), Objects("a.c"), false, true);
		foreach (string one in new[] { Path.Combine(Lib, "one" + LibExt), Path.Combine(Lib, "libone" + LibExt) })
			if (File.Exists(one))
				File.Delete(one);
		clang.Librarian(Objects("a.c"), Path.Combine(Lib, "one" + LibExt));
		Check("reporter lines in " + mode, recorder.Started + " started, " + recorder.Finished, mode == ClangOutput.Progress ? "2 started, ok" : "0 started, ");
	}
	// The detailed lines go out in every mode: at normal level in Detailed, at verbose level in the
	// others, so a message handler receives them while the console shows only what the mode says
	foreach (ClangOutput mode in new[] { ClangOutput.Silent, ClangOutput.Progress, ClangOutput.Detailed }){
		int normalCount = 0, verboseCount = 0, diagnostics = 0;
		// The handler of the caller, if any, is put back after the test
		Msg.MessageHandler? previous = Msg.OnMessage;
		Msg.OnMessage = (Msg.LogLevels level, Msg.MessageKind kind, string module, string message, int indent) => {
			if (kind == Msg.MessageKind.Task && message.StartsWith("Compiling ")){
				if (level == Msg.LogLevels.Normal) normalCount++; else verboseCount++;
			}
			if (kind == Msg.MessageKind.Warning && message.Contains("unused variable"))
				diagnostics++;
		};
		Clang clang = Configured(mode);
		clang.Options.Progress = recorder;
		clang.Compile(Sources("a.c", "b.cpp", "warn.c"), Objects("a.c", "b.cpp", "warn.c"), false, true);
		Msg.OnMessage = previous;
		Check("handler gets the unit lines in " + mode, normalCount + " normal, " + verboseCount + " verbose, warning " + (diagnostics > 0 ? "delivered" : "missing"), (mode == ClangOutput.Detailed ? "3 normal, 0 verbose" : "0 normal, 3 verbose") + ", warning delivered");
	}
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [3/8] Every verb once
// ------------------------------------------------------------------------------------------------

void TestVerbs(){
	Banner("[3/8] Every verb once");
	Nuke(Obj); Nuke(Lib); Nuke(Bin);
	Clang clang = Configured();
	string db = Path.Combine(Sandbox, "compile_commands.json");
	Check("OpenCompileCommands", Show(clang.OpenCompileCommands(db)), "true");
	string[] units = Host.IsWindows() ? new[] { "a.c", "b.cpp", "c.c", "flag.c", "res/app.rc" } : new[] { "a.c", "b.cpp", "c.c", "flag.c" };
	KList src = new KList();
	KList obj = new KList();
	foreach (string u in units){
		src.Add((u.StartsWith("res/") ? Path.Combine(Res, Path.GetFileName(u)) : Path.Combine(Src, u)).Replace('\\', '/'));
		obj.Add(Path.Combine(Obj, Path.GetFileNameWithoutExtension(u) + ObjExt).Replace('\\', '/'));
	}
	int processFileCalls = 0;
	clang.ProcessFile = (string file) => { processFileCalls++; return file.EndsWith("flag.c") ? "-DFROM_PROCESSFILE=1" : ""; };
	ToolResult r = clang.Compile(src, obj);
	CompileResult c = clang.LastCompile!;
	Check("Compile", r.Status.ToString(), "Success");
	Check("units compiled", c.Compiled + " of " + c.Units.Count + ", " + c.Queued + " queued, " + c.UpToDate + " up to date", units.Length + " of " + units.Length + ", " + units.Length + " queued, 0 up to date");
	Check("objects written", Show(((string[])obj).All(o => File.Exists(o))), "true");
	Check("dependency files written", Show(File.Exists(Path.Combine(Obj, "a.d")) && File.Exists(Path.Combine(Obj, "b.d"))), "true");
	Check("records written", Show(File.Exists(Path.Combine(Obj, "a" + ObjExt + ".kdep"))), "true");
	Check("ProcessFile called per C and C++ unit", processFileCalls.ToString(), units.Count(u => !u.EndsWith(".rc")).ToString());
	Check("ProcessFile arguments applied", c.Units.First(u => u.Source.EndsWith("flag.c")).Status.ToString(), "Compiled");
	Check("LastOutput kept", Show(clang.LastOutput != null), "true");
	Check("compile database written", c.CompileDatabase.Length > 0 && File.Exists(db) ? "yes" : "no", "yes");
	string dbText = File.ReadAllText(db);
	Check("database holds the C units", Show(dbText.Contains("a.c") && dbText.Contains("b.cpp")), "true");
	if (Host.IsWindows()){
		Check("database without the resource unit", Show(!dbText.Contains("app.rc")), "true");
		Check("resource unit compiled", c.Units.First(u => u.Source.EndsWith("app.rc")).Status.ToString(), "Compiled");
	}
	Check("Status queued", Clang.Status.Queued >= units.Length ? "counted" : Clang.Status.Queued.ToString(), "counted");
	// Librarian
	r = clang.Librarian(Objects("a.c", "b.cpp"), Archive());
	Check("Librarian", r.Status.ToString(), "Success");
	Check("archive written", Show(File.Exists(ArchiveFile())), "true");
	Check("LastLibrarian", clang.LastLibrarian!.Objects.Count() + " objects, up to date " + Show(clang.LastLibrarian.UpToDate), "2 objects, up to date false");
	// Linker: an executable, then a shared library
	clang.Options.Libraries = new KList { "core" };
	clang.Compile(Sources("main1.cpp"), Objects("main1.cpp"));
	r = clang.Linker(Objects("main1.cpp"), Exe1());
	Check("Linker executable", r.Status.ToString(), "Success");
	Check("executable written", Show(File.Exists(Exe1())), "true");
	Check("LastLinker", clang.LastLinker!.Shared ? "shared" : "executable", "executable");
	clang.Options.Libraries = new KList();
	string shared = Path.Combine(Bin, "core" + clang.Options.SharedExtension).Replace('\\', '/');
	r = clang.Linker(Objects("a.c", "b.cpp"), shared, true);
	Check("Linker shared library", r.Status.ToString(), "Success");
	Check("shared library written", Show(File.Exists(clang.LastLinker!.Output)), "true");
	Check("lib prefix on unix", Path.GetFileName(clang.LastLinker.Output), Host.IsWindows() ? "core" + clang.Options.SharedExtension : "libcore" + clang.Options.SharedExtension);
	// Format
	if (HasFormat){
		string fmt = Path.Combine(Src, "fmt.c").Replace('\\', '/');
		Check("Format", Show(clang.Format(new KList { fmt }, "")), "true");
		Check("file formatted", File.ReadAllText(fmt).Contains("int f(int x)") ? "reformatted" : File.ReadAllText(fmt).Trim(), "reformatted");
		Check("LastFormat", clang.LastFormat!.Files.Count() + " formatted, " + clang.LastFormat.Failed.Count() + " failed", "1 formatted, 0 failed");
		Check("Format missing file", Show(clang.Format(new KList { Path.Combine(Src, "nope.c") }, "")), "false");
		Check("error code", clang.LastError.Code.ToString(), "NotFound");
	} else
		Skip("Format", "clang-format is not available");
	// The compile database reused by a second instance
	Clang other = Silent();
	Check("OpenCompileCommands reused", Show(other.OpenCompileCommands(Path.Combine(Sandbox, "other.json"))), "true");
	Check("second database not created", Show(!File.Exists(Path.Combine(Sandbox, "other.json"))), "true");
	// Status
	Check("Status errors so far", Clang.Status.Errors >= 3 ? "counted" : Clang.Status.Errors.ToString(), "counted");
	// Shown, not checked: the table is printed for the eye and counts as no check
	Msg.PrintTask("Status.Print()          : ");
	Msg.RawPrint("");
	Clang.Status.Print();
	// Two instances with different options: each compiles its own objects
	Clang second = Configured();
	second.Options.Defines = new KList { "SECOND=1" };
	string otherObj = Path.Combine(Obj, "second");
	Directory.CreateDirectory(otherObj);
	KList obj2 = new KList { Path.Combine(otherObj, "a" + ObjExt).Replace('\\', '/') };
	Check("second instance compiles", second.Compile(Sources("a.c"), obj2).Status.ToString(), "Success");
	Check("first instance unchanged", clang.Options.Defines.Count().ToString(), "0");
	// Clean
	Check("Clean", Show(clang.Clean(obj2, Path.Combine(Bin, "x.out"))), "true");
	Check("folders gone", Show(!Directory.Exists(otherObj)), "true");
	Check("Clean of absent folders", Show(clang.Clean(obj2, Path.Combine(Bin, "x.out"))), "true");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [4/8] Failures
// ------------------------------------------------------------------------------------------------

void TestFailures(){
	Banner("[4/8] Failures");
	Nuke(Obj); Nuke(Lib); Nuke(Bin);
	Clang clang = Configured();
	ToolResult r = clang.Compile(Sources("a.c", "bad.c", "c.c"), Objects("a.c", "bad.c", "c.c"), false);
	CompileResult c = clang.LastCompile!;
	Check("error unit result", r.Status.ToString(), "Failed");
	Check("error code", clang.LastError.Code.ToString(), "Failed");
	Check("first error line", clang.LastError.Message.Contains("bad.c") && clang.LastError.Message.Contains("error:") ? "names bad.c" : clang.LastError.Message, "names bad.c");
	Check("failed units", c.Failed.Count + ": " + string.Join(",", c.Failed.Select(u => Path.GetFileName(u.Source))), "1: bad.c");
	Check("diagnostics kept per unit", c.Units.First(u => u.Source.EndsWith("bad.c")).Diagnostics.Any(l => l.Contains("missing_function")) ? "kept" : "missing", "kept");
	Check("other units compiled", Compiled(c), "a.c,c.c");
	Check("not cancelled", Show(c.Cancelled), "false");
	// The abort, through a child script that returns 1 with the reason
	WriteChildScript();
	int code = Kombine(Path.Combine(Sandbox, "child.csx"), "abort", null, false);
	Check("abort through a child", code.ToString(), "1");
	Check("reason in Engine.LastError", Engine.LastError.Message.Contains("bad.c") ? "names bad.c" : Engine.LastError.Message, "names bad.c");
	// Missing files, unknown extension, mismatched lists, duplicates
	r = clang.Compile(Sources("nope.c"), Objects("nope.c"), false);
	Check("missing source", clang.LastError.Code + ", " + r.Status, "NotFound, Failed");
	r = clang.Compile(new KList { Path.Combine(Src, "a.c"), Path.Combine(Src, "text.txt") }, Objects("a.c", "text.txt"), false);
	Check("unknown extension", clang.LastError.Code.ToString(), "NotSupported");
	r = clang.Compile(Sources("a.c", "c.c"), Objects("a.c"), false);
	Check("mismatched lists", clang.LastError.Code.ToString(), "InvalidArgument");
	r = clang.Compile(Sources("a.c", "a.c"), Objects("a.c", "b.o"), false);
	Check("duplicates", clang.LastError.Code.ToString(), "InvalidArgument");
	r = clang.Librarian(Objects("nope.c"), Archive());
	Check("missing object", clang.LastError.Code + ", " + r.Status, "NotFound, Failed");
	// A symbol defined by two objects: reported by the librarian, where the mistake is, as a warning
	// by default, as a failure with DuplicateSymbols = Fail, never for the inline functions and
	// templates every C++ unit emits
	File.WriteAllText(Path.Combine(Src, "dup1.c"), "int twice(void) { return 1; }\nint only1(void) { return 1; }\n");
	File.WriteAllText(Path.Combine(Src, "dup2.c"), "int twice(void) { return 2; }\nint only2(void) { return 2; }\n");
	File.WriteAllText(Path.Combine(Src, "cx1.cpp"), "inline int shared_inline(int x) { return x + 1; }\ntemplate<class T> T tpl(T v) { return v; }\nint use1() { return shared_inline(1) + tpl(2); }\n");
	File.WriteAllText(Path.Combine(Src, "cx2.cpp"), "inline int shared_inline(int x) { return x + 1; }\ntemplate<class T> T tpl(T v) { return v; }\nint use2() { return shared_inline(2) + tpl(3); }\n");
	clang.Compile(Sources("dup1.c", "dup2.c", "cx1.cpp", "cx2.cpp"), Objects("dup1.c", "dup2.c", "cx1.cpp", "cx2.cpp"), false);
	string dupLib = Path.Combine(Lib, "dup" + LibExt).Replace('\\', '/');
	clang.Options.DuplicateSymbols = DuplicateSymbolCheck.Warn;
	r = clang.Librarian(Objects("dup1.c", "dup2.c"), dupLib);
	Check("duplicate symbol warned", r.Status + ", " + clang.LastLibrarian!.Warnings + " warnings" + (clang.LastLibrarian.Diagnostics.Any(l => l.Contains("twice") && l.Contains("dup1") && l.Contains("dup2")) ? ", names the symbol and both objects" : ", " + string.Join(" | ", clang.LastLibrarian.Diagnostics)), "Success, 1 warnings, names the symbol and both objects");
	clang.Options.DuplicateSymbols = DuplicateSymbolCheck.Fail;
	File.Delete(Host.IsWindows() ? dupLib : Path.Combine(Lib, "libdup" + LibExt));
	r = clang.Librarian(Objects("dup1.c", "dup2.c"), dupLib);
	Check("duplicate symbol refused", clang.LastError.Code + ", " + r.Status + ", archive " + (File.Exists(Host.IsWindows() ? dupLib : Path.Combine(Lib, "libdup" + LibExt)) ? "written" : "not written"), "InvalidArgument, Failed, archive not written");
	clang.Options.DuplicateSymbols = DuplicateSymbolCheck.Warn;
	string cxLib = Path.Combine(Lib, "cx" + LibExt).Replace('\\', '/');
	r = clang.Librarian(Objects("cx1.cpp", "cx2.cpp"), cxLib);
	Check("inline and template not duplicates", r.Status + ", " + clang.LastLibrarian!.Warnings + " warnings", "Success, 0 warnings");
	clang.Options.DuplicateSymbols = DuplicateSymbolCheck.Ignore;
	r = clang.Librarian(Objects("dup1.c", "dup2.c"), dupLib);
	Check("duplicate symbol ignored (default)", r.Status + ", " + clang.LastLibrarian!.Warnings + " warnings", "Success, 0 warnings");
	// A tool that runs but is not a compiler: a failure of the tool, not of the sources
	Clang wrong = Configured();
	wrong.Options.CC = "git";
	r = wrong.Compile(Sources("a.c"), Objects("a.c"), false, true);
	Check("tool failure", r.Status + ", " + (wrong.LastError.Message.Contains("ClangOptions.CC") ? "hint names ClangOptions.CC" : wrong.LastError.Message), "Failed, hint names ClangOptions.CC");
	// A timeout
	Clang slow = Configured();
	slow.Options.Timeout = 1;
	r = slow.Compile(Sources("b.cpp"), Objects("b.cpp"), false, true);
	Check("timeout", r.Status + ", " + (slow.LastError.Message.Contains("timeout") ? "timeout reported" : slow.LastError.Message), "Failed, timeout reported");
	// A linker error
	clang.Compile(Sources("undefined.c"), Objects("undefined.c"), false);
	r = clang.Linker(Objects("undefined.c"), Path.Combine(Bin, "undefined" + BinExt), false);
	Check("linker error", r.Status + ", " + clang.LastError.Code, "Failed, Failed");
	Check("linker diagnostics kept", clang.LastLinker!.Errors > 0 && clang.LastLinker.Diagnostics.Any(l => l.Contains("missing_symbol")) ? "kept" : string.Join(" | ", clang.LastLinker.Diagnostics), "kept");
	Check("record of a failed link absent", Show(!File.Exists(Path.Combine(Obj, "undefined" + BinExt + ".kdep"))), "true");
	Check("output folder holds no record", Show(!Directory.EnumerateFiles(Bin, "*.kdep").Any()), "true");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [5/8] Nothing left behind
// ------------------------------------------------------------------------------------------------

void TestNothingLeftBehind(){
	Banner("[5/8] Nothing left behind");
	Nuke(Obj); Nuke(Lib); Nuke(Bin);
	WriteFixtures();
	Clang clang = Configured();
	current = clang;
	Check("initial build", Show(Build(clang)), "true");
	Check("second build does nothing", Show(Build(clang)) + ", " + Compiled(clang.LastCompile!), "true, ");
	// The objects clang writes are deterministic: a unit compiled again from the same code gives the
	// same bytes, so the archive and the executables behind it are made again only when the code changed
	Case("object deleted", () => File.Delete(Objects("a.c")[0]), "a.c", "");
	Case("source edited", () => Edit(Path.Combine(Src, "a.c"), "SPACED_VALUE;", "SPACED_VALUE + 1;"), "a.c", "core,app1");
	Case("shared header edited", () => Edit(Path.Combine(Inc, "shared.h"), "1", "2"), "a.c,b.cpp", "core,app1");
	Case("header through a header edited", () => Edit(Path.Combine(Inc, "inner.h"), "1", "2"), "b.cpp", "core,app1");
	Case("-isystem header edited", () => Edit(Path.Combine(Sys, "sysheader.h"), "1", "2"), "b.cpp", "core,app1");
	Case("header in a folder with a space", () => Edit(Path.Combine(IncSpaced, "spaced.h"), "1", "2"), "a.c", "core,app1");
	Case("source dated older than its object", () => { Edit(Path.Combine(Src, "c.c"), "ONLY_VALUE;", "ONLY_VALUE + 1;"); File.SetLastWriteTimeUtc(Path.Combine(Src, "c.c"), new DateTime(2000, 1, 1)); }, "c.c", "app2");
	Case("edit keeping the date", () => EditKeepingDate(Path.Combine(Src, "c.c"), "+ 1;", "+ 12;"), "c.c", "app2");
	Case("dependency file deleted", () => File.Delete(Path.Combine(Obj, "c.d")), "c.c", "");
	Case("define changed", () => clang.Options.Defines = new KList { "AGAIN=1" }, "a.c,b.cpp,c.c,main1.cpp,main2.c", "");
	Case("include path changed", () => clang.Options.IncludeDirs = new KList { Inc, IncSpaced, Sys }, "a.c,b.cpp,c.c,main1.cpp,main2.c", "");
	Case("switch changed", () => { clang.Options.SwitchesCC.Add("-DVIA_SWITCH=1"); }, "a.c,c.c,main2.c", "");
	Case("compiler name changed", () => { clang.Options.CXX = Path.GetFileNameWithoutExtension(clang.Options.CXX) == "clang++" ? "clang++" + (Host.IsWindows() ? ".exe" : "") : clang.Options.CXX; }, Host.IsWindows() ? "b.cpp,main1.cpp" : "", Host.IsWindows() ? "app1,app2" : "");
	Case("linker switch changed", () => clang.Options.SwitchesLD = new KList { "-w" }, "", "app1,app2");
	Case("archive deleted", () => File.Delete(ArchiveFile()), "", "core");
	Case("executable deleted", () => File.Delete(Exe2()), "", "app2");
	Case("rebuild", () => rebuildNext = true, "a.c,b.cpp,c.c,main1.cpp,main2.c", "");
	// A header deleted: the unit compiles again and fails as it should
	File.Delete(Path.Combine(Inc, "only.h"));
	ToolResult r = clang.Compile(Sources("a.c", "b.cpp", "c.c"), Objects("a.c", "b.cpp", "c.c"));
	Check("header deleted: unit fails", r.Status + ", " + string.Join(",", clang.LastCompile!.Failed.Select(u => Path.GetFileName(u.Source))), "Failed, c.c");
	WriteFixtures();
	// The archive list
	Check("restore", Show(Build(clang)), "true");
	string extra = Objects("c.c")[0];
	r = clang.Librarian(Objects("a.c", "b.cpp", "c.c"), Archive());
	Check("object added to the archive", r.Status.ToString(), "Success");
	r = clang.Librarian(Objects("a.c", "b.cpp"), Archive());
	Check("object removed from the archive", r.Status + ", " + (ArchiveContains("c" + ObjExt) ? "still inside" : "gone"), "Success, gone");
	// An archive whose command line exceeds the platform limit: the objects go through a response file
	// in one command, and every member must be inside
	string many = Path.Combine(Sandbox, "many");
	Directory.CreateDirectory(Path.Combine(many, "src"));
	Directory.CreateDirectory(Path.Combine(many, "obj"));
	KList manySrc = new KList();
	KList manyObj = new KList();
	for (int i = 0; i < 320; i++){
		string name = "unit_with_a_long_enough_name_to_fill_the_command_line_" + i.ToString("D3");
		File.WriteAllText(Path.Combine(many, "src", name + ".c"), "int f" + i + "(void) { return " + i + "; }\n");
		manySrc.Add(Path.Combine(many, "src", name + ".c").Replace('\\', '/'));
		manyObj.Add(Path.Combine(many, "obj", name + ObjExt).Replace('\\', '/'));
	}
	Check("compile of 320 units", clang.Compile(manySrc, manyObj).Status.ToString(), "Success");
	string manyLib = Path.Combine(many, "many" + LibExt).Replace('\\', '/');
	Check("archive through a response file", clang.Librarian(manyObj, manyLib).Status + ", " + (((string)manyObj.Flatten()).Length > 32766 ? "line over the limit" : "line under the limit"), "Success, line over the limit");
	Check("every member inside", ArchiveMembers(Host.IsWindows() ? manyLib : Path.Combine(many, "libmany" + LibExt)).ToString(), "320");
	Check("no response file left", Show(!Directory.EnumerateFiles(Path.GetTempPath(), "kombine-*.rsp").Any()), "true");
	// A library given by its path, and one rebuilt in the library paths
	clang.Options.Libraries = new KList { ArchiveFile() };
	clang.Options.LibraryDirs = new KList();
	string byPath = Path.Combine(Bin, "bypath" + BinExt).Replace('\\', '/');
	Check("link with a library by path", clang.Linker(Objects("main1.cpp"), byPath).Status.ToString(), "Success");
	DateTime before = File.GetLastWriteTimeUtc(byPath);
	Edit(Path.Combine(Src, "a.c"), "SPACED_VALUE;", "SPACED_VALUE + 3;");
	clang.Compile(Sources("a.c"), Objects("a.c"));
	clang.Librarian(Objects("a.c", "b.cpp"), Archive());
	System.Threading.Thread.Sleep(20);
	Check("library by path made again: linked", clang.Linker(Objects("main1.cpp"), byPath).Status + ", " + (File.GetLastWriteTimeUtc(byPath) > before ? "relinked" : "not relinked"), "Success, relinked");
	// Resources, on Windows
	if (Host.IsWindows()){
		KList rc = new KList { Path.Combine(Res, "app.rc").Replace('\\', '/') };
		KList rco = new KList { Path.Combine(Obj, "app.res").Replace('\\', '/') };
		Check("resource compiled", clang.Compile(rc, rco).Status.ToString(), "Success");
		Check("resource up to date", clang.Compile(rc, rco).Status.ToString(), "NoChanges");
		File.WriteAllText(Path.Combine(Res, "data.txt"), "payload two\n");
		Check("resource data edited: compiled", clang.Compile(rc, rco).Status.ToString(), "Success");
		Edit(Path.Combine(Res, "resource.h"), "100", "101");
		Check("resource header edited: compiled", clang.Compile(rc, rco).Status.ToString(), "Success");
	}
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [6/8] Nothing built without need
// ------------------------------------------------------------------------------------------------

void TestNothingWithoutNeed(){
	Banner("[6/8] Nothing built without need");
	Nuke(Obj); Nuke(Lib); Nuke(Bin);
	WriteFixtures();
	Clang clang = Configured();
	current = clang;
	Check("initial build", Show(Build(clang)), "true");
	Case("no change at all", () => { }, "", "");
	Case("header no unit includes edited", () => Edit(Path.Combine(Inc, "unused.h"), "1", "2"), "", "");
	Case("source touched, content unchanged", () => File.SetLastWriteTimeUtc(Path.Combine(Src, "a.c"), DateTime.UtcNow), "", "");
	Case("header touched, content unchanged", () => File.SetLastWriteTimeUtc(Path.Combine(Inc, "shared.h"), DateTime.UtcNow), "", "");
	Case("source of one unit edited", () => Edit(Path.Combine(Src, "c.c"), "ONLY_VALUE;", "ONLY_VALUE + 1;"), "c.c", "app2");
	Case("unit order changed", () => reverseNext = true, "", "");
	Case("options that do not reach the command", () => { clang.Options.Output = ClangOutput.Silent; clang.Options.TaskLabel = "x"; clang.Options.Verbose = true; clang.Options.ShowWarnings = false; clang.Options.Verbose = false; clang.Options.TaskLabel = ""; }, "", "");
	Check("OpenCompileCommands again", Show(clang.OpenCompileCommands(Path.Combine(Sandbox, "again.json"))), "true");
	Check("database not created again", Show(!File.Exists(Path.Combine(Sandbox, "again.json"))), "true");
	Check("empty list", clang.Compile(new KList(), new KList()).Status.ToString(), "NoChanges");
	// A library the executable does not link, made again
	string other = Path.Combine(Lib, "other" + LibExt).Replace('\\', '/');
	clang.Librarian(Objects("c.c"), other);
	Dictionary<string, DateTime> dates = Dates();
	File.Delete(Host.IsWindows() ? other : Path.Combine(Lib, "libother" + LibExt));
	clang.Librarian(Objects("c.c"), other);
	clang.Options.Libraries = new KList { "core" };
	clang.Linker(Objects("main1.cpp"), Exe1());
	Check("unrelated library made again: no link", Rebuilt(dates), "");
	Check("Status.Completed unchanged by the up to date builds", Show(Clang.Status.Completed > 0), "true");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [7/8] Child script
// ------------------------------------------------------------------------------------------------

void TestChildScript(){
	Banner("[7/8] Child script");
	Nuke(Obj); Nuke(Lib); Nuke(Bin);
	WriteFixtures();
	RecordingProgress recorder = new RecordingProgress();
	Clang.ClangOptions defaults = Configured(ClangOutput.Progress).Options;
	defaults.Progress = recorder;
	defaults.Defines = new KList { "FROM_PARENT=1" };
	defaults.SetAsDefault();
	Clang copy = new Clang();
	Check("new instance takes the defaults", copy.Options.Output + ", " + copy.Options.Defines.Count() + " defines", "Progress, 1 defines");
	copy.Options.Defines.Add("LOCAL=1");
	Check("shared object untouched", defaults.Defines.Count().ToString(), "1");
	long queuedBefore = Clang.Status.Queued;
	long completedBefore = Clang.Status.Completed;
	WriteChildScript();
	int code = Kombine(Path.Combine(Sandbox, "child.csx"), "compile", null, false);
	Check("child compiles", code.ToString(), "0");
	Check("child took the output mode and the reporter", recorder.Started > 0 ? "progress line rendered by the child" : "no line", "progress line rendered by the child");
	Check("child added to the status", (Clang.Status.Queued - queuedBefore) + " queued, " + (Clang.Status.Completed - completedBefore) + " completed", "3 queued, 3 completed");
	Check("child appended to the compile database", Show(File.ReadAllText(Path.Combine(Sandbox, "compile_commands.json")).Contains("main2.c")), "true");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [8/8] Batch concurrency of the engine: ConcurrentCommands runs exactly that many at a time
// ------------------------------------------------------------------------------------------------

void TestBatchLimit(){
	Banner("[8/8] Batch concurrency");
	// A program that holds a lock file while it runs for 400 ms and fails when the lock already
	// exists: with one command at a time no run ever sees the lock of another
	string src = Path.Combine(Sandbox, "sleeper.c");
	File.WriteAllText(src, string.Join("\n", new[] {
		"#include <stdio.h>",
		"#ifdef _WIN32",
		"#include <windows.h>",
		"#define SLEEP() Sleep(400)",
		"#else",
		"#include <unistd.h>",
		"#define SLEEP() usleep(400000)",
		"#endif",
		"int main(int argc, char** argv) {",
		"	FILE* f = fopen(argv[1], \"r\");",
		"	if (f) { fclose(f); return 1; }",
		"	f = fopen(argv[1], \"w\");",
		"	if (f) fclose(f);",
		"	SLEEP();",
		"	remove(argv[1]);",
		"	return 0;",
		"}",
		""
	}));
	Clang clang = Configured();
	KList so = new KList { src.Replace('\\', '/') };
	KList oo = new KList { Path.Combine(Obj, "sleeper" + ObjExt).Replace('\\', '/') };
	string exe = Path.Combine(Bin, "sleeper" + BinExt).Replace('\\', '/');
	bool built = clang.Compile(so, oo).Status != ToolStatus.Failed && clang.Linker(oo, exe).Status != ToolStatus.Failed;
	Check("sleeper built", Show(built), "true");
	if (!built){
		EndBanner();
		return;
	}
	string lockFile = Path.Combine(Sandbox, "sleeper.lock");
	// One at a time: four runs of 400 ms take at least 1.6 s and never overlap
	double seconds = RunSleepers(exe, lockFile, 1, out ToolResult one);
	Check("limit 1: one at a time", one.Status + (seconds >= 1.5 ? ", no overlap" : ", overlapped (" + seconds.ToString("0.0") + " s)"), "Success, no overlap");
	// Two at a time: two rounds, well under the sequential time
	seconds = RunSleepers(exe, lockFile, 2, out ToolResult two);
	Check("limit 2: two at a time", seconds < 1.5 ? "concurrent" : "sequential (" + seconds.ToString("0.0") + " s)", "concurrent");
	// Zero: every queued command at once
	seconds = RunSleepers(exe, lockFile, 0, out ToolResult all);
	Check("limit 0: all at once", seconds < 1.0 ? "all at once" : "limited (" + seconds.ToString("0.0") + " s)", "all at once");
	EndBanner();
}

/// <summary>
/// Runs the sleeper four times through one batch with the given limit and returns the seconds it took.
/// </summary>
double RunSleepers(string exe, string lockFile, uint limit, out ToolResult result){
	if (File.Exists(lockFile))
		File.Delete(lockFile);
	Tool tool = new Tool("sleeper");
	tool.ConcurrentCommands = limit;
	for (int i = 0; i < 4; i++)
		tool.QueueCommand(exe, "\"" + lockFile + "\"", i, null);
	System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
	result = tool.ExecuteCommands();
	if (File.Exists(lockFile))
		File.Delete(lockFile);
	return watch.Elapsed.TotalSeconds;
}

/// <summary>
/// Writes the child script of the sandbox: "abort" compiles the error unit with the abort in
/// effect, "compile" compiles three units with the defaults of the parent.
/// </summary>
void WriteChildScript(){
	string script = string.Join("\n", new[] {
		"#load \"extensions/clang.csx\"",
		"int abort(string[] args){",
		"	Clang clang = new Clang();",
		"	clang.Options.Output = ClangOutput.Silent;",
		"	clang.Options.IncludeDirs = new KList { \"inc\", \"inc dir\" };",
		"	clang.Compile(new KList { \"src/a.c\", \"src/bad.c\", \"src/c.c\" }, new KList { \"obj/child_a" + ObjExt + "\", \"obj/child_bad" + ObjExt + "\", \"obj/child_c" + ObjExt + "\" }, true);",
		"	return 0;",
		"}",
		"int compile(string[] args){",
		"	Clang clang = new Clang();",
		"	clang.OpenCompileCommands(\"compile_commands.json\");",
		"	ToolResult r = clang.Compile(new KList { \"src/a.c\", \"src/c.c\", \"src/main2.c\" }, new KList { \"obj/child_a" + ObjExt + "\", \"obj/child_c" + ObjExt + "\", \"obj/child_main2" + ObjExt + "\" }, false, true);",
		"	return r.Status == ToolStatus.Failed ? 1 : 0;",
		"}",
		""
	});
	File.WriteAllText(Path.Combine(Sandbox, "child.csx"), script);
}

// ------------------------------------------------------------------------------------------------
// The cases of groups 5 and 6
// ------------------------------------------------------------------------------------------------

bool rebuildNext = false;
bool reverseNext = false;

/// <summary>
/// Applies one change to an up to date tree, builds again and checks what compiled and what was
/// made again: exactly the expected units and outputs, in both directions.
/// </summary>
void Case(string name, Action change, string expectedCompiled, string expectedRebuilt){
	// The options a case changes belong to the instance of the group
	change();
	Clang instance = current!;
	Dictionary<string, DateTime> dates = Dates();
	System.Threading.Thread.Sleep(20);
	string[] units = { "a.c", "b.cpp", "c.c", "main1.cpp", "main2.c" };
	if (reverseNext){
		units = units.Reverse().ToArray();
		reverseNext = false;
	}
	bool ok = instance.Compile(Sources(units), Objects(units), null, rebuildNext).Status != ToolStatus.Failed;
	rebuildNext = false;
	string compiled = Compiled(instance.LastCompile!);
	ok &= instance.Librarian(Objects("a.c", "b.cpp"), Archive()).Status != ToolStatus.Failed;
	instance.Options.Libraries = new KList { "core" };
	ok &= instance.Linker(Objects("main1.cpp"), Exe1()).Status != ToolStatus.Failed;
	instance.Options.Libraries = new KList();
	ok &= instance.Linker(Objects("main2.c", "c.c"), Exe2()).Status != ToolStatus.Failed;
	string rebuilt = Rebuilt(dates);
	Check(name, (ok ? "" : "FAILED build, ") + "compiled [" + compiled + "] made [" + rebuilt + "]", "compiled [" + expectedCompiled + "] made [" + expectedRebuilt + "]");
}

/// <summary>The instance whose options the cases change; set by the groups.</summary>
Clang? current = null;

/// <summary>
/// The units compiled by a Compile, by name, in order.
/// </summary>
string Compiled(CompileResult c){
	return string.Join(",", c.Units.Where(u => u.Status == UnitStatus.Compiled || u.Status == UnitStatus.Warnings).Select(u => Path.GetFileName(u.Source)));
}

/// <summary>
/// The dates of the archive and the executables.
/// </summary>
Dictionary<string, DateTime> Dates(){
	Dictionary<string, DateTime> d = new Dictionary<string, DateTime>();
	foreach (var pair in new[] { ("core", ArchiveFile()), ("app1", Exe1()), ("app2", Exe2()) })
		d[pair.Item1] = File.Exists(pair.Item2) ? File.GetLastWriteTimeUtc(pair.Item2) : DateTime.MinValue;
	return d;
}

/// <summary>
/// The outputs whose date moved since the snapshot, by name.
/// </summary>
string Rebuilt(Dictionary<string, DateTime> before){
	Dictionary<string, DateTime> now = Dates();
	return string.Join(",", before.Keys.Where(k => now[k] != before[k]));
}

/// <summary>
/// True when the archive holds a member of that name (ar t).
/// </summary>
bool ArchiveContains(string member){
	Tool t = new Tool("ar");
	ToolResult r = t.CommandSync(new Clang().Options.AR, "t \"" + ArchiveFile() + "\"");
	return string.Concat(r.Stdout).Contains(member);
}

/// <summary>
/// The number of members of an archive (ar t).
/// </summary>
int ArchiveMembers(string archive){
	Tool t = new Tool("ar");
	ToolResult r = t.CommandSync(new Clang().Options.AR, "t \"" + archive + "\"");
	return string.Concat(r.Stdout).Split('\n').Count(l => l.Trim().Length > 0);
}

/// <summary>
/// Replaces text in a file.
/// </summary>
void Edit(string file, string from, string to){
	File.WriteAllText(file, File.ReadAllText(file).Replace(from, to));
}

/// <summary>
/// Replaces text of the same length in a file and restores its date.
/// </summary>
void EditKeepingDate(string file, string from, string to){
	DateTime date = File.GetLastWriteTimeUtc(file);
	Edit(file, from, to);
	File.SetLastWriteTimeUtc(file, date);
}

// ------------------------------------------------------------------------------------------------
// Helpers
// ------------------------------------------------------------------------------------------------

/// <summary>
/// A progress reporter that records the calls it receives, to verify what the Progress output
/// mode renders and that the other modes render nothing.
/// </summary>
class RecordingProgress : ITaskProgress {
	public int Started = 0;
	public int Reports = 0;
	public string Finished = string.Empty;
	public void Start(string message){ Started++; }
	public void Report(double value, string? status = null){ Reports++; }
	public void Finish(string message = "", ProgressOutcome outcome = ProgressOutcome.Success){ Finished = message; }
	public void Dispose(){ }
	public void Reset(){ Started = 0; Reports = 0; Finished = string.Empty; }
}

/// <summary>
/// Removes a folder with everything in it.
/// </summary>
void Nuke(string path){
	if (!Directory.Exists(path))
		return;
	foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
		File.SetAttributes(file, FileAttributes.Normal);
	Directory.Delete(path, true);
}

string Show(bool value){
	return value ? "true" : "false";
}

void Banner(string title){
	Msg.Print(title);
	Msg.BeginIndent();
	TestGroup(title);
}

void EndBanner(){
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);
}

void Skip(string group, string reason){
	skipped++;
	Msg.PrintTask($"{group,-28} : skipped ");
	Msg.PrintTaskWarning(reason);
	Msg.RawPrint(Environment.NewLine);
	TestResult("SKIPPED", group + " : " + reason);
}

/// <summary>
/// Compares the actual value against the expected one and prints an aligned line with a colored
/// OK / FAILED tag. On failure the expected value is printed below.
/// </summary>
void Check(string what, string actual, string expected){
	Msg.PrintTask($"{what,-40} : {actual,-48} ");
	if (actual == expected){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-40} : {expected}");
	}
	TestResult(actual == expected ? "OK" : "FAILED", what + " : " + actual);
}
