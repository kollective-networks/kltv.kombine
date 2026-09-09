/*---------------------------------------------------------------------------------------------------------

	Kombine Bin2cpp Extension Example

	(C)Kollective Networks 2026

	Test of the bin2cpp extension (extensions/bin2cpp.csx), by groups, on binaries written into a
	sandbox (.tmp.bin2cpp) and removed at the end:

		[1/5] Basics                   one source per binary, one source for all, the symbols, the records
		[2/5] Output modes             what each mode renders, shown on screen once
		[3/5] Nothing left behind      every change that must generate something generates exactly that
		[4/5] Nothing without need     every change that must generate nothing generates nothing
		[5/5] Failures                 a missing binary, mismatched lists, a duplicate name, a write that fails
		                               leaving the previous output intact, the abort through a child script

	The build action converts the svg files of the res folder and compiles them with clang, the way
	a build script does; clean removes what it produced.

		mkb test     runs every group
		mkb build    converts the resources and builds the binary of mybin.csx
		mkb clean    removes the generated sources and the build output

---------------------------------------------------------------------------------------------------------*/
#load "extensions/bin2cpp.csx"
#load "extensions/clang.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// Define the output artifact folders and export them so the child scripts build into one tree
KValue OutputBin = CurrentWorkingFolder + "/out/bin/";
KValue OutputTmp = CurrentWorkingFolder + "/out/tmp/";
KValue OutputLib = CurrentWorkingFolder + "/out/lib/";
OutputBin.Export("OutputBin");
OutputTmp.Export("OutputTmp");
OutputLib.Export("OutputLib");

int passed = 0;
int failed = 0;
string Sandbox = ".tmp.bin2cpp";
string In = ".tmp.bin2cpp/in";
string Gen = ".tmp.bin2cpp/gen";

/// <summary>
/// Runs every group.
/// </summary>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing the bin2cpp extension");
	Msg.BeginIndent();
	Fixtures();
	TestBasics();
	TestOutputModes();
	TestNothingLeftBehind();
	TestNothingWithoutNeed();
	TestFailures();
	int total = passed + failed;
	Msg.PrintTask($"Summary : {passed} of {total} checks passed ");
	if (failed == 0)
		Msg.PrintTaskSuccess("OK");
	else
		Msg.PrintTaskError($"{failed} FAILED");
	Msg.EndIndent();
	Msg.EndIndent();
	Nuke(Sandbox);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failed != 0){
		Msg.PrintError($"bin2cpp extension tests failed: {failed} of {total}");
		return 1;
	}
	return 0;
}

/// <summary>
/// Converts the svg files of the res folder into sources of the gen folder, one per file and one
/// for all, and builds the binary that uses them.
/// </summary>
int build(string[] args) {
	Bin2cpp converter = new Bin2cpp();
	KList binFiles = Glob("res/**/*.svg");
	KList cppFiles = binFiles.WithExtension(".cpp").WithReplace("res/", "gen/");
	if (!converter.Generate(binFiles, cppFiles)) {
		Msg.PrintError("Failed to generate cpp files: " + converter.LastError.Message);
		return 1;
	}
	// The symbols of every input, generated or not, for a script that writes a header declaring them.
	// The symbol derives from the path as given: relative paths keep it stable
	Msg.Print("Generated symbols:");
	Msg.BeginIndent();
	foreach (var symbol in converter.Symbols)
		Msg.Print(symbol.Key + " -> " + symbol.Value);
	Msg.EndIndent();
	KValue amalgamation = "gen/amalgamation.cpp";
	if (!converter.Generate(binFiles, amalgamation)) {
		Msg.PrintError("Failed to generate the amalgamation: " + converter.LastError.Message);
		return 1;
	}
	Clang clang = new Clang();
	clang.OpenCompileCommands("out/tmp/compile_commands.json");
	Kombine("mybin.csx", "build", args);
	return 0;
}

/// <summary>
/// Removes the generated sources and the build output.
/// </summary>
int clean(string[] args) {
	Kombine("mybin.csx", "clean", args);
	Folders.Delete("gen", true);
	Nuke(Sandbox);
	return 0;
}

// ------------------------------------------------------------------------------------------------
// The fixtures: three small binaries
// ------------------------------------------------------------------------------------------------

void Fixtures(){
	Nuke(Sandbox);
	Directory.CreateDirectory(In);
	Directory.CreateDirectory(Gen);
	WriteBinary(Path.Combine(In, "a.bin"), 100, 1);
	WriteBinary(Path.Combine(In, "b.bin"), 300, 2);
	WriteBinary(Path.Combine(In, "c.bin"), 17, 3);
}

