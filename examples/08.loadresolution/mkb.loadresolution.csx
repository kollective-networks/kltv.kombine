/*---------------------------------------------------------------------------------------------------------

	Kombine #load Resolution Example

	(C)Kollective Networks 2026

	#load resolution regression test.

	Layout: two repos share the same relative helper layout (scripts/build/helper.csx) and
	repo-b is embedded inside repo-a's dependency folder (repo-a/ext/repo-b), like a build
	that clones a library repo below itself:

		repo-a/kombine.csx                         #load "scripts/build/helper.csx"
		repo-a/scripts/build/helper.csx            owner: repo-a
		repo-a/ext/mod.csx                         #load "scripts/build/helper.csx"
		repo-a/ext/repo-b/kombine.csx              #load "scripts/build/helper.csx"
		repo-a/ext/repo-b/scripts/build/helper.csx owner: repo-b
		repo-a/ext/repo-b/ext/mod.csx              #load "scripts/build/helper.csx"
		repo-a/forward.csx                         #load "forward/helper.forward.csx"
		repo-a/scripts/forward/helper.forward.csx  owner: forward, only the forward search finds it

	Every script must bind its OWN repo's helper. Each one registers the owner of the helper it
	bound in the run wide registry and this script verifies it. A forward search run before the
	backward step used to make repo-a/ext/mod.csx bind repo-b's helper (first match walking the
	subfolders of repo-a/ext), and the state cache then persisted the wrong bind.

	The checks run with the forward search disabled (the default) and enabled (Engine.ForwardSearch,
	the script side of -kforward): the regular references bind the same helper in both modes since
	the forward search is the last step. The forward only reference fails without it, and the
	engine stays silent: the child returns 1 and Engine.LastError carries the reason, which this
	script prints as its own message. With it, the reference binds. The children are rebuilt for
	every check (Engine.RebuildScripts) because the resolution happens when a script is compiled.

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
/// Runs the check action of every script in the layout, in both forward search modes, and
/// verifies the helper each one bound.
/// </summary>
/// <param name="args"></param>
/// <returns>0 if every script bound the expected helper, 1 otherwise.</returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing #load resolution");
	Msg.BeginIndent();
	bool launchedForward = Engine.ForwardSearch;
	bool launchedRebuild = Engine.RebuildScripts;
	Msg.Print("Every script loads \"scripts/build/helper.csx\" and must bind the helper of its own repo");
	Msg.Print("Layout  : repo-a (helper owner repo-a) with repo-b embedded in repo-a/ext (helper owner repo-b)");
	Msg.Print("Launched: forward search " + (launchedForward ? "enabled (-kforward)" : "disabled") + ", the checks below run both modes");
	Msg.Print("Scripts : rebuilt for every check, the resolution runs when a script is compiled");
	Msg.RawPrint(Environment.NewLine);

	// The resolution happens at compile time: the children are rebuilt so every check resolves again
	Engine.RebuildScripts = true;

	// Script to run, owner of the helper it must bind and the resolution step involved
	var cases = new (string Script, string Expected, string Step)[] {
		("repo-a/kombine.csx",            "repo-a", "root script, resolved through the script folder"),
		("repo-a/ext/repo-b/kombine.csx", "repo-b", "root script of the embedded repo, resolved through the script folder"),
		("repo-a/ext/mod.csx",            "repo-a", "subfolder script, backward step to repo-a (the regression case)"),
		("repo-a/ext/repo-b/ext/mod.csx", "repo-b", "subfolder script of the embedded repo, backward step to repo-b"),
	};
	bool[] modes = { false, true };
	for (int m = 0; m < modes.Length; m++){
		Engine.ForwardSearch = modes[m];
		string mode = modes[m] ? "enabled" : "disabled";
		Msg.Print($"[{m + 1}/{modes.Length}] Forward search {mode}" + (modes[m] ? " (-kforward)" : " (the default)"));
		Msg.BeginIndent();
		// The regular references resolve before the forward step, so they bind the same helper in both modes
		foreach (var c in cases){
			Msg.Print(c.Script);
			Msg.BeginIndent();
			Msg.Print("case           : " + c.Step);
			// The script registers the owner of the helper it bound under its own path and mode
			string key = c.Script + "#" + mode;
			int code = Kombine(c.Script, "check", new string[] { key }, false);
			Check("check returned", code.ToString(), "0");
			Check("bound helper", Share.Registry("loadresolution", key), c.Expected);
			Msg.EndIndent();
			Msg.RawPrint(Environment.NewLine);
		}
		// A reference only the forward search can satisfy: fails without it, binds with it
		Msg.Print("repo-a/forward.csx");
		Msg.BeginIndent();
		Msg.Print("case           : reference that only the forward search can satisfy");
		string forwardKey = "repo-a/forward.csx#" + mode;
		int forwardCode = Kombine("repo-a/forward.csx", "check", new string[] { forwardKey }, false);
		if (modes[m]){
			Check("check returned", forwardCode.ToString(), "0");
			Check("bound helper", Share.Registry("loadresolution", forwardKey), "forward");
			Check("engine error", Engine.LastError.Code.ToString(), "None");
		} else {
			// The engine does not print anything: the failure comes back as the return code and the
			// reason in Engine.LastError, so the message below is this script's own
			Check("check returned", forwardCode.ToString(), "1");
			Check("bound helper", Quote(Share.Registry("loadresolution", forwardKey)), "\"\"");
			Check("engine error", Engine.LastError.Code.ToString(), "NotFound");
			Verify("reason text", Engine.LastError.Message.Contains("forward search"), "names the forward search");
			ShowReason(Engine.LastError);
		}
		Msg.EndIndent();
		Msg.RawPrint(Environment.NewLine);
		Msg.EndIndent();
	}

	// Restore the settings the tool was launched with
	Engine.ForwardSearch = launchedForward;
	Engine.RebuildScripts = launchedRebuild;

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

/// <summary>
/// Prints an aligned line with a colored OK / FAILED tag for a condition, showing what was verified.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="ok">Result of the condition.</param>
/// <param name="shown">What the condition verifies, shown as the value.</param>
void Verify(string what, bool ok, string shown){
	Msg.PrintTask($"{what,-14} : {shown,-20} ");
	if (ok){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
	}
}

/// <summary>
/// Prints the reason of an engine failure as this script's own message, one line per row.
/// </summary>
/// <param name="error">Error reported by the engine.</param>
void ShowReason(ApiError error){
	string[] lines = error.Message.Split('\n');
	Msg.Print($"{"reason",-14} : {lines[0]}");
	for (int i = 1; i < lines.Length; i++)
		Msg.Print($"{"",-14}   {lines[i]}");
}

/// <summary>
/// Renders a value between quotes, so an empty value is visible.
/// </summary>
/// <param name="value">Value to render.</param>
/// <returns>The quoted value.</returns>
string Quote(KValue value){
	return "\"" + value + "\"";
}
