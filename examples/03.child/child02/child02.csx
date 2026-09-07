/*---------------------------------------------------------------------------------------------------------

	Kombine Child Scripts Example: second child

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Report helpers shared with the parent script
#load "../mkb.child.checks.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

/// <summary>
/// Second child, sibling of the first one: receives the parent exports and the run wide registry,
/// but not the values exported by its sibling.
/// </summary>
/// <param name="args"></param>
/// <returns>The number of failed checks.</returns>
int test(string[] args){
	Banner("child02: data received as a sibling of child01");
	// Exported by the parent
	Check("Import myvar", Quote(KValue.Import("myvar", "not received")), "\"my value\"");
	// Exported by the sibling: exports only flow down to children, so the default is returned
	Check("Import another", Quote(KValue.Import("another", "not received")), "\"not received\"");
	// Registered by the sibling: the registry is run wide
	KValue regvalue = Share.Registry("myreg", "mykey");
	Verify("Registry myreg/mykey", Tail(regvalue, 3), Unix(regvalue).TrimEnd('/').EndsWith("/child01/includes"), "a path ending in /child01/includes");
	Check("Registry missing key", Quote(Share.Registry("myreg", "nokey")), "\"\"");
	EndBanner();
	return Summary();
}
