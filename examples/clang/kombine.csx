/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/
#load "extensions/clang.csx"

#r "mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;


// Define the output artifact folders
// We make use of the builtin property "CurrentWorkingFolder" to define the output folders
// The current folder will be the folder where the script is executed
KValue OutputBin = CurrentWorkingFolder+"/out/bin/";
KValue OutputTmp = CurrentWorkingFolder+"/out/tmp/"; 
KValue OutputLib = CurrentWorkingFolder+"/out/lib/";


// Export them so they can be used by other scripts
OutputBin.Export("OutputBin");
OutputTmp.Export("OutputTmp");
OutputLib.Export("OutputLib");

/// <summary>
/// Build the project action.
/// </summary>
/// <returns>Exiting code</returns>
int build(string[] args){
	Msg.Print("Building example: ");

	// Open the compile commands file
	// and leave it prepared for the build process
	Clang clang = new Clang();
	clang.OpenCompileCommands("out/tmp/compile_commands.json");	

	// To check if we've admin rights
	if (!Host.IsRoot()) {
		Msg.Print("[!] This script is running with no root privileges.");
	} else {
		Msg.Print("[!] This script is running with root privileges.");
	}
	Msg.Print("----------------------------------------------------------");
	// You can check the return code to throw custom messages
	if (Kombine("lib/mylib.csx","build",args) != 0) {
		Msg.PrintError("Failed to build static library");
		return -1;
	}
	Msg.Print("----------------------------------------------------------");
	// Or just use the default values to let the script fail on error
	Kombine("exe/mybin.csx","build",args);
	Msg.Print("----------------------------------------------------------");
	if (Kombine("dll/mydll.csx","build",args) != 0) {
		Msg.PrintError("Failed to build shared library");
		return -1;
	}
	return 0;
}

/// <summary>
/// Clean the project action.
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int clean(string[] args){
	Msg.Print("Cleaning artifacts.");
	Msg.Print("----------------------------------------------------------");
	if (Kombine("lib/mylib.csx","clean",args) != 0) {
		Msg.PrintError("Failed to clean static library");
		return -1;
	}
	Msg.Print("----------------------------------------------------------");
	if (Kombine("exe/mybin.csx","clean",args) != 0) {
		Msg.PrintError("Failed to clean binary");
		return -1;
	}
	Msg.Print("----------------------------------------------------------");
	if (Kombine("dll/mydll.csx","clean",args) != 0) {
		Msg.PrintError("Failed to clean shared library");
		return -1;
	}	
	return 0;
}

/// <summary>
/// 
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int help(string[] args) {
	Msg.Print("Example script");
	Msg.Print("Usage: mkb build to build the target");
	Msg.Print("       mkb clean to clean the target");
	return 0;
}