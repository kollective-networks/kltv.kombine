/*---------------------------------------------------------------------------------------------------------

	Kombine examples: the results of the checks, for the examples runner

	(C)Kollective Networks 2026

	Every example prints its own report and keeps its own counters. When the examples runner drives
	the run it shares a list under "kombine.tests" (Share API), and every check of every script,
	child scripts included, is appended to it with its group, so the runner prints the summary of the
	whole run at the end without reading the log and without touching the message handler, which
	the examples test. Run standalone, the list does not exist and nothing is recorded.

	Loaded with #load by the report helpers of the examples: Banner calls TestGroup, the check
	helpers call TestResult with "OK", "FAILED" or "SKIPPED", and the summary line of the script is
	printed through TestSummary, which stays silent under the runner: one summary closes the run.

---------------------------------------------------------------------------------------------------------*/

using Kltv.Kombine.Api;
using System.Collections.Generic;

// The shared list of the runner, looked up when the script is evaluated; null when run standalone
List<string>? testResults = Share.Get("kombine.tests") as List<string>;
// The group (the banner) the following results belong to
string testGroup = "";

/// <summary>
/// Sets the group of the following results: the banner of the checks.
/// </summary>
/// <param name="title">The banner title.</param>
void TestGroup(string title){
	testGroup = title;
}

/// <summary>
/// Records the result of a check for the runner: "OK", "FAILED" or "SKIPPED", the group and the text.
/// </summary>
/// <param name="status">"OK", "FAILED" or "SKIPPED".</param>
/// <param name="text">The check as printed: its name and its value or reason.</param>
void TestResult(string status, string text){
	testResults?.Add(status + "|" + testGroup + "|" + text.Trim());
}

/// <summary>
/// Prints the summary line of the running script ("Summary : N of M checks passed" with an OK or
/// FAILED tag), unless the runner collects the results: it prints one summary for the whole run.
/// </summary>
/// <param name="passed">Checks passed.</param>
/// <param name="failed">Checks failed.</param>
/// <param name="skipped">Groups skipped, shown when not zero.</param>
/// <param name="what">The wording after the counts, "checks passed" by default.</param>
void TestSummary(int passed, int failed, int skipped = 0, string what = "checks passed"){
	if (testResults != null)
		return;
	Msg.PrintTask($"Summary : {passed} of {passed + failed} {what}" + (skipped > 0 ? $", {skipped} groups skipped " : " "));
	if (failed == 0)
		Msg.PrintTaskSuccess("OK");
	else
		Msg.PrintTaskError($"{failed} FAILED");
}
