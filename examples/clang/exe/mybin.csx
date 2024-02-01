/*---------------------------------------------------------------------------------------------------------

	Kombine Makefile example

	(C)Kollective Networks 2022

---------------------------------------------------------------------------------------------------------*/

#load "scripts/clang.csx"

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
// Library directories (We use the output folder and os to fetch the appropiate library folder)
KList LibraryDirs = new KList() { OutputLib+"mylib/"+ Host.GetOSKind() };
// Libraries
KList Libraries = new KList() { "mylib" };

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
	clang.IncludeDirs = Includes;
	clang.Defines = Defines;
	clang.SwitchesCC = CFlags;
	clang.SwitchesCXX = CxxFlags;
	// Generate the list of object files to be used as output
	KList objs = src.WithExtension(clang.ObjectExtension).WithPrefix(OutputTmp);
	// And compile the sources
	clang.Compile(src, objs);
	// Linker
	// -------------------------------------------------------
	clang.LibraryDirs = LibraryDirs;
	clang.Libraries = Libraries;
	clang.SwitchesLD = LinkerFlags;
	clang.Linker(objs, OutputBin + Name + clang.BinaryExtension);
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
	Clang clang = new Clang();
	KList objs = src.WithExtension(clang.ObjectExtension).WithPrefix(OutputTmp);
	KValue output = OutputBin + Name + clang.BinaryExtension;
	clang.Clean(objs,output);
	return 0;
}