void WriteBinary(string file, int size, int seed){
	byte[] data = new byte[size];
	for (int i = 0; i < size; i++)
		data[i] = (byte)((i * 7 + seed * 13) & 0xFF);
	File.WriteAllBytes(file, data);
}

KList Inputs(params string[] names){
	KList list = new KList();
	foreach (string n in names)
		list.Add(In + "/" + n);
	return list;
}

KList Outputs(params string[] names){
	KList list = new KList();
	foreach (string n in names)
		list.Add(Gen + "/" + Path.GetFileNameWithoutExtension(n) + ".cpp");
	return list;
}

KValue Single(){ return Gen + "/all.cpp"; }

Bin2cpp Silent(){
	Bin2cpp c = new Bin2cpp();
	c.Output = Bin2cpp.OutputMode.Silent;
	c.AbortOnFailure = false;
	return c;
}

// ------------------------------------------------------------------------------------------------
// [1/5] Basics
// ------------------------------------------------------------------------------------------------

void TestBasics(){
	Banner("[1/5] Basics");
	Bin2cpp c = Silent();
	Check("Generate one per file", Show(c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Outputs("a.bin", "b.bin", "c.bin"))), "true");
	Check("generated", Counts(c), "3 generated, 0 up to date, 0 failed");
	Check("symbols", c.Symbols.Count.ToString(), "3");
	string symbol = c.Symbols[".tmp.bin2cpp.in.a.bin"];
	Check("symbol in the source", Show(File.ReadAllText(Gen + "/a.cpp").Contains(symbol + "[]") && File.ReadAllText(Gen + "/a.cpp").Contains(symbol + "_size = 100")), "true");
	Check("records written", Show(File.Exists(Gen + "/a.cpp.kdep")), "true");
	Check("second run", Show(c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Outputs("a.bin", "b.bin", "c.bin"))) + ", " + Counts(c), "true, 0 generated, 3 up to date, 0 failed");
	Check("entries", string.Join(",", c.LastGenerate!.Entries.Select(e => e.Status)), "UpToDate,UpToDate,UpToDate");
	Check("Generate one for all", Show(c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Single())), "true");
	Check("single generated", Counts(c) + ", single " + Show(c.LastGenerate!.Single), "1 generated, 0 up to date, 0 failed, single true");
	Check("every symbol in the source", Show(c.Symbols.Values.All(s => File.ReadAllText(Single()).Contains(s + "[]"))), "true");
	Check("single second run", Show(c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Single())) + ", " + Counts(c), "true, 0 generated, 1 up to date, 0 failed");
	Check("no temporary left", Show(!Directory.EnumerateFiles(Gen, "*.tmp").Any()), "true");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [2/5] Output modes
// ------------------------------------------------------------------------------------------------

