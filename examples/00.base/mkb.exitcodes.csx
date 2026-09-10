/*---------------------------------------------------------------------------------------------------------

	Kombine Base Exit Code Contract Tests

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
// The results of the checks, for the examples runner
#load "mkb.results.csx"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using System.IO;
using System.Collections.Generic;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

/// <summary>
/// Verifies the exit code contract of the tool: every launch mode (script return, abort, exception,
/// invalid action, builtin actions, missing files...) must map to the documented exit code.
/// Every case runs the tool as a child process with its output captured, so nothing leaks to the console.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every contract holds, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing exit code contracts");
	Msg.BeginIndent();

	const int ExitCodeSuccess = 0;
	const int ExitCodeFailure = 1;
	string mkbBinary = CurrentToolFolder + (Host.IsWindows() ? "/mkb.exe" : "/mkb");
	string tempFolder = CurrentWorkingFolder + "/.tmp.exitcodes";

	// Fresh sandbox with the helper scripts used by the cases
	if (Folders.Exists(tempFolder))
		Folders.Delete(tempFolder, true);
	Folders.Create(tempFolder);
	Files.WriteTextFile(tempFolder + "/pass.csx",       "int pass(string[] args){ Msg.Print(\"pass\"); return 0; }");
	Files.WriteTextFile(tempFolder + "/ret7.csx",       "int ret7(string[] args){ return 7; }");
	Files.WriteTextFile(tempFolder + "/abort.csx",      "int abort(string[] args){ Msg.PrintAndAbort(\"forced abort\"); return 0; }");
	Files.WriteTextFile(tempFolder + "/throw.csx",      "int thrower(string[] args){ throw new Exception(\"forced throw\"); }");
	Files.WriteTextFile(tempFolder + "/voidreturn.csx", "void voidret(string[] args){ Msg.Print(\"void return\"); }");
	Files.WriteTextFile(tempFolder + "/noaction.csx",   "int someaction(string[] args){ return 0; }");

	// Name, tool arguments and expected exit code of every contract
	var cases = new List<(string Name, string[] Args, int Expected)> {
		("Successful script returns success",                       new[] { "-kfile:" + tempFolder + "/pass.csx", "pass" },              ExitCodeSuccess),
		("Explicit return value is preserved",                      new[] { "-kfile:" + tempFolder + "/ret7.csx", "ret7" },              7),
		("PrintAndAbort normalizes to generic failure",             new[] { "-kfile:" + tempFolder + "/abort.csx", "abort" },            ExitCodeFailure),
		("Unhandled script exception normalizes to generic failure", new[] { "-kfile:" + tempFolder + "/throw.csx", "thrower" },          ExitCodeFailure),
		("Invalid action return type normalizes to generic failure", new[] { "-kfile:" + tempFolder + "/voidreturn.csx", "voidret" },     ExitCodeFailure),
		("kconfig returns failure while unimplemented",             new[] { "kconfig" },                                                 ExitCodeFailure),
		("kcache help returns success",                             new[] { "kcache", "help" },                                          ExitCodeSuccess),
		("kcache without subcommand returns failure",               new[] { "kcache" },                                                  ExitCodeFailure),
		("kcache unknown subcommand returns failure",               new[] { "kcache", "invalid" },                                       ExitCodeFailure),
		("No action defaults to help success",                      new[] { "-kfile:" + tempFolder + "/noaction.csx" },                  ExitCodeSuccess),
		("Unknown action returns failure",                          new[] { "-kfile:" + tempFolder + "/noaction.csx", "doesnotexist" },  ExitCodeFailure),
		("Missing script file returns failure",                     new[] { "-kfile:" + tempFolder + "/missing.csx", "any" },            ExitCodeFailure),
	};

	Msg.Print("Tool    : " + Path.GetFullPath(mkbBinary));
	Msg.Print("Sandbox : " + Path.GetFullPath(tempFolder));
	Msg.RawPrint(Environment.NewLine);

	int passed = 0;
	int failed = 0;
	for (int i = 0; i < cases.Count; i++){
		var c = cases[i];
		Msg.Print($"[{i + 1:00}/{cases.Count:00}] {c.Name}");
		Msg.BeginIndent();
		TestGroup($"[{i + 1:00}/{cases.Count:00}] {c.Name}");
		// Child output is collected but never echoed, keeping the report clean
		Tool tool = new Tool("exitcodes");
		tool.CaptureOutput = false;
		tool.ExpectedExitCode = c.Expected;
		ToolResult result = tool.CommandSync(mkbBinary, c.Args, null);
		Msg.Print("command  : mkb " + string.Join(" ", c.Args).Replace(tempFolder + "/", ""));
		Msg.PrintTask($"exitcode : {("expected " + c.Expected + ", got " + result.ExitCode),-24}");
		if (result.ExitCode == c.Expected){
			Msg.PrintTaskSuccess("OK");
			passed++;
		} else {
			Msg.PrintTaskError("FAILED");
			failed++;
			PrintChildOutput(result);
		}
		TestResult(result.ExitCode == c.Expected ? "OK" : "FAILED", "exitcode : expected " + c.Expected + ", got " + result.ExitCode);
		Msg.EndIndent();
		Msg.RawPrint(Environment.NewLine);
	}

	Folders.Delete(tempFolder, true);

	TestSummary(passed, failed, 0, "contracts verified");

	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failed != 0){
		Msg.PrintError($"Exit code contract tests failed: {failed} of {cases.Count}");
		return ExitCodeFailure;
	}
	return ExitCodeSuccess;
}

/// <summary>
/// Prints the captured output of a failed case, so the reason is visible without polluting the passing ones.
/// </summary>
/// <param name="result">Result of the child tool execution.</param>
void PrintChildOutput(ToolResult result){
	const int maxLines = 10;
	// Normalize line endings and drop blank lines so only meaningful output is shown
	List<string> lines = new List<string>();
	foreach (string line in result.Stdout) {
		string clean = line.TrimEnd('\r', '\n');
		if (!string.IsNullOrWhiteSpace(clean))
			lines.Add(clean);
	}
	foreach (string line in result.Stderr) {
		string clean = line.TrimEnd('\r', '\n');
		if (!string.IsNullOrWhiteSpace(clean))
			lines.Add(clean);
	}
	if (lines.Count == 0)
		return;
	Msg.Print("output   :");
	Msg.BeginIndent();
	for (int i = 0; i < lines.Count && i < maxLines; i++)
		Msg.Print("| " + lines[i]);
	if (lines.Count > maxLines)
		Msg.Print($"| ... ({lines.Count - maxLines} more lines)");
	Msg.EndIndent();
}
