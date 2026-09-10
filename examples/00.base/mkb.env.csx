/*---------------------------------------------------------------------------------------------------------

	Kombine Environment Example

	(C)Kollective Networks 2026

	Env is the environment the tools and the child scripts receive. This example exercises it:
	variables set, read, removed and cleaned by list or wildcard, the case of the names (ignored on
	Windows, exact elsewhere), snapshots restored, the path entries, where a tool resolves, the
	requirements, and a batch file or shell script loaded into the environment and seen by a tool.

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
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
string Sandbox = ".tmp.env";

/// <summary>
/// Runs every group.
/// </summary>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing the environment");
	Msg.BeginIndent();
	Directory.CreateDirectory(Sandbox);
	TestVariables();
	TestSnapshot();
	TestPaths();
	TestLoad();
	Folders.Delete(Sandbox, true);
	TestSummary(passed, failed);
	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	return failed == 0 ? 0 : 1;
}

/// <summary>
/// Variables: set, get, has, remove, clean by list and wildcard, the case of the names, require and forbid.
/// </summary>
void TestVariables(){
	Banner("[1/4] Variables");
	Check("Set", Show(Env.Set("KMB_TEST_ONE", "one")), "true");
	Check("Get", Env.Get("KMB_TEST_ONE"), "one");
	Check("Has", Show(Env.Has("KMB_TEST_ONE")), "true");
	Check("Get missing", Quote(Env.Get("KMB_TEST_MISSING")), "\"\"");
	Check("Get missing with default", Env.Get("KMB_TEST_MISSING", "fallback"), "fallback");
	Check("Import sees it", KValue.Import("KMB_TEST_ONE", ""), "one");
	// The case of the names follows the platform: one variable on Windows, two elsewhere
	Check("case of the names", Show(Env.Has("kmb_test_one")), Show(Host.IsWindows()));
	Check("Set empty name", Show(Env.Set("", "x")) + ", " + Env.LastError.Code, "false, InvalidArgument");
	Check("Remove", Show(Env.Remove("KMB_TEST_ONE")), "true");
	Check("Remove again", Show(Env.Remove("KMB_TEST_ONE")), "false");
	// Clean takes the list of the caller: names and wildcards, and returns what it found
	Env.Set("KMB_CLEAN_A", "a");
	Env.Set("KMB_CLEAN_B", "b");
	Env.Set("KMB_KEEP", "k");
	KList removed = Env.Clean("KMB_CLEAN_*", "KMB_NOT_THERE", "KMB_KEEP");
	Check("Clean removed", removed.Count().ToString(), "3");
	Check("Clean wildcard", Show(!Env.Has("KMB_CLEAN_A") && !Env.Has("KMB_CLEAN_B")), "true");
	Check("Clean by name", Show(Env.Has("KMB_KEEP")), "false");
	// Require and Forbid without the abort
	Check("Require present", Show(Env.Require("PATH", false)), "true");
	Check("Require missing", Show(Env.Require("KMB_TEST_MISSING", false)) + ", " + Env.LastError.Code, "false, NotFound");
	Check("Forbid missing", Show(Env.Forbid("KMB_TEST_MISSING", false)), "true");
	Check("Forbid present", Show(Env.Forbid("PATH", false)) + ", " + Env.LastError.Code, "false, AlreadyExists");
	EndBanner();
}

/// <summary>
/// Snapshots: the environment put back as it was.
/// </summary>
void TestSnapshot(){
	Banner("[2/4] Snapshot and restore");
	Dictionary<string, string> before = Env.Snapshot();
	int count = before.Count;
	Env.Set("KMB_SNAP", "yes");
	Env.Set("KMB_SNAP_TWO", "yes");
	Env.Remove("PATHEXT");
	Check("changed", Show(Env.Has("KMB_SNAP") && !Env.Has("PATHEXT") && Env.Snapshot().Count == count + 1), "true");
	Check("Restore", Show(Env.Restore(before)), "true");
	Check("restored", Show(!Env.Has("KMB_SNAP") && Env.Has("PATHEXT") && Env.Snapshot().Count == count), "true");
	Check("Restore null", Show(Env.Restore(null)) + ", " + Env.LastError.Code, "false, InvalidArgument");
	EndBanner();
}

/// <summary>
/// The path entries: prepend, append, remove by wildcard, and where a tool resolves.
/// </summary>
void TestPaths(){
	Banner("[3/4] Path entries and tools");
	Dictionary<string, string> before = Env.Snapshot();
	string folder = Path.GetFullPath(Path.Combine(Sandbox, "bin"));
	Directory.CreateDirectory(folder);
	int count = Env.Paths().Count();
	Check("PrependPath", Show(Env.PrependPath(folder)), "true");
	Check("first entry", Unix(Env.Paths()[0]), Unix(folder));
	Check("PrependPath again", Show(Env.PrependPath(folder) && Env.Paths().Count() == count + 1), "true");
	string other = Path.GetFullPath(Path.Combine(Sandbox, "other"));
	Directory.CreateDirectory(other);
	Check("AppendPath", Show(Env.AppendPath(other) && Unix(Env.Paths()[Env.Paths().Count() - 1]) == Unix(other)), "true");
	// A tool put in the folder resolves there first
	string tool = Host.IsWindows() ? "kmbtesttool.cmd" : "kmbtesttool";
	File.WriteAllText(Path.Combine(folder, tool), Host.IsWindows() ? "@echo off\r\necho tool\r\n" : "#!/bin/sh\necho tool\n");
	if (!Host.IsWindows())
		Files.SetExecutable(Path.Combine(folder, tool));
	KList found = Env.Which("kmbtesttool");
	Check("Which finds it", found.Count().ToString() + ", " + (found.Count() > 0 ? Unix(Path.GetDirectoryName(found[0]) ?? "") : ""), "1, " + Unix(folder));
	Check("Which finds mkb", Show(Env.Which("mkb").Count() > 0), "true");
	Check("Which missing", Env.Which("kmb.no.such.tool").Count().ToString(), "0");
	KList removedPaths = Env.RemovePath("*" + Path.DirectorySeparatorChar + ".tmp.env" + Path.DirectorySeparatorChar + "*");
	Check("RemovePath", removedPaths.Count().ToString(), "2");
	Check("entries back", Env.Paths().Count().ToString(), count.ToString());
	Env.Restore(before);
	EndBanner();
}

/// <summary>
/// Load: a batch file or a shell script run in a child, its variables brought in, seen by a tool.
/// </summary>
void TestLoad(){
	Banner("[4/4] Load from a script file");
	Dictionary<string, string> before = Env.Snapshot();
	Env.Set("KMB_GONE", "still here");
	string file;
	if (Host.IsWindows()){
		file = Path.Combine(Sandbox, "env.bat");
		File.WriteAllText(file, "@echo off\r\nset KMB_LOADED=yes\r\nset KMB_ARG=%1\r\nset KMB_GONE=\r\n");
	} else {
		file = Path.Combine(Sandbox, "env.sh");
		File.WriteAllText(file, "KMB_LOADED=yes\nexport KMB_LOADED\nKMB_ARG=$1\nexport KMB_ARG\nunset KMB_GONE\n");
	}
	Check("Load", Show(Env.Load(file, "hello")), "true");
	Check("variable loaded", Env.Get("KMB_LOADED"), "yes");
	Check("argument received", Env.Get("KMB_ARG"), "hello");
	Check("variable removed by the file", Show(Env.Has("KMB_GONE")), "false");
	Check("LastLoaded", Show(Env.LastLoaded.Contains("KMB_LOADED") && Env.LastLoaded.Contains("KMB_ARG") && Env.LastLoaded.Contains("KMB_GONE")), "true");
	// A tool receives the environment as it is now
	Tool echo = new Tool("echo");
	echo.CaptureOutput = false;
	ToolResult r = Host.IsWindows() ? echo.CommandSync("cmd.exe", "/d /c echo %KMB_LOADED%-%KMB_ARG%", null) : echo.CommandSync("/bin/sh", "-c \"echo $KMB_LOADED-$KMB_ARG\"", null);
	Check("tool sees it", string.Join("", r.Stdout).Trim(), "yes-hello");
	Check("Load missing file", Show(Env.Load(Path.Combine(Sandbox, "nope.bat"))) + ", " + Env.LastError.Code, "false, NotFound");
	// A file that fails changes nothing
	string failing = Path.Combine(Sandbox, Host.IsWindows() ? "fail.bat" : "fail.sh");
	File.WriteAllText(failing, Host.IsWindows() ? "@echo off\r\nset KMB_NEVER=1\r\nexit /b 3\r\n" : "KMB_NEVER=1\nexport KMB_NEVER\nreturn 3\n");
	Check("Load failing file", Show(Env.Load(failing)) + ", " + Env.LastError.Code, "false, Failed");
	Check("nothing changed", Show(Env.Has("KMB_NEVER")), "false");
	Env.Restore(before);
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// Helpers
// ------------------------------------------------------------------------------------------------

void Banner(string title){
	Msg.Print(title);
	Msg.BeginIndent();
	TestGroup(title);
}

void EndBanner(){
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);
}

string Show(bool value){
	return value ? "true" : "false";
}

string Quote(KValue value){
	return "\"" + value + "\"";
}

string Unix(string value){
	return value.Replace('\\', '/');
}

void Check(string what, string actual, string expected){
	Msg.PrintTask($"{what,-30} : {actual,-44} ");
	if (actual == expected){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-30} : {expected}");
	}
	TestResult(actual == expected ? "OK" : "FAILED", what + " : " + actual);
}