void TestOutputModes(){
	Banner("[2/5] Output modes");
	RecordingProgress recorder = new RecordingProgress();
	foreach (Bin2cpp.OutputMode mode in new[] { Bin2cpp.OutputMode.Silent, Bin2cpp.OutputMode.Progress, Bin2cpp.OutputMode.Detailed }){
		recorder.Reset();
		Bin2cpp c = Silent();
		c.Output = mode;
		c.Progress = recorder;
		File.Delete(Gen + "/a.cpp");
		c.Generate(Inputs("a.bin", "b.bin"), Outputs("a.bin", "b.bin"));
		Check("reporter lines in " + mode, recorder.Started + " started, " + recorder.Finished, mode == Bin2cpp.OutputMode.Progress ? "1 started, ok (1 up to date)" : "0 started, ");
	}
	Msg.Print("--- shown on screen: Progress, then Detailed ---");
	Msg.BeginIndent();
	Bin2cpp shown = Silent();
	shown.Output = Bin2cpp.OutputMode.Progress;
	File.Delete(Gen + "/b.cpp");
	shown.Generate(Inputs("a.bin", "b.bin", "c.bin"), Outputs("a.bin", "b.bin", "c.bin"));
	shown.TaskLabel = "Embedding the assets";
	shown.Generate(Inputs("a.bin", "b.bin", "c.bin"), Single());
	shown.Output = Bin2cpp.OutputMode.Detailed;
	File.Delete(Gen + "/c.cpp");
	shown.Generate(Inputs("a.bin", "b.bin", "c.bin"), Outputs("a.bin", "b.bin", "c.bin"));
	Msg.EndIndent();
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [3/5] Nothing left behind
// ------------------------------------------------------------------------------------------------

void TestNothingLeftBehind(){
	Banner("[3/5] Nothing left behind");
	Bin2cpp c = Silent();
	c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Outputs("a.bin", "b.bin", "c.bin"));
	c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Single());
	Case(c, "binary edited", () => Flip(Path.Combine(In, "a.bin")), "a.cpp", true);
	Case(c, "binary edited, date kept", () => FlipKeepingDate(Path.Combine(In, "b.bin")), "b.cpp", true);
	Case(c, "binary edited, dated older", () => { Flip(Path.Combine(In, "c.bin")); File.SetLastWriteTimeUtc(Path.Combine(In, "c.bin"), new DateTime(2000, 1, 1)); }, "c.cpp", true);
	Case(c, "record deleted", () => File.Delete(Gen + "/a.cpp.kdep"), "a.cpp", false);
	Case(c, "output deleted", () => File.Delete(Gen + "/b.cpp"), "b.cpp", false);
	// A path given differently is another symbol: generated again with it
	string before = c.Symbols[".tmp.bin2cpp.in.a.bin"];
	KList other = new KList { "./" + In + "/a.bin" };
	Check("path given differently", Show(c.Generate(other, Outputs("a.bin"))) + ", " + Counts(c) + ", symbol " + (c.Symbols.Values.First() != before ? "changed" : "same"), "true, 1 generated, 0 up to date, 0 failed, symbol changed");
	c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Outputs("a.bin", "b.bin", "c.bin"));
	// The single output follows its list
	Check("file added to the list", Show(c.Generate(Inputs("a.bin", "b.bin", "c.bin", "a.bin"), Single())), "false");
	Check("duplicate rejected", c.LastError.Code.ToString(), "InvalidArgument");
	WriteBinary(Path.Combine(In, "d.bin"), 50, 4);
	Check("file added to the list", Show(c.Generate(Inputs("a.bin", "b.bin", "c.bin", "d.bin"), Single())) + ", " + Counts(c), "true, 1 generated, 0 up to date, 0 failed");
	Check("file removed from the list", Show(c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Single())) + ", " + Counts(c), "true, 1 generated, 0 up to date, 0 failed");
	Check("list reordered", Show(c.Generate(Inputs("c.bin", "b.bin", "a.bin"), Single())) + ", " + Counts(c), "true, 1 generated, 0 up to date, 0 failed");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [4/5] Nothing built without need
// ------------------------------------------------------------------------------------------------

void TestNothingWithoutNeed(){
	Banner("[4/5] Nothing without need");
	Bin2cpp c = Silent();
	c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Outputs("a.bin", "b.bin", "c.bin"));
	c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Single());
	Case(c, "no change", () => { }, "", false);
	Case(c, "binary touched", () => File.SetLastWriteTimeUtc(Path.Combine(In, "a.bin"), DateTime.UtcNow), "", false);
	Case(c, "output touched", () => File.SetLastWriteTimeUtc(Gen + "/b.cpp", DateTime.UtcNow), "", false);
	Case(c, "output dated older than the binary", () => File.SetLastWriteTimeUtc(Gen + "/c.cpp", new DateTime(2000, 1, 1)), "", false);
	Case(c, "settings that do not reach the output", () => { c.TaskLabel = "x"; c.Output = Bin2cpp.OutputMode.Silent; c.TaskLabel = ""; }, "", false);
	Check("list order of one per file", Show(c.Generate(Inputs("c.bin", "b.bin", "a.bin"), Outputs("c.bin", "b.bin", "a.bin"))) + ", " + Counts(c), "true, 0 generated, 3 up to date, 0 failed");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [5/5] Failures
// ------------------------------------------------------------------------------------------------

