/*---------------------------------------------------------------------------------------------------------

	Kombine Files, Folders and Compression Example: child script

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Report helpers shared with the parent script
#load "../mkb.folders.checks.csx"

// Remember, this is just used for intellisense, nothing else
#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

/// <summary>
/// Child script: searches backwards (walking up from its own folder) for a file living next to the
/// parent script, checks the well known folders as seen from a child and runs a nested script that
/// is resolved through the backward trace.
/// </summary>
/// <param name="args"></param>
/// <returns>The number of failed checks.</returns>
int test(string[] args){
	Banner("child.csx: backward search from a child folder");
	// Backward search walks up from the script folder
	KValue found = Folders.SearchBackPath("folder1/parent.csx");
	Verify("SearchBackPath", Tail(found, 3), Unix(found).EndsWith("/04.folders/folder1/parent.csx"), "a path ending in /04.folders/folder1/parent.csx");
	Check("SearchBackPath missing", Quote(Folders.SearchBackPath("does.not.exist.txt")), "\"\"");
	// Well known folders from a child: its own folder and the folder of the script that ran it
	Verify("CurrentScriptFolder", Tail(CurrentScriptFolder, 2), Unix(CurrentScriptFolder).EndsWith("/04.folders/child"), "a path ending in /04.folders/child");
	Verify("ParentScriptFolder", Tail(ParentScriptFolder, 2), Unix(ParentScriptFolder).EndsWith("/examples/04.folders"), "a path ending in /examples/04.folders");
	// The nested script is not in this folder: it is resolved through the backward trace
	int code = Kombine("folder1/parent.csx", "test", args, false);
	Check("parent.csx returned", code.ToString(), "0");
	EndBanner();
	return Summary();
}
