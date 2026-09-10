/*---------------------------------------------------------------------------------------------------------

	Kombine Environment Extension Example: MSVC on Windows

	(C)Kollective Networks 2026

	Test of the env.win.msvc extension (extensions/env.win.msvc.csx):

		[1/4] Discovery       what the machine has, listed once on screen; Find with nothing required
		[2/4] Apply           the environment set from what is installed, seen by Which, and put back
		[3/4] Requirements    a requirement met is honored, one not met fails with a warning and touches
		                      nothing, SetUnspecified false sets only what was asked
		[4/4] Elsewhere       outside Windows every verb says NotSupported

	The machine may have no Visual Studio at all: the checks that need one are skipped then, and
	the ones that need nothing run everywhere.

		mkb test     runs every group

---------------------------------------------------------------------------------------------------------*/
#load "extensions/env.win.msvc.csx"

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

int passed = 0;
int failed = 0;
int skipped = 0;

/// <summary>
/// Runs every group.
/// </summary>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing the env.win.msvc extension");
	Msg.BeginIndent();
	if (Host.IsWindows()){
		TestDiscovery();
		TestApply();
		TestRequirements();
	} else {
		TestElsewhere();
	}
	TestSummary(passed, failed, skipped);
	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	return failed == 0 ? 0 : 1;
}

/// <summary>
/// An instance that returns instead of aborting.
/// </summary>
EnvWinMsvc Quiet(){
	EnvWinMsvc e = new EnvWinMsvc();
	e.AbortOnFailure = false;
	return e;
}

// ------------------------------------------------------------------------------------------------
// [1/4] Discovery
// ------------------------------------------------------------------------------------------------

