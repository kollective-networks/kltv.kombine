/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

//
// Use the clang extension
//
#load "extensions/clang.csx"

// Remember, this is just used for intellisense, nothing else
#r "../../../out/bin/win-x64/debug/mkb.dll"
using Kltv.Kombine.Api;
using Kltv.Kombine.Types;
using static Kltv.Kombine.Api.Statics;
using static Kltv.Kombine.Api.Tool;

// Set this project name
//
KValue Name = "mybin";
// The folders we want to use for the output artifacts
// We import them but if not found we use our default values
KValue OutputBin = KValue.Import("OutputBin","out/bin/");
KValue OutputLib = KValue.Import("OutputLib","out/lib/");
KValue OutputTmp = KValue.Import("OutputTmp","out/tmp/");

// The sources to be compiled.
KList	src = Glob("src/**/*.c");
		src += Glob("src/**/*.cpp");
		
// Compile flags and includes / defines
KList CFlags = new KList { "-std=c17", "-g", "-O0", "-gdwarf-4" };
KList CxxFlags = new KList { "-std=c++20", "-g", "-O0", "-gdwarf-4" };
// Defines
KList Defines = new KList { "DEBUG" };
// Include directories
KList Includes = new KList { "../lib/inc/" };

// Linker flags and libraries
KList LinkerFlags = new KList();

// We will add to the output folders our current project name and the platform we're building on
// Library is not used in this case being this example a binary, but just for the record.
OutputLib += Name + "/" + Host.GetOSKind() + "/";
OutputBin += Name + "/" + Host.GetOSKind() + "/";
OutputTmp += Name + "/" + Host.GetOSKind() + "/";


/// <summary>
/// Build the project action.
/// </summary>
/// <returns>Exiting code</returns>
int build(string[] args){
	// Fancy printing if we want.
	Msg.Print("Building binary: "+Name);
	// Create an instance of the clang tool.
	Clang clang = new Clang();
	clang.OpenCompileCommands("out/tmp/compile_commands.json");	
	// Compile
	// -------------------------------------------------------
	clang.Options.IncludeDirs = Includes;
	clang.Options.Defines = Defines;
	clang.Options.SwitchesCC = CFlags;
	clang.Options.SwitchesCXX = CxxFlags;
	// Generate the list of object files to be used as output
	KList objs = src.WithExtension(clang.Options.ObjectExtension).WithPrefix(OutputTmp);
	// And compile the sources
	clang.Compile(src, objs);
	// Linker
	// -------------------------------------------------------
	clang.Options.SwitchesLD = LinkerFlags;
	//
	// As example we will add the object files generated from the bin2obj extension to the sources to be linked
	// except the amalgamation to avoid symbol redefinitions (since this is just a test and we generated the resources twice).
	// 
	objs += Glob("out/tmp/gen/**/*.obj");
	objs -= "out/tmp/gen/amalgamation.obj";
	// And link
	clang.Linker(objs, OutputBin + Name + clang.Options.BinaryExtension);
	Msg.PrintTask("Building binary: " + Name +" ");
	Msg.PrintTaskSuccess(" done");
	return 0;
}

/// <summary>
/// Clan the project action
/// </summary>
/// <param name="args"></param>
/// <returns></returns>
int clean(string[] args){
	Msg.Print("Cleaning artifacts for binary: "+Name);
	// This is just an example but you can just use Folders.Delete and destroy all the artifacts as well.
	Clang clang = new Clang();
	KList objs = src.WithExtension(clang.Options.ObjectExtension).WithPrefix(OutputTmp);
	KValue output = OutputBin + Name + clang.Options.BinaryExtension;
	clang.Clean(objs,output);
	return 0;
}

