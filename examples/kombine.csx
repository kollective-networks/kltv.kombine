/*---------------------------------------------------------------------------------------------------------

	Kombine examples runner

	(C)Kollective Networks 2026

	Runs the examples as tests, by groups: test (the engine examples), extensions, extras and all.
	Every example prints its own report, one aligned line per check with an OK or FAILED tag, and
	returns a non-zero code when a check fails; run on its own it ends with its summary line, under
	the runner that line is silent. The runner runs every script whatever the previous ones returned
	and closes the run with one summary: the checks passed and failed per category and, for every
	failure, the category, the group and the check, so a long log is searched only where it failed.
	It returns 1 when any check or script failed.

	The results reach the runner through a list shared with the Share API under "kombine.tests":
	the report helpers of the examples (see mkb.results.csx) append every check to it, child scripts
	included, so the runner needs neither the log nor the message handler, which some examples test.

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.Collections.Generic;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// The summary of the run: the results per category and the scripts that returned an error
class Category {
	public string Name = "";
	public int Passed = 0;
	public int Failed = 0;
	public int Skipped = 0;
	public int Scripts = 0;
	public int ScriptsFailed = 0;
}
List<string> results = new List<string>();
List<Category> categories = new List<Category>();
List<string> failedChecks = new List<string>();
List<string> failedScripts = new List<string>();
bool summaryStarted = false;

/// <summary>
/// Execute all the scripts as test.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check and script passed, 1 otherwise.</returns>
int test(string[] args){
	bool own = Begin();
	Msg.Print("");
	Msg.Print("Testing scripts");
	Msg.Print("");
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Base functions");
	Run("Base functions", "00.base/mkb.version.csx", "test", args);
	Run("Base functions", "00.base/mkb.admin.csx", "test", args);
	Run("Exit codes", "00.base/mkb.exitcodes.csx", "test", args);
	Run("Progress", "00.base/mkb.progress.csx", "test", args);
	Run("Log handler", "00.base/mkb.log.csx", "test", args);
	Run("Modules", "00.base/mkb.modules.csx", "test", args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Simple script (two actions)");
	Run("Simple script", "01.simple/mkb.simple.csx", "build", args);
	Run("Simple script", "01.simple/mkb.simple.csx", "clean", args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Built in types");
	Run("Built in types", "02.types/mkb.types.csx", "test", args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Child scripts");
	Run("Child scripts", "03.child/mkb.child.csx", "test", args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: #load resolution");
	Run("#load resolution", "08.loadresolution/mkb.loadresolution.csx", "test", args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Files & folders & compression");
	Run("Files, folders and compression", "04.folders/mkb.folders.csx", "test", args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Network API");
	Run("Network API", "05.network/mkb.network.csx", "test", args);
	Msg.Print("----------------------------------------------------------");
	return own ? Report() : 0;
}

/// <summary>
/// Execute the extension scripts tests
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check and script passed, 1 otherwise.</returns>
int extensions(string[] args){
	bool own = Begin();
	Msg.Print("");
	Msg.Print("Testing Kombine extensions");
	Msg.Print("");
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: clang");
	Run("clang", "06.extensions/00.clang/mkb.ext.clang.csx", "test", args);
	Run("clang", "06.extensions/00.clang/mkb.ext.clang.csx", "build", args);
	Run("clang", "06.extensions/00.clang/mkb.ext.clang.csx", "cleanall", args);
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: clang doc");
	Run("clang doc", "06.extensions/01.clang.docs/mkb.ext.clang.doc.csx", "doc", args);
	Msg.Print("----------------------------------------------------------");

	Msg.Print("");
	Msg.Print("Testing: git");
	Run("git", "06.extensions/02.git/mkb.ext.git.csx", "test", args);
	Msg.Print("----------------------------------------------------------");

	Msg.Print("");
	Msg.Print("Testing: Bin2cpp");
	Run("bin2cpp", "06.extensions/04.bin2cpp/mkb.ext.bin2cpp.csx", "test", args);
	Run("bin2cpp", "06.extensions/04.bin2cpp/mkb.ext.bin2cpp.csx", "build", args);
	Run("bin2cpp", "06.extensions/04.bin2cpp/mkb.ext.bin2cpp.csx", "clean", args);

	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: Bin2obj");
	Run("bin2obj", "06.extensions/05.bin2obj/mkb.ext.bin2obj.csx", "test", args);
	Run("bin2obj", "06.extensions/05.bin2obj/mkb.ext.bin2obj.csx", "build", args);
	Run("bin2obj", "06.extensions/05.bin2obj/mkb.ext.bin2obj.csx", "clean", args);
	Msg.Print("----------------------------------------------------------");
	return own ? Report() : 0;
}

/// <summary>
/// Execute the extra example scripts tests (sdl2, msys2)
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check and script passed, 1 otherwise.</returns>
int extras(string[] args){
	bool own = Begin();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	Msg.Print("Testing: sdl2");
	Run("sdl2", "07.extras/00.sdl2/sdl2.csx", "build", args);
	Run("sdl2", "07.extras/00.sdl2/sdl2.csx", "clean", args);
	Msg.Print("----------------------------------------------------------");
	if (Host.IsWindows()) {
		Msg.Print("");
		Msg.Print("Testing: msys2");
		Run("msys2", "07.extras/01.msys2/msys2.packages.csx", "test", args);
		Run("msys2", "07.extras/01.msys2/msys2.build.csx", "build", args);
		Run("msys2", "07.extras/01.msys2/msys2.build.csx", "clean", args);
	} else {
		Msg.Print("");
		Msg.Print("Testing: msys2 (skipped on non-Windows platform)");
	}
	return own ? Report() : 0;
}

/// <summary>
/// Execute all the tests in this file: base scripts, extensions and extras, with one summary at the end.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every check and script passed, 1 otherwise.</returns>
int all(string[] args){
	Begin();
	Msg.Print("");
	Msg.Print("Running all tests");
	Msg.Print("");
	test(args);
	extensions(args);
	extras(args);
	Msg.Print("");
	Msg.Print("All tests completed");
	Msg.Print("");
	return Report();
}

// ------------------------------------------------------------------------------------------------
// The summary of the run
// ------------------------------------------------------------------------------------------------

/// <summary>
/// Starts the summary of a run: the results are reset and the list shared with the scripts. A nested
/// call (an action run from "all") changes nothing.
/// </summary>
/// <returns>True when this call started the summary and must report it.</returns>
bool Begin(){
	if (summaryStarted)
		return false;
	summaryStarted = true;
	results.Clear();
	categories.Clear();
	failedChecks.Clear();
	failedScripts.Clear();
	// Shared once per process: the same list is kept under the name from then on
	Share.Set("kombine.tests", results);
	return true;
}

/// <summary>
/// Runs one action of an example script under a category. A failure does not stop the run: the
/// results the script recorded are counted and its exit code kept for the summary.
/// </summary>
/// <param name="what">The category shown in the summary.</param>
/// <param name="script">The script, relative to this folder.</param>
/// <param name="action">The action to run.</param>
/// <param name="args">The arguments of the action.</param>
void Run(string what, string script, string action, string[] args){
	Category? category = categories.Find(c => c.Name == what);
	if (category == null){
		category = new Category { Name = what };
		categories.Add(category);
	}
	category.Scripts++;
	int before = results.Count;
	int code = Kombine(script, action, args, false);
	// The results recorded while it ran: "status|group|text"
	for (int i = before; i < results.Count; i++){
		string[] parts = results[i].Split('|', 3);
		string status = parts[0];
		string group = parts.Length > 1 ? parts[1] : "";
		string text = parts.Length > 2 ? parts[2] : "";
		if (status == "OK")
			category.Passed++;
		else if (status == "SKIPPED")
			category.Skipped++;
		else {
			category.Failed++;
			failedChecks.Add(what + " > " + group + " > " + text);
		}
	}
	if (code != 0){
		string reason = Engine.LastError.IsError ? " (" + Engine.LastError.Message.Split('\n')[0] + ")" : "";
		failedScripts.Add(what + " > " + script + " " + action + " returned " + code + reason);
		category.ScriptsFailed++;
	}
}

/// <summary>
/// Prints the summary of the run: the totals, one line per category and the failures.
/// </summary>
/// <returns>0 if every check and script passed, 1 otherwise.</returns>
int Report(){
	summaryStarted = false;
	int passed = 0, failed = 0, scripts = 0;
	foreach (Category c in categories){
		passed += c.Passed;
		failed += c.Failed;
		scripts += c.Scripts;
	}
	bool ok = failed == 0 && failedScripts.Count == 0;
	Msg.Print("");
	Msg.Print("==========================================================");
	Msg.PrintTask($"Test summary : {passed} ok, {failed} failed, {scripts} scripts ");
	if (ok)
		Msg.PrintTaskSuccess("OK");
	else
		Msg.PrintTaskError(FailedTag(failed, failedScripts.Count));
	// One line per category, so a failure is located at a glance
	Msg.BeginIndent();
	foreach (Category c in categories){
		string skipped = c.Skipped > 0 ? $", {c.Skipped} skipped" : "";
		Msg.PrintTask($"{c.Name,-32} : {c.Passed} ok, {c.Failed} failed{skipped} ");
		if (c.Failed == 0 && c.ScriptsFailed == 0)
			Msg.PrintTaskSuccess("OK");
		else
			Msg.PrintTaskError(FailedTag(c.Failed, c.ScriptsFailed));
	}
	if (!ok){
		Msg.RawPrint(Environment.NewLine);
		foreach (string f in failedChecks)
			Msg.PrintError(f);
		foreach (string f in failedScripts)
			Msg.PrintError(f);
	}
	Msg.EndIndent();
	Msg.Print("==========================================================");
	Msg.Print("");
	return ok ? 0 : 1;
}

/// <summary>
/// The tag of a failure: the checks and the scripts that failed, in words.
/// </summary>
string FailedTag(int checks, int scripts){
	List<string> parts = new List<string>();
	if (checks > 0)
		parts.Add(checks + " check" + (checks == 1 ? "" : "s"));
	if (scripts > 0)
		parts.Add(scripts + " script" + (scripts == 1 ? "" : "s"));
	return string.Join(" and ", parts) + " FAILED";
}