void TestFailures(){
	Banner("[5/5] Failures");
	Bin2cpp c = Silent();
	Check("missing binary, output absent", Show(c.Generate(Inputs("nope.bin"), Outputs("nope.bin"))) + ", " + c.LastError.Code, "false, NotFound");
	Check("no output written", Show(!File.Exists(Gen + "/nope.cpp")), "true");
	Check("missing binary, single output present", Show(c.Generate(Inputs("a.bin", "nope.bin"), Single())) + ", " + c.LastError.Code, "false, NotFound");
	Check("mismatched lists", Show(c.Generate(Inputs("a.bin", "b.bin"), Outputs("a.bin"))) + ", " + c.LastError.Code, "false, InvalidArgument");
	Check("duplicate name", Show(c.Generate(Inputs("a.bin", "a.bin"), Outputs("a.bin", "b.bin"))) + ", " + c.LastError.Code, "false, InvalidArgument");
	if (Host.IsWindows()){
		// A write that fails: the previous output stays intact and no temporary file is left
		string output = Gen + "/a.cpp";
		c.Generate(Inputs("a.bin"), Outputs("a.bin"));
		string previous = File.ReadAllText(output);
		File.SetAttributes(output, FileAttributes.ReadOnly);
		Flip(Path.Combine(In, "a.bin"));
		bool ok = c.Generate(Inputs("a.bin"), Outputs("a.bin"));
		Check("write failure", Show(ok) + ", " + c.LastError.Code + ", " + c.LastGenerate!.Entries[0].Status, "false, Failed, Failed");
		Check("previous output intact", Show(File.ReadAllText(output) == previous), "true");
		Check("no temporary left", Show(!File.Exists(output + ".tmp")), "true");
		File.SetAttributes(output, FileAttributes.Normal);
		Check("generated once writable", Show(c.Generate(Inputs("a.bin"), Outputs("a.bin"))) + ", " + Counts(c), "true, 1 generated, 0 up to date, 0 failed");
	}
	// The abort, through a child script that returns 1 with the reason
	File.WriteAllText(Path.Combine(Sandbox, "child.csx"), string.Join("\n", new[] {
		"#load \"extensions/bin2cpp.csx\"",
		"int abort(string[] args){",
		"	Bin2cpp c = new Bin2cpp();",
		"	c.Output = Bin2cpp.OutputMode.Silent;",
		"	c.Generate(new KList { \"in/nope.bin\" }, new KList { \"gen/nope.cpp\" });",
		"	return 0;",
		"}",
		""
	}));
	int code = Kombine(Path.Combine(Sandbox, "child.csx"), "abort", null, false);
	Check("abort through a child", code + ", " + (Engine.LastError.Message.Contains("NotFound") ? "reason kept" : Engine.LastError.Message), "1, reason kept");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// Helpers
// ------------------------------------------------------------------------------------------------

/// <summary>
/// Applies one change and generates again, one per file and the single output: exactly the
/// expected outputs must be generated.
/// </summary>
void Case(Bin2cpp c, string name, Action change, string expectedPerFile, bool expectedSingle){
	change();
	bool ok = c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Outputs("a.bin", "b.bin", "c.bin"));
	string perFile = string.Join(",", c.LastGenerate!.Entries.Where(e => e.Status == Bin2cpp.EntryStatus.Generated).Select(e => Path.GetFileName(e.Output)));
	ok &= c.Generate(Inputs("a.bin", "b.bin", "c.bin"), Single());
	bool single = c.LastGenerate!.Generated == 1;
	Check(name, (ok ? "" : "FAILED, ") + "generated [" + perFile + "] single " + Show(single), "generated [" + expectedPerFile + "] single " + Show(expectedSingle));
}

string Counts(Bin2cpp c){
	Bin2cpp.Result r = c.LastGenerate!;
	return r.Generated + " generated, " + r.UpToDate + " up to date, " + r.Failed + " failed";
}

/// <summary>Changes one byte of a file.</summary>
void Flip(string file){
	byte[] data = File.ReadAllBytes(file);
	data[data.Length / 2] ^= 0xFF;
	File.WriteAllBytes(file, data);
}

/// <summary>Appends one byte to a file and restores its date: an edit the date does not show, the size does.</summary>
void FlipKeepingDate(string file){
	DateTime date = File.GetLastWriteTimeUtc(file);
	byte[] data = File.ReadAllBytes(file);
	Array.Resize(ref data, data.Length + 1);
	data[data.Length - 1] = 0x5A;
	File.WriteAllBytes(file, data);
	File.SetLastWriteTimeUtc(file, date);
}

/// <summary>
/// A progress reporter that records the calls it receives.
/// </summary>
class RecordingProgress : ITaskProgress {
	public int Started = 0;
	public string Finished = string.Empty;
	public void Start(string message){ Started++; }
	public void Report(double value, string? status = null){ }
	public void Finish(string message = "", ProgressOutcome outcome = ProgressOutcome.Success){ Finished = message; }
	public void Dispose(){ }
	public void Reset(){ Started = 0; Finished = string.Empty; }
}

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
}

void EndBanner(){
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);
}

void Check(string what, string actual, string expected){
	Msg.PrintTask($"{what,-40} : {actual,-52} ");
	if (actual == expected){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-40} : {expected}");
	}
}
