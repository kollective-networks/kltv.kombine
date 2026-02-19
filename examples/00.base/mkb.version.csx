/*---------------------------------------------------------------------------------------------------------

	Kombine Base Version Functions Example

	(C)Kollective Networks 2026

---------------------------------------------------------------------------------------------------------*/

// Remember, this is just used for intellisense, nothing else
#r "../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;


/// <summary>
/// Execute all the scripts as test.
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int test(string[] args){
	Msg.Print("----------------------------------------------------------");
	Msg.BeginIndent();
	Msg.Print("-Testing version functions");
	Msg.BeginIndent();
	string version = MkbVersion();
	Msg.Print("Kombine version is: " + version);
	int major = MkbMajorVersion();
	Msg.Print("Kombine major version is: " + major);
	int minor = MkbMinorVersion();
	Msg.Print("Kombine minor version is: " + minor);
	int hex = MkbHexVersion();
	Msg.Print("Kombine hex version is: " + hex);
	//
	// Hex version is provided to allow quick comparisons between versions
	// Example
	if (hex >= 0x0102){
		Msg.Print("Kombine version is at least 1.2");
	} else {
		Msg.Print("Kombine version is below 1.2");
	}
	Msg.EndIndent();
	Msg.EndIndent();
	Msg.Print("----------------------------------------------------------");
	Msg.Print("");
	return 0;
}

