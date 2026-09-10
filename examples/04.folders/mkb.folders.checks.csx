/*---------------------------------------------------------------------------------------------------------

	Kombine Files, Folders and Compression Example

	(C)Kollective Networks 2026

	Report helpers shared by the main script and the child scripts through #load.
	A loaded file is compiled into the loading script, so every script keeps its own counters,
	prints its own summary and returns the number of failed checks to its parent.

---------------------------------------------------------------------------------------------------------*/

// The results of the checks, for the examples runner
#load "mkb.results.csx"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using System;

int passed = 0;
int failed = 0;

/// <summary>
/// Prints the banner of a group of checks and indents its lines.
/// </summary>
/// <param name="title">Group title.</param>
void Banner(string title){
	Msg.Print(title);
	Msg.BeginIndent();
	TestGroup(title);
}

/// <summary>
/// Closes a group of checks.
/// </summary>
void EndBanner(){
	Msg.EndIndent();
	Msg.RawPrint(Environment.NewLine);
}

/// <summary>
/// Compares the actual value against the expected one and reports the outcome.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="actual">Actual value.</param>
/// <param name="expected">Expected value.</param>
void Check(string what, string actual, string expected){
	Verify(what, actual, actual == expected, expected);
}

/// <summary>
/// Prints an aligned line with the actual value and a colored OK / FAILED tag.
/// On failure the expected value is printed below in red.
/// </summary>
/// <param name="what">Name of the check.</param>
/// <param name="actual">Actual value to be shown.</param>
/// <param name="ok">Outcome of the check.</param>
/// <param name="expected">Expected value, shown when the check failed.</param>
void Verify(string what, string actual, bool ok, string expected){
	Msg.PrintTask($"{what,-26} : {actual,-36} ");
	if (ok){
		Msg.PrintTaskSuccess("OK");
		passed++;
	} else {
		Msg.PrintTaskError("FAILED");
		failed++;
		Msg.PrintError($"{"expected",-26} : {expected}");
	}
	TestResult(ok ? "OK" : "FAILED", what + " : " + actual);
}

/// <summary>
/// Prints the summary line of the running script.
/// </summary>
/// <returns>The number of failed checks, to be returned to the parent script.</returns>
int Summary(){
	TestSummary(passed, failed);
	return failed;
}

/// <summary>
/// Renders a boolean as true / false
/// </summary>
/// <param name="value">Value to render.</param>
/// <returns>The rendered value.</returns>
string Show(bool value){
	return value ? "true" : "false";
}

/// <summary>
/// Renders a value between quotes, so empty values and spaces are visible.
/// </summary>
/// <param name="value">Value to render.</param>
/// <returns>The quoted value.</returns>
string Quote(KValue value){
	return "\"" + value + "\"";
}

/// <summary>
/// Normalizes the separators of a path to unix style.
/// </summary>
/// <param name="value">Path to normalize.</param>
/// <returns>The normalized path.</returns>
string Unix(KValue value){
	return value.ToString().Replace('\\', '/');
}

/// <summary>
/// Renders the last segments of a path, to keep long absolute paths short in the report.
/// </summary>
/// <param name="value">Path to render.</param>
/// <param name="segments">Number of trailing segments to keep.</param>
/// <returns>The shortened path, prefixed with ".../" when segments were dropped.</returns>
string Tail(KValue value, int segments){
	string[] parts = Unix(value).TrimEnd('/').Split('/');
	if (parts.Length <= segments)
		return string.Join("/", parts);
	return ".../" + string.Join("/", parts, parts.Length - segments, segments);
}
