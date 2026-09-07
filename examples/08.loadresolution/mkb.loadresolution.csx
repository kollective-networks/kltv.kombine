/*---------------------------------------------------------------------------------------------------------

	Kombine #load Resolution Example

	(C)Kollective Networks 2026

	#load resolution regression test.

	Layout: two repos share the same relative helper layout (scripts/build/helper.csx) and
	repo-b is embedded inside repo-a's dependency folder (repo-a/ext/repo-b), like a build
	that clones a library repo below itself:

		repo-a/kombine.csx                        #load "scripts/build/helper.csx"
		repo-a/scripts/build/helper.csx           owner: repo-a
		repo-a/ext/mod.csx                        #load "scripts/build/helper.csx"
		repo-a/ext/repo-b/kombine.csx             #load "scripts/build/helper.csx"
		repo-a/ext/repo-b/scripts/build/helper.csx owner: repo-b
		repo-a/ext/repo-b/ext/mod.csx             #load "scripts/build/helper.csx"

	Every script must bind its OWN repo's helper. Each one registers the owner of the helper it
	bound in the run wide registry and this script verifies it. The retired recursive forward
	search used to make repo-a/ext/mod.csx bind repo-b's helper (first match walking the
	subfolders of repo-a/ext), and the state cache then persisted the wrong bind.

	Run with: mkb -ksrb -kfile:mkb.loadresolution.csx test
	(the rebuild flag forces real compiles, resolution happens there)

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;
using static Kltv.Kombine.Api.Statics;

int passed = 0;
int failed = 0;

/// <summary>
/// Runs the check action of every script in the layout and verifies the helper each one bound.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every script bound its own helper, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing #load resolution");
	Msg.BeginIndent();
	Msg.Print("Every script loads \"scripts/build/helper.csx\" and must bind the helper of its own repo");
	Msg.Print("Layout  : repo-a (helper owner repo-a) with repo-b embedded in repo-a/ext (helper owner repo-b)");
	Msg.Print("Hint    : run with -ksrb to force the compile of every script, the resolution happens there");
	Msg.RawPrint(Environment.NewLine);

	// Script to run, owner of the helper it must bind and the resolution step involved
	var cases = new (string Script, string Expected, string Step)[] {
		("repo-a/kombine.csx",            "repo-a", "root script, resolved through the script folder"),
		("repo-a/ext/repo-b/kombine.csx", "repo-b", "root script of the embedded repo, resolved through the script folder"),
		("repo-a/ext/mod.csx",            "repo-a", "subfolder script, backward step to repo-a (the regression case)"),
		("repo-a/ext/repo-b/ext/mod.csx", "repo-b", "subfolder script of the embedded repo, backward step to repo-b"),
	};
	for (int i = 0; i < cases.Length; i++){
		var c = cases[i];
		Msg.Print($"[{i + 1}/{cases.Length}] {c.Script}");
		Msg.BeginIndent();
		Msg.Print("case           : " + c.Step);
		// The script registers the owner of the helper it bound under its own path
		int code = Kombine(c.Script, "check", new string[] { c.Script }, false);
		Check("check returned", code.ToString(), "0");
		Check("bound helper", Share.Registry("loadresolution", c.Script), c.Expected);
		Msg.EndIndent();
		Msg.RawPrint(Environment.NewLine);
	}

	int total = passed + failed;
	Msg.PrintTask($"Summary : {passed} of {total} checks passed ");
	if (failed == 0)
		Msg.PrintTaskSuccess("OK");
	else
		Msg.PrintTaskError($"{failed} FAILED");

	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	if (failed != 0){
		Msg.PrintError($"#load resolution tests failed: {failed} of {total}");
		return 1;
	}
	return 0;
}

/// <summary>
/// Compares the actual value against the expected one and prints an aligned line with a colored
/// OK / FAILED tag. On failure the expected value is printed below in red.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="actual">Actual value.</param>
/// <param name="expected">Expected value.</param>
void Check(string what, string actual, string expected){
	Msg.PrintTask($"{what,-14} : {actual,-20} ");
	if (actual == expected){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-14} : {expected}");
	}
}
