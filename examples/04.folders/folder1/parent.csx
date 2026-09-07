/*---------------------------------------------------------------------------------------------------------

	Kombine Files, Folders and Compression Example: nested script

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Report helpers shared with the parent scripts
#load "../mkb.folders.checks.csx"

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

/// <summary>
/// Nested script run by the child script: checks the well known folders as seen from here.
/// </summary>
/// <param name="args"></param>
/// <returns>The number of failed checks.</returns>
int test(string[] args){
	Banner("parent.csx: folders seen from a nested script");
	Verify("CurrentScriptFolder", Tail(CurrentScriptFolder, 2), Unix(CurrentScriptFolder).EndsWith("/04.folders/folder1"), "a path ending in /04.folders/folder1");
	Verify("CurrentWorkingFolder", Tail(CurrentWorkingFolder, 2), Unix(CurrentWorkingFolder).EndsWith("/04.folders/folder1"), "a path ending in /04.folders/folder1");
	Verify("ParentScriptFolder", Tail(ParentScriptFolder, 2), Unix(ParentScriptFolder).EndsWith("/04.folders/child"), "a path ending in /04.folders/child");
	EndBanner();
	return Summary();
}
