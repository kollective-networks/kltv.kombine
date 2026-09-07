/*---------------------------------------------------------------------------------------------------------

	Kombine Base Exit Code Contract Tests

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing exit code contracts");
	Msg.BeginIndent();

	string mkbBinary = CurrentToolFolder + (Host.IsWindows() ? "/mkb.exe" : "/mkb");
	string tempFolder = CurrentWorkingFolder + "/.tmp.exitcodes";
	const int ExitCodeFailure = 1;
	if (Folders.Exists(tempFolder)) {
		Folders.Delete(tempFolder, true);
	}
	Folders.Create(tempFolder);

	string scriptPass = tempFolder + "/pass.csx";
	string scriptReturn = tempFolder + "/ret7.csx";
	string scriptAbort = tempFolder + "/abort.csx";
	string scriptThrow = tempFolder + "/throw.csx";
	string scriptVoidReturn = tempFolder + "/voidreturn.csx";
	string scriptNoAction = tempFolder + "/noaction.csx";

	Files.WriteTextFile(scriptPass,
		"int pass(string[] args){ Msg.Print(\"pass\"); return 0; }");
	Files.WriteTextFile(scriptReturn,
		"int ret7(string[] args){ return 7; }");
	Files.WriteTextFile(scriptAbort,
		"int abort(string[] args){ Msg.PrintAndAbort(\"forced abort\"); return 0; }");
	Files.WriteTextFile(scriptThrow,
		"int thrower(string[] args){ throw new Exception(\"forced throw\"); }");
	Files.WriteTextFile(scriptVoidReturn,
		"void voidret(string[] args){ Msg.Print(\"void return\"); }");
	Files.WriteTextFile(scriptNoAction,
		"int someaction(string[] args){ return 0; }");

	ExpectExitCode("successful script returns success", Exec(mkbBinary, new string[] { "-kfile:" + scriptPass, "pass" }, true), 0);
	ExpectExitCode("explicit return value is preserved", Exec(mkbBinary, new string[] { "-kfile:" + scriptReturn, "ret7" }, true), 7);
	ExpectExitCode("PrintAndAbort normalizes to generic failure", Exec(mkbBinary, new string[] { "-kfile:" + scriptAbort, "abort" }, true), ExitCodeFailure);
	ExpectExitCode("unhandled script exception normalizes to generic failure", Exec(mkbBinary, new string[] { "-kfile:" + scriptThrow, "thrower" }, true), ExitCodeFailure);
	ExpectExitCode("invalid action return type normalizes to generic failure", Exec(mkbBinary, new string[] { "-kfile:" + scriptVoidReturn, "voidret" }, true), ExitCodeFailure);
	ExpectExitCode("kconfig returns failure while unimplemented", Exec(mkbBinary, "kconfig", true), ExitCodeFailure);
	ExpectExitCode("kcache help returns success", Exec(mkbBinary, new string[] { "kcache", "help" }, true), 0);
	ExpectExitCode("kcache without subcommand returns failure", Exec(mkbBinary, "kcache", true), ExitCodeFailure);
	ExpectExitCode("kcache unknown subcommand returns failure", Exec(mkbBinary, new string[] { "kcache", "invalid" }, true), ExitCodeFailure);
	ExpectExitCode("no action defaults to help success", Exec(mkbBinary, new string[] { "-kfile:" + scriptNoAction }, true), 0);
	ExpectExitCode("unknown action returns failure", Exec(mkbBinary, new string[] { "-kfile:" + scriptNoAction, "doesnotexist" }, true), ExitCodeFailure);
	ExpectExitCode("missing script file returns failure", Exec(mkbBinary, new string[] { "-kfile:" + tempFolder + "/missing.csx", "any" }, true), ExitCodeFailure);

	Folders.Delete(tempFolder, true);

	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	return 0;
}

void ExpectExitCode(string testName, int actual, int expected){
	if (actual != expected){
		Msg.PrintAndAbort($"{testName}: expected exit code {expected}, got {actual}");
	}
	Msg.Print($"{testName}: {actual}");
}