void TestDiscovery(){
	Banner("[1/4] Discovery");
	EnvWinMsvc e = Quiet();
	Msg.Print("--- shown on screen: what this machine has ---");
	Msg.BeginIndent();
	e.List();
	Msg.EndIndent();
	List<VsInstance> instances = e.Instances();
	List<WinSdk> sdks = e.Sdks();
	Check("instances sorted newest first", Show(instances.Count < 2 || instances.Zip(instances.Skip(1), (a, b) => string.Compare(a.Version, b.Version, StringComparison.Ordinal) >= 0 || a.Version.Length == 0 || b.Version.Length == 0).All(x => x)), "true");
	Check("every instance has a source", Show(instances.All(i => i.Sources.Count > 0)), "true");
	Check("every SDK has windows.h", Show(sdks.All(s => File.Exists(Path.Combine(s.Path, "Include", s.Version, "um", "windows.h")))), "true");
	// Nothing required: nothing can fail, whatever is installed
	Check("Find with nothing required", Show(e.Find()) + ", " + e.LastError.Code, "true, None");
	Check("LastEnvironment set", Show(e.LastEnvironment != null), "true");
	Check("selection matches the machine", Show((e.LastEnvironment!.HasToolset == instances.Any(i => i.Toolsets.Count > 0)) || !e.LastEnvironment.HasToolset), "true");
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [2/4] Apply
// ------------------------------------------------------------------------------------------------

void TestApply(){
	Banner("[2/4] Apply and restore");
	EnvWinMsvc e = Quiet();
	Dictionary<string, string> before = Env.Snapshot();
	Env.Set("INCLUDE", "C:\\stale\\include");
	Env.Set("VSCMD_VER", "stale");
	Check("Apply", Show(e.Apply()) + ", " + e.LastError.Code, "true, None");
	EnvWinMsvcResult r = e.LastEnvironment!;
	Check("stale variables removed", Show(r.Removed.Contains("INCLUDE") && r.Removed.Contains("VSCMD_VER") && !Env.Has("VSCMD_VER")), "true");
	if (r.HasToolset){
		Check("VCToolsInstallDir set", Show(Env.Get("VCToolsInstallDir").ToString().StartsWith(r.ToolsetPath, StringComparison.OrdinalIgnoreCase)), "true");
		Check("INCLUDE from the toolset", Show(Env.Get("INCLUDE").ToString().Contains(Path.Combine(r.ToolsetPath, "include"))), "true");
		Check("LIB for the target", Show(Env.Get("LIB").ToString().Contains(Path.Combine(r.ToolsetPath, "lib", r.TargetArch))), "true");
		KList cl = Env.Which("cl");
		Check("cl resolves in the toolset", Show(cl.Count() > 0 && cl[0].ToString().StartsWith(r.ToolsetPath, StringComparison.OrdinalIgnoreCase)), "true");
		Check("VisualStudioVersion", Env.Get("VisualStudioVersion"), r.Instance!.Version.Split('.')[0] + ".0");
	} else {
		Skip("toolset checks", "no Visual Studio with a C++ toolset on this machine");
		Check("no INCLUDE without a toolset or an SDK", Show(!Env.Has("INCLUDE") || r.HasSdk), "true");
	}
	if (r.HasSdk){
		Check("WindowsSdkDir set", Show(Env.Get("WindowsSdkDir").ToString().StartsWith(r.Sdk!.Path, StringComparison.OrdinalIgnoreCase)), "true");
		Check("UCRTVersion", Env.Get("UCRTVersion"), r.Sdk.Version);
		Check("INCLUDE has the UCRT", Show(Env.Get("INCLUDE").ToString().Contains(Path.Combine(r.Sdk.Path, "Include", r.Sdk.Version, "ucrt"))), "true");
	} else {
		Skip("SDK checks", "no Windows SDK on this machine");
	}
	Check("Restore", Show(e.Restore()), "true");
	Check("environment as before", Show(Env.Get("INCLUDE") == "C:\\stale\\include" && Env.Get("VSCMD_VER") == "stale" && !Env.Has("VCToolsInstallDir") && !Env.Has("WindowsSdkDir")), "true");
	Check("Restore again", Show(e.Restore()) + ", " + e.LastError.Code, "false, InvalidArgument");
	Env.Restore(before);
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [3/4] Requirements
// ------------------------------------------------------------------------------------------------

void TestRequirements(){
	Banner("[3/4] Requirements");
	Dictionary<string, string> before = Env.Snapshot();
	EnvWinMsvc probe = Quiet();
	probe.Find();
	EnvWinMsvcResult found = probe.LastEnvironment!;
	// A requirement nobody can meet: a warning, false, NotFound, nothing touched
	EnvWinMsvc e = Quiet();
	e.Options.Version = "9";
	Msg.Print("--- shown on screen: the warning of an unmet requirement ---");
	Check("unmet version", Show(e.Apply()) + ", " + e.LastError.Code, "false, NotFound");
	Check("nothing touched", Show(Env.Snapshot().Count == before.Count && !Env.Has("VCToolsInstallDir")), "true");
	e = Quiet();
	e.Options.Sdk = "1.2.3.4";
	Check("unmet SDK", Show(e.Apply()) + ", " + e.LastError.Code, "false, NotFound");
	e = Quiet();
	e.Options.HostArch = "mips";
	Check("bad architecture", Show(e.Find()) + ", " + e.LastError.Code, "false, InvalidArgument");
	e = Quiet();
	e.Options.Path = @"C:\no\such\folder";
	Check("bad path", Show(e.Find()) + ", " + e.LastError.Code, "false, NotFound");
	// That or newer: a bar nothing reaches fails, a bar everything passes succeeds
	e = Quiet();
	e.Options.Version = "99+";
	Check("unmet version or newer", Show(e.Find()) + ", " + e.LastError.Code, "false, NotFound");
	e = Quiet();
	e.Options.Sdk = "99.0+";
	Check("unmet SDK or newer", Show(e.Find()) + ", " + e.LastError.Code, "false, NotFound");
	if (found.HasToolset){
		// A requirement met: the same selection as the default
		e = Quiet();
		e.Options.Version = found.Instance!.Version.Split('.')[0];
		e.Options.VcTools = found.Toolset;
		Check("met version and toolset", Show(e.Find()) + ", " + (e.LastEnvironment?.Toolset ?? ""), "true, " + found.Toolset);
		e = Quiet();
		e.Options.Version = "[" + found.Instance.Version.Split('.')[0] + ".0," + (int.Parse(found.Instance.Version.Split('.')[0]) + 1) + ".0)";
		Check("met version range", Show(e.Find()), "true");
		e = Quiet();
		e.Options.Path = found.Instance.Path;
		Check("met path", Show(e.Find()) + ", " + Show(e.LastEnvironment?.Instance?.Path == found.Instance.Path), "true, true");
		// That or newer, met: the same selection as the default, and an older bar too
		string major = found.Instance.Version.Split('.')[0];
		string toolsetMajorMinor = string.Join(".", found.Toolset.Split('.').Take(2));
		e = Quiet();
		e.Options.Version = major + "+";
		e.Options.VcTools = toolsetMajorMinor + "+";
		Check("met version and toolset or newer", Show(e.Find()) + ", " + (e.LastEnvironment?.Toolset ?? ""), "true, " + found.Toolset);
		e = Quiet();
		e.Options.Version = (int.Parse(major) - 2) + "+";
		e.Options.VcTools = "14.0+";
		Check("met older bar or newer", Show(e.Find()) + ", " + (e.LastEnvironment?.Instance?.Version ?? ""), "true, " + found.Instance.Version);
		// Strict at the precision given: the exact toolset only itself, a wrong last part nothing
		e = Quiet();
		e.Options.VcTools = found.Toolset + ".1";
		Check("strict toolset unmet", Show(e.Find()) + ", " + e.LastError.Code, "false, NotFound");
		// Only the toolset asked: the SDK found is not set
		e = Quiet();
		e.Options.VcTools = found.Toolset;
		e.Options.SetUnspecified = false;
		Check("SetUnspecified false", Show(e.Apply() && Env.Has("VCToolsInstallDir") && !Env.Has("WindowsSdkDir")), "true");
		e.Restore();
	} else {
		Skip("requirements met", "no Visual Studio with a C++ toolset on this machine");
	}
	if (found.HasSdk){
		e = Quiet();
		e.Options.Sdk = found.Sdk!.Version;
		e.Options.SetUnspecified = false;
		Check("only the SDK", Show(e.Apply() && Env.Has("WindowsSdkDir") && !Env.Has("VCToolsInstallDir")), "true");
		e.Restore();
		e = Quiet();
		e.Options.Sdk = "10.0+";
		Check("met SDK or newer", Show(e.Find()) + ", " + (e.LastEnvironment?.Sdk?.Version ?? ""), "true, " + found.Sdk.Version);
	} else {
		Skip("SDK requirement", "no Windows SDK on this machine");
	}
	Env.Restore(before);
	EndBanner();
}

// ------------------------------------------------------------------------------------------------
// [4/4] Elsewhere
// ------------------------------------------------------------------------------------------------

void TestElsewhere(){
	Banner("[4/4] Outside Windows");
	EnvWinMsvc e = Quiet();
	Check("Find", Show(e.Find()) + ", " + e.LastError.Code, "false, NotSupported");
	Check("Apply", Show(e.Apply()) + ", " + e.LastError.Code, "false, NotSupported");
	Check("Instances", e.Instances().Count.ToString(), "0");
	Check("Sdks", e.Sdks().Count.ToString(), "0");
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

void Skip(string group, string reason){
	skipped++;
	Msg.PrintTask($"{group,-36} : skipped ");
	Msg.PrintTaskWarning(reason);
	TestResult("SKIPPED", group + " : " + reason);
}

string Show(bool value){
	return value ? "true" : "false";
}

void Check(string what, string actual, string expected){
	Msg.PrintTask($"{what,-36} : {actual,-44} ");
	if (actual == expected){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-36} : {expected}");
	}
	TestResult(actual == expected ? "OK" : "FAILED", what + " : " + actual);
}
